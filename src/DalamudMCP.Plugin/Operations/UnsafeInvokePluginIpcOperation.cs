using System.Globalization;
using System.Reflection;
using System.Runtime.Versioning;
using System.Text.Json;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;
using DalamudMCP.Framework;
using DalamudMCP.Protocol;
using MemoryPack;

namespace DalamudMCP.Plugin.Operations;

[Operation(
    "unsafe.invoke.plugin-ipc",
    Description = "Developer-only escape hatch for invoking arbitrary plugin IPC function callgates.",
    Summary = "Invokes an arbitrary plugin IPC function callgate.")]
[ResultFormatter(typeof(UnsafeInvokePluginIpcOperation.TextFormatter))]
[CliCommand("unsafe", "invoke", "plugin-ipc")]
[McpTool("unsafe_invoke_plugin_ipc")]
public sealed partial class UnsafeInvokePluginIpcOperation
    : IOperation<UnsafeInvokePluginIpcOperation.Request, UnsafeInvokePluginIpcResult>
{
    private readonly Func<Request, CancellationToken, ValueTask<UnsafeInvokePluginIpcResult>> executor;

    [SupportedOSPlatform("windows")]
    public UnsafeInvokePluginIpcOperation(
        IDalamudPluginInterface pluginInterface,
        IFramework framework)
    {
        ArgumentNullException.ThrowIfNull(pluginInterface);
        ArgumentNullException.ThrowIfNull(framework);

        executor = CreateDalamudExecutor(new PluginIpcGateway(pluginInterface), framework);
    }

    internal UnsafeInvokePluginIpcOperation(Func<Request, CancellationToken, ValueTask<UnsafeInvokePluginIpcResult>> executor)
    {
        this.executor = executor ?? throw new ArgumentNullException(nameof(executor));
    }

    public ValueTask<UnsafeInvokePluginIpcResult> ExecuteAsync(Request request, OperationContext context)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(context);

        return executor(request, context.CancellationToken);
    }

    [MemoryPackable]
    [ProtocolOperation("unsafe.invoke.plugin-ipc")]
    public sealed partial class Request
    {
        [Option("callgate", Description = "Exact IPC callgate name to invoke.")]
        public string Callgate { get; init; } = string.Empty;

        [Option("result-kind", Description = "Expected result kind: bool,int,uint,long,ulong,float,double,string.")]
        public string ResultKind { get; init; } = string.Empty;

        [Option("argument-kinds", Description = "Comma-separated argument kinds: bool,int,uint,long,ulong,float,double,string.", Required = false)]
        public string? ArgumentKinds { get; init; }

        [Option("arguments-json", Description = "JSON array containing the IPC function arguments.", Required = false)]
        public string? ArgumentsJson { get; init; }

        [Option("run-on-framework-thread", Description = "Run the IPC call on Dalamud's framework thread.", Required = false)]
        public bool? RunOnFrameworkThread { get; init; }
    }

    public sealed class TextFormatter : IResultFormatter<UnsafeInvokePluginIpcResult>
    {
        public string? FormatText(UnsafeInvokePluginIpcResult result, OperationContext context)
        {
            ArgumentNullException.ThrowIfNull(result);
            ArgumentNullException.ThrowIfNull(context);

            return result.SummaryText;
        }
    }

    [SupportedOSPlatform("windows")]
    private static Func<Request, CancellationToken, ValueTask<UnsafeInvokePluginIpcResult>> CreateDalamudExecutor(
        IPluginIpcGateway gateway,
        IFramework framework)
    {
        return async (request, cancellationToken) =>
        {
            cancellationToken.ThrowIfCancellationRequested();
            bool runOnFrameworkThread = request.RunOnFrameworkThread ?? true;
            if (!runOnFrameworkThread || framework.IsInFrameworkUpdateThread)
                return InvokeUnsafeIpc(gateway, request);

            return await framework.RunOnFrameworkThread(() => InvokeUnsafeIpc(gateway, request)).ConfigureAwait(false);
        };
    }

    internal static UnsafeInvokePluginIpcResult InvokeUnsafeIpc(IPluginIpcGateway gateway, Request request)
    {
        ArgumentNullException.ThrowIfNull(gateway);
        ArgumentNullException.ThrowIfNull(request);

        string callgate = string.IsNullOrWhiteSpace(request.Callgate)
            ? throw new ArgumentException("callgate is required.", nameof(request))
            : request.Callgate.Trim();
        PluginIpcValueKind resultKind = ParseKind(request.ResultKind);
        PluginIpcValueKind[] argumentKinds = ParseKindsCsv(request.ArgumentKinds);
        object?[] arguments = ParseArguments(argumentKinds, request.ArgumentsJson);
        Type[] typeArguments = BuildTypeArguments(argumentKinds, resultKind);

        if (!gateway.TryCreate(callgate, typeArguments, out IPluginCallGateSubscriber? subscriber) ||
            subscriber is null ||
            !subscriber.HasFunction)
        {
            return new UnsafeInvokePluginIpcResult(
                callgate,
                false,
                "ipc_missing",
                resultKind.ToString().ToLowerInvariant(),
                null,
                $"No IPC function subscriber matched '{callgate}'.");
        }

        try
        {
            object? result = subscriber.InvokeFunc(arguments);
            string resultJson = JsonSerializer.Serialize(result, typeArguments[^1]);
            return new UnsafeInvokePluginIpcResult(
                callgate,
                true,
                null,
                resultKind.ToString().ToLowerInvariant(),
                resultJson,
                $"IPC '{callgate}' returned {resultJson}.");
        }
        catch (TargetInvocationException exception) when (exception.InnerException is not null)
        {
            return new UnsafeInvokePluginIpcResult(
                callgate,
                false,
                "ipc_error",
                resultKind.ToString().ToLowerInvariant(),
                null,
                $"IPC '{callgate}' failed: {exception.InnerException.Message}");
        }
        catch (Exception exception)
        {
            return new UnsafeInvokePluginIpcResult(
                callgate,
                false,
                "ipc_error",
                resultKind.ToString().ToLowerInvariant(),
                null,
                $"IPC '{callgate}' failed: {exception.Message}");
        }
    }

    internal static PluginIpcValueKind[] ParseKindsCsv(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return [];

        string[] segments = value.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        PluginIpcValueKind[] kinds = new PluginIpcValueKind[segments.Length];
        for (int index = 0; index < segments.Length; index++)
            kinds[index] = ParseKind(segments[index]);

        return kinds;
    }

    internal static PluginIpcValueKind ParseKind(string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);
        return value.Trim().ToLowerInvariant() switch
        {
            "bool" or "boolean" => PluginIpcValueKind.Bool,
            "int" or "int32" => PluginIpcValueKind.Whole32,
            "uint" or "uint32" => PluginIpcValueKind.UWhole32,
            "long" or "int64" => PluginIpcValueKind.Whole64,
            "ulong" or "uint64" => PluginIpcValueKind.UWhole64,
            "float" or "single" => PluginIpcValueKind.Fraction32,
            "double" => PluginIpcValueKind.Fraction64,
            "string" or "text" => PluginIpcValueKind.Text,
            _ => throw new ArgumentException($"Unsupported IPC value kind '{value}'.", nameof(value))
        };
    }

    private static Type[] BuildTypeArguments(
        PluginIpcValueKind[] argumentKinds,
        PluginIpcValueKind resultKind)
    {
        Type[] typeArguments = new Type[argumentKinds.Length + 1];
        for (int index = 0; index < argumentKinds.Length; index++)
            typeArguments[index] = GetClrType(argumentKinds[index]);

        typeArguments[^1] = GetClrType(resultKind);
        return typeArguments;
    }

    private static object?[] ParseArguments(PluginIpcValueKind[] argumentKinds, string? argumentsJson)
    {
        if (argumentKinds.Length == 0)
            return [];

        if (string.IsNullOrWhiteSpace(argumentsJson))
            throw new ArgumentException("arguments-json is required when argument-kinds are specified.", nameof(argumentsJson));

        using JsonDocument document = JsonDocument.Parse(argumentsJson);
        if (document.RootElement.ValueKind is not JsonValueKind.Array)
            throw new ArgumentException("arguments-json must be a JSON array.", nameof(argumentsJson));

        JsonElement[] values = document.RootElement.EnumerateArray().ToArray();
        if (values.Length != argumentKinds.Length)
        {
            throw new ArgumentException(
                $"arguments-json contained {values.Length} values but {argumentKinds.Length} argument-kinds were provided.",
                nameof(argumentsJson));
        }

        object?[] arguments = new object?[argumentKinds.Length];
        for (int index = 0; index < argumentKinds.Length; index++)
            arguments[index] = ParseElement(values[index], argumentKinds[index]);

        return arguments;
    }

    private static object ParseElement(JsonElement element, PluginIpcValueKind kind)
    {
        return kind switch
        {
            PluginIpcValueKind.Bool => element.GetBoolean(),
            PluginIpcValueKind.Whole32 => element.GetInt32(),
            PluginIpcValueKind.UWhole32 => ReadUnsignedInteger<uint>(element),
            PluginIpcValueKind.Whole64 => element.GetInt64(),
            PluginIpcValueKind.UWhole64 => ReadUnsignedInteger<ulong>(element),
            PluginIpcValueKind.Fraction32 => ReadFloatingPoint<float>(element),
            PluginIpcValueKind.Fraction64 => element.GetDouble(),
            PluginIpcValueKind.Text => element.GetString() ?? string.Empty,
            _ => throw new InvalidOperationException($"Unsupported IPC value kind '{kind}'.")
        };
    }

    private static T ReadUnsignedInteger<T>(JsonElement element)
        where T : unmanaged, System.Numerics.IBinaryInteger<T>
    {
        if (element.ValueKind is JsonValueKind.Number)
        {
            long value = element.GetInt64();
            if (value < 0)
                throw new ArgumentException("Unsigned IPC values cannot be negative.");

            return T.CreateChecked(value);
        }

        if (element.ValueKind is JsonValueKind.String &&
            ulong.TryParse(element.GetString(), NumberStyles.Integer, CultureInfo.InvariantCulture, out ulong parsed))
        {
            return T.CreateChecked(parsed);
        }

        throw new ArgumentException("The JSON value was not a valid unsigned integer.");
    }

    private static T ReadFloatingPoint<T>(JsonElement element)
        where T : unmanaged, System.Numerics.IFloatingPoint<T>
    {
        return element.ValueKind switch
        {
            JsonValueKind.Number => T.CreateChecked(element.GetDouble()),
            JsonValueKind.String when double.TryParse(
                element.GetString(),
                NumberStyles.Float | NumberStyles.AllowThousands,
                CultureInfo.InvariantCulture,
                out double parsed) => T.CreateChecked(parsed),
            _ => throw new ArgumentException("The JSON value was not a valid floating-point number.")
        };
    }

    private static Type GetClrType(PluginIpcValueKind kind)
    {
        return kind switch
        {
            PluginIpcValueKind.Bool => typeof(bool),
            PluginIpcValueKind.Whole32 => typeof(int),
            PluginIpcValueKind.UWhole32 => typeof(uint),
            PluginIpcValueKind.Whole64 => typeof(long),
            PluginIpcValueKind.UWhole64 => typeof(ulong),
            PluginIpcValueKind.Fraction32 => typeof(float),
            PluginIpcValueKind.Fraction64 => typeof(double),
            PluginIpcValueKind.Text => typeof(string),
            _ => throw new InvalidOperationException($"Unsupported IPC value kind '{kind}'.")
        };
    }

    internal interface IPluginIpcGateway
    {
        public bool TryCreate(string callgate, IReadOnlyList<Type> typeArguments, out IPluginCallGateSubscriber? subscriber);
    }

    internal interface IPluginCallGateSubscriber
    {
        public bool HasFunction { get; }

        public object? InvokeFunc(IReadOnlyList<object?> arguments);
    }

    internal sealed class PluginIpcGateway : IPluginIpcGateway
    {
        private static readonly MethodInfo[] GetSubscriberMethods = typeof(IDalamudPluginInterface)
            .GetMethods(BindingFlags.Instance | BindingFlags.Public)
            .Where(static method =>
                string.Equals(method.Name, "GetIpcSubscriber", StringComparison.Ordinal) &&
                method.IsGenericMethodDefinition)
            .OrderBy(static method => method.GetGenericArguments().Length)
            .ToArray();

        private readonly IDalamudPluginInterface pluginInterface;

        public PluginIpcGateway(IDalamudPluginInterface pluginInterface)
        {
            this.pluginInterface = pluginInterface ?? throw new ArgumentNullException(nameof(pluginInterface));
        }

        public bool TryCreate(string callgate, IReadOnlyList<Type> typeArguments, out IPluginCallGateSubscriber? subscriber)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(callgate);
            ArgumentNullException.ThrowIfNull(typeArguments);

            MethodInfo? method = GetSubscriberMethods.FirstOrDefault(candidate => candidate.GetGenericArguments().Length == typeArguments.Count);
            if (method is null)
            {
                subscriber = null;
                return false;
            }

            object? rawSubscriber = method.MakeGenericMethod(typeArguments.ToArray()).Invoke(pluginInterface, [callgate]);
            if (rawSubscriber is null)
            {
                subscriber = null;
                return false;
            }

            subscriber = new ReflectionPluginCallGateSubscriber(rawSubscriber);
            return true;
        }
    }

    internal sealed class ReflectionPluginCallGateSubscriber : IPluginCallGateSubscriber
    {
        private readonly object subscriber;
        private readonly PropertyInfo hasFunctionProperty;
        private readonly MethodInfo invokeFuncMethod;

        public ReflectionPluginCallGateSubscriber(object subscriber)
        {
            this.subscriber = subscriber ?? throw new ArgumentNullException(nameof(subscriber));
            Type type = subscriber.GetType();
            hasFunctionProperty = type.GetProperty("HasFunction", BindingFlags.Instance | BindingFlags.Public)
                ?? throw new InvalidOperationException("The IPC subscriber did not expose HasFunction.");
            invokeFuncMethod = type.GetMethod("InvokeFunc", BindingFlags.Instance | BindingFlags.Public)
                ?? throw new InvalidOperationException("The IPC subscriber did not expose InvokeFunc.");
        }

        public bool HasFunction => (bool?)hasFunctionProperty.GetValue(subscriber) ?? false;

        public object? InvokeFunc(IReadOnlyList<object?> arguments)
        {
            ArgumentNullException.ThrowIfNull(arguments);
            return invokeFuncMethod.Invoke(subscriber, arguments.ToArray());
        }
    }
}

public enum PluginIpcValueKind
{
    Bool = 0,
    Whole32 = 1,
    UWhole32 = 2,
    Whole64 = 3,
    UWhole64 = 4,
    Fraction32 = 5,
    Fraction64 = 6,
    Text = 7
}

[MemoryPackable]
public sealed partial record UnsafeInvokePluginIpcResult(
    string Callgate,
    bool Succeeded,
    string? Reason,
    string ResultKind,
    string? ResultJson,
    string SummaryText);
