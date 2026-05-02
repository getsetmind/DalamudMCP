using System.Reflection;
using System.Runtime.Versioning;
using System.Text.Json;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;
using DalamudMCP.Protocol;
using Manifold;
using MemoryPack;

namespace DalamudMCP.Plugin.Operations;

[Operation(
    "plugin.ipc",
    Description = "Invokes a convention-based plugin IPC function using the callgate name {PluginName}.MCP.{Method}. Primitive JSON arguments are inferred as bool, int, double, or string. JSON objects and arrays are passed as string envelopes.",
    Summary = "Invokes a convention-based plugin IPC function.")]
[ResultFormatter(typeof(SafeInvokePluginIpcOperation.TextFormatter))]
[CliCommand("plugin", "ipc")]
[McpTool("invoke_plugin_ipc")]
public sealed partial class SafeInvokePluginIpcOperation
    : IOperation<SafeInvokePluginIpcOperation.Request, SafeInvokePluginIpcResult>
{
    private readonly Func<Request, CancellationToken, ValueTask<SafeInvokePluginIpcResult>> executor;

    [SupportedOSPlatform("windows")]
    public SafeInvokePluginIpcOperation(IDalamudPluginInterface pluginInterface, IFramework framework)
    {
        ArgumentNullException.ThrowIfNull(pluginInterface);
        ArgumentNullException.ThrowIfNull(framework);

        executor = CreatePluginExecutor(new PluginIpcGateway(pluginInterface), framework);
    }

    internal SafeInvokePluginIpcOperation(Func<Request, CancellationToken, ValueTask<SafeInvokePluginIpcResult>> executor)
    {
        this.executor = executor ?? throw new ArgumentNullException(nameof(executor));
    }

    internal SafeInvokePluginIpcOperation(IPluginIpcGateway gateway, IFramework framework)
    {
        ArgumentNullException.ThrowIfNull(gateway);
        ArgumentNullException.ThrowIfNull(framework);

        executor = CreatePluginExecutor(gateway, framework);
    }

    public ValueTask<SafeInvokePluginIpcResult> ExecuteAsync(Request request, OperationContext context)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(context);

        return executor(request, context.CancellationToken);
    }

    [MemoryPackable]
    [ProtocolOperation("plugin.ipc")]
    public sealed partial class Request
    {
        [Option("plugin-name", Description = "Target plugin InternalName.")]
        public string PluginName { get; init; } = string.Empty;

        [Option("method", Description = "IPC method name. The full callgate is {plugin-name}.MCP.{method}.")]
        public string Method { get; init; } = string.Empty;

        [Option("arguments-json", Description = "JSON array containing IPC arguments.", Required = false)]
        public string? ArgumentsJson { get; init; }
    }

    public sealed class TextFormatter : IResultFormatter<SafeInvokePluginIpcResult>
    {
        public string? FormatText(SafeInvokePluginIpcResult result, OperationContext context)
        {
            ArgumentNullException.ThrowIfNull(result);
            ArgumentNullException.ThrowIfNull(context);

            return result.SummaryText;
        }
    }

    [SupportedOSPlatform("windows")]
    private static Func<Request, CancellationToken, ValueTask<SafeInvokePluginIpcResult>> CreatePluginExecutor(
        IPluginIpcGateway gateway,
        IFramework framework)
    {
        return async (request, cancellationToken) =>
        {
            cancellationToken.ThrowIfCancellationRequested();
            ArgumentException.ThrowIfNullOrWhiteSpace(request.PluginName);
            ArgumentException.ThrowIfNullOrWhiteSpace(request.Method);

            if (framework.IsInFrameworkUpdateThread)
                return InvokeSafeIpc(gateway, request);

            return await framework.RunOnFrameworkThread(() => InvokeSafeIpc(gateway, request)).ConfigureAwait(false);
        };
    }

    internal static SafeInvokePluginIpcResult InvokeSafeIpc(IPluginIpcGateway gateway, Request request)
    {
        ArgumentNullException.ThrowIfNull(gateway);
        ArgumentNullException.ThrowIfNull(request);

        string pluginName = string.IsNullOrWhiteSpace(request.PluginName)
            ? throw new ArgumentException("plugin-name is required.", nameof(request))
            : request.PluginName.Trim();
        string method = string.IsNullOrWhiteSpace(request.Method)
            ? throw new ArgumentException("method is required.", nameof(request))
            : request.Method.Trim();
        string callgate = $"{pluginName}.MCP.{method}";

        try
        {
            (object?[] arguments, Type[] argumentTypes) = ParseArguments(request.ArgumentsJson);
            Type[] typeArguments = [.. argumentTypes, typeof(object)];

            if (!gateway.TryCreate(callgate, typeArguments, out IPluginCallGateSubscriber? subscriber) ||
                subscriber is null)
            {
                return Failure(pluginName, method, "ipc_missing", null, $"No IPC subscriber found for callgate '{callgate}'.");
            }

            if (!subscriber.HasFunction)
                return Failure(pluginName, method, "ipc_not_ready", null, $"IPC function for callgate '{callgate}' is not registered yet.");

            object? result = subscriber.InvokeFunc(arguments);
            string returnJson = JsonSerializer.Serialize<object?>(result);
            return new SafeInvokePluginIpcResult(
                pluginName,
                method,
                true,
                "ipc_success",
                returnJson,
                null,
                $"IPC '{callgate}' succeeded. Return value: {returnJson}.");
        }
        catch (InvalidCastException exception)
        {
            return Failure(pluginName, method, "ipc_type_mismatch", $"Type mismatch: {exception.Message}", $"IPC call failed: type mismatch for callgate '{callgate}'.");
        }
        catch (TargetInvocationException exception) when (exception.InnerException is InvalidCastException inner)
        {
            return Failure(pluginName, method, "ipc_type_mismatch", $"Type mismatch: {inner.Message}", $"IPC call failed: type mismatch for callgate '{callgate}'.");
        }
        catch (TargetInvocationException exception) when (exception.InnerException is not null)
        {
            return Failure(pluginName, method, "ipc_plugin_error", exception.InnerException.Message, $"IPC call failed: plugin error for callgate '{callgate}'. {exception.InnerException.Message}");
        }
        catch (Exception exception)
        {
            return Failure(pluginName, method, "ipc_plugin_error", exception.Message, $"IPC call failed: plugin error for callgate '{callgate}'. {exception.Message}");
        }
    }

    private static SafeInvokePluginIpcResult Failure(
        string pluginName,
        string method,
        string status,
        string? errorMessage,
        string summaryText)
    {
        return new SafeInvokePluginIpcResult(pluginName, method, false, status, null, errorMessage, summaryText);
    }

    private static (object?[] Arguments, Type[] Types) ParseArguments(string? argumentsJson)
    {
        if (string.IsNullOrWhiteSpace(argumentsJson))
            return ([], []);

        using JsonDocument document = JsonDocument.Parse(argumentsJson);
        if (document.RootElement.ValueKind is not JsonValueKind.Array)
            throw new ArgumentException("arguments-json must be a JSON array.", nameof(argumentsJson));

        JsonElement[] elements = document.RootElement.EnumerateArray().ToArray();
        object?[] arguments = new object?[elements.Length];
        Type[] types = new Type[elements.Length];

        for (int index = 0; index < elements.Length; index++)
            (arguments[index], types[index]) = ParseJsonElement(elements[index]);

        return (arguments, types);
    }

    private static (object? Value, Type Type) ParseJsonElement(JsonElement element)
    {
        return element.ValueKind switch
        {
            JsonValueKind.True => (true, typeof(bool)),
            JsonValueKind.False => (false, typeof(bool)),
            JsonValueKind.Number when element.TryGetInt32(out int value) => (value, typeof(int)),
            JsonValueKind.Number => (element.GetDouble(), typeof(double)),
            JsonValueKind.String => (element.GetString() ?? string.Empty, typeof(string)),
            JsonValueKind.Object or JsonValueKind.Array => (element.GetRawText(), typeof(string)),
            JsonValueKind.Null => (null, typeof(object)),
            _ => throw new ArgumentException($"Unsupported JSON value kind: {element.ValueKind}")
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

    private sealed class PluginIpcGateway : IPluginIpcGateway
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

    private sealed class ReflectionPluginCallGateSubscriber : IPluginCallGateSubscriber
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

[MemoryPackable]
public sealed partial record SafeInvokePluginIpcResult(
    string PluginName,
    string Method,
    bool Success,
    string Status,
    string? ReturnValue,
    string? ErrorMessage,
    string SummaryText);
