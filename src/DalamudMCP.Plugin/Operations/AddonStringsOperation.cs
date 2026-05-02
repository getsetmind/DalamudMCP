using System.Globalization;
using System.Runtime.Versioning;
using System.Text;
using Dalamud.Game.NativeWrapper;
using Dalamud.Plugin.Services;
using Manifold;
using DalamudMCP.Plugin.Readers;
using DalamudMCP.Protocol;
using MemoryPack;

namespace DalamudMCP.Plugin.Operations;

[Operation(
    "addon.strings",
    Description = "Gets addon string table values.",
    Summary = "Gets addon string table values.")]
[ResultFormatter(typeof(AddonStringsOperation.TextFormatter))]
[CliCommand("addon", "strings")]
[McpTool("get_addon_strings")]
public sealed partial class AddonStringsOperation
    : IOperation<AddonStringsOperation.Request, AddonStringsSnapshot>, IPluginReaderStatus
{
    private readonly Func<Request, CancellationToken, ValueTask<AddonStringsSnapshot>> executor;
    private readonly Func<bool>? isReadyProvider;
    private readonly Func<string>? detailProvider;
    private readonly string unavailableDetail;

    [SupportedOSPlatform("windows")]
    public AddonStringsOperation(
        IFramework framework,
        IClientState clientState,
        IGameGui gameGui)
    {
        ArgumentNullException.ThrowIfNull(framework);
        ArgumentNullException.ThrowIfNull(clientState);
        ArgumentNullException.ThrowIfNull(gameGui);

        executor = CreateDalamudExecutor(framework, clientState, gameGui);
        isReadyProvider = () => clientState.IsLoggedIn;
        detailProvider = () => clientState.IsLoggedIn ? "ready" : "not_logged_in";
        unavailableDetail = "not_logged_in";
    }

    internal AddonStringsOperation(
        Func<Request, CancellationToken, ValueTask<AddonStringsSnapshot>> executor,
        bool isReady = true,
        string detail = "ready")
    {
        this.executor = executor ?? throw new ArgumentNullException(nameof(executor));
        isReadyProvider = () => isReady;
        detailProvider = () => string.IsNullOrWhiteSpace(detail) ? "ready" : detail;
        unavailableDetail = string.IsNullOrWhiteSpace(detail) ? "ready" : detail;
    }

    public string ReaderKey => "addon.strings";

    public bool IsReady => isReadyProvider?.Invoke() ?? false;

    public string Detail => detailProvider?.Invoke() ?? unavailableDetail;

    public ValueTask<AddonStringsSnapshot> ExecuteAsync(Request request, OperationContext context)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(context);

        return executor(request, context.CancellationToken);
    }

    [MemoryPackable]
    [ProtocolOperation("addon.strings")]
    [LegacyBridgeRequest("GetAddonStrings")]
    public sealed partial class Request
    {
        [Option("addon", Description = "Addon name to inspect.")]
        public string AddonName { get; init; } = string.Empty;
    }

    public sealed class TextFormatter : IResultFormatter<AddonStringsSnapshot>
    {
        public string? FormatText(AddonStringsSnapshot result, OperationContext context)
        {
            ArgumentNullException.ThrowIfNull(result);
            ArgumentNullException.ThrowIfNull(context);

            return $"{result.AddonName} entryCount={result.Entries.Length.ToString(CultureInfo.InvariantCulture)}";
        }
    }

    [SupportedOSPlatform("windows")]
    private static Func<Request, CancellationToken, ValueTask<AddonStringsSnapshot>> CreateDalamudExecutor(
        IFramework framework,
        IClientState clientState,
        IGameGui gameGui)
    {
        return async (request, cancellationToken) =>
        {
            cancellationToken.ThrowIfCancellationRequested();
            string addonName = NormalizeAddonName(request.AddonName);

            if (framework.IsInFrameworkUpdateThread)
                return ReadCurrentCore(clientState, gameGui, addonName, cancellationToken);

            return await framework.RunOnFrameworkThread(() => ReadCurrentCore(clientState, gameGui, addonName, cancellationToken))
                .ConfigureAwait(false);
        };
    }

    [SupportedOSPlatform("windows")]
    private static AddonStringsSnapshot ReadCurrentCore(
        IClientState clientState,
        IGameGui gameGui,
        string addonName,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (!clientState.IsLoggedIn)
            throw new InvalidOperationException("Addon strings are not available because the local player is not logged in.");

        AtkUnitBasePtr addon = gameGui.GetAddonByName(addonName, 1);
        if (addon.IsNull || !addon.IsReady)
            throw new InvalidOperationException($"Addon '{addonName}' is not ready.");

        List<object?> values = new();
        foreach (AtkValuePtr atkValue in addon.AtkValues)
        {
            try
            {
                values.Add(atkValue.GetValue());
            }
            catch (NotImplementedException)
            {
                values.Add(null);
            }
        }

        return new AddonStringsSnapshot(
            addonName,
            DateTimeOffset.UtcNow,
            CreateEntries(values));
    }

    private static string NormalizeAddonName(string addonName)
    {
        return string.IsNullOrWhiteSpace(addonName)
            ? throw new ArgumentException("Addon name is required.", nameof(addonName))
            : addonName.Trim();
    }

    private static AddonStringEntry[] CreateEntries(IEnumerable<object?> values)
    {
        List<AddonStringEntry> entries = new();
        int index = 0;
        foreach (object? value in values)
        {
            string? formatted = FormatValue(value);
            if (!string.IsNullOrWhiteSpace(formatted))
                entries.Add(new AddonStringEntry(index, formatted, formatted));

            index++;
        }

        return [.. entries];
    }

    private static string? FormatValue(object? value)
    {
        return value switch
        {
            null => null,
            string text when string.IsNullOrWhiteSpace(text) => null,
            string text => FormatGameText(text),
            bool flag => flag ? "true" : "false",
            IFormattable formattable => formattable.ToString(null, CultureInfo.InvariantCulture),
            _ => value.ToString()
        };
    }

    private static string FormatGameText(string text)
    {
        if (!ContainsControlCharacters(text))
            return text;

        StringBuilder builder = new(text.Length);
        for (int index = 0; index < text.Length; index++)
        {
            char current = text[index];
            if (current == '\u0002')
            {
                int endIndex = index + 1;
                while (endIndex < text.Length &&
                       char.IsControl(text[endIndex]) &&
                       text[endIndex] is not '\r' and not '\n' and not '\t')
                {
                    endIndex++;
                }

                if (endIndex > index + 1)
                {
                    AppendPayloadToken(builder, text.AsSpan(index, endIndex - index));
                    index = endIndex - 1;
                    continue;
                }
            }

            if (char.IsControl(current) && current is not '\r' and not '\n' and not '\t')
            {
                builder.Append("\\u");
                builder.Append(((int)current).ToString("X4", CultureInfo.InvariantCulture));
                continue;
            }

            builder.Append(current);
        }

        return builder.ToString();
    }

    private static bool ContainsControlCharacters(string text)
    {
        foreach (char character in text)
            if (char.IsControl(character) && character is not '\r' and not '\n' and not '\t')
                return true;

        return false;
    }

    private static void AppendPayloadToken(StringBuilder builder, ReadOnlySpan<char> payload)
    {
        builder.Append("[payload:");
        for (int index = 0; index < payload.Length; index++)
        {
            if (index > 0)
                builder.Append('-');

            builder.Append(((int)payload[index]).ToString("X2", CultureInfo.InvariantCulture));
        }

        builder.Append(']');
    }
}

[MemoryPackable]
public sealed partial record AddonStringEntry(
    int Index,
    string? RawValue,
    string? DecodedValue);

[MemoryPackable]
public sealed partial record AddonStringsSnapshot(
    string AddonName,
    DateTimeOffset CapturedAt,
    AddonStringEntry[] Entries);



