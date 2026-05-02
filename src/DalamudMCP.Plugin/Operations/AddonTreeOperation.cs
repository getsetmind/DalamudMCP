using System.Globalization;
using System.Runtime.Versioning;
using Dalamud.Game.NativeWrapper;
using Dalamud.Plugin.Services;
using Manifold;
using DalamudMCP.Plugin.Readers;
using DalamudMCP.Protocol;
using FFXIVClientStructs.FFXIV.Component.GUI;
using FFXIVClientStructs.Interop;
using MemoryPack;

namespace DalamudMCP.Plugin.Operations;

[Operation(
    "addon.tree",
    Description = "Gets an addon UI tree.",
    Summary = "Gets an addon UI tree.")]
[ResultFormatter(typeof(AddonTreeOperation.TextFormatter))]
[CliCommand("addon", "tree")]
[McpTool("get_addon_tree")]
public sealed partial class AddonTreeOperation
    : IOperation<AddonTreeOperation.Request, AddonTreeSnapshot>, IPluginReaderStatus
{
    private readonly Func<Request, CancellationToken, ValueTask<AddonTreeSnapshot>> executor;
    private readonly Func<bool>? isReadyProvider;
    private readonly Func<string>? detailProvider;
    private readonly string unavailableDetail;

    [SupportedOSPlatform("windows")]
    public AddonTreeOperation(
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

    internal AddonTreeOperation(
        Func<Request, CancellationToken, ValueTask<AddonTreeSnapshot>> executor,
        bool isReady = true,
        string detail = "ready")
    {
        this.executor = executor ?? throw new ArgumentNullException(nameof(executor));
        isReadyProvider = () => isReady;
        detailProvider = () => string.IsNullOrWhiteSpace(detail) ? "ready" : detail;
        unavailableDetail = string.IsNullOrWhiteSpace(detail) ? "ready" : detail;
    }

    public string ReaderKey => "addon.tree";

    public bool IsReady => isReadyProvider?.Invoke() ?? false;

    public string Detail => detailProvider?.Invoke() ?? unavailableDetail;

    public ValueTask<AddonTreeSnapshot> ExecuteAsync(Request request, OperationContext context)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(context);

        return executor(request, context.CancellationToken);
    }

    [MemoryPackable]
    [ProtocolOperation("addon.tree")]
    [LegacyBridgeRequest("GetAddonTree")]
    public sealed partial class Request
    {
        [Option("addon", Description = "Addon name to inspect.")]
        public string AddonName { get; init; } = string.Empty;
    }

    public sealed class TextFormatter : IResultFormatter<AddonTreeSnapshot>
    {
        public string? FormatText(AddonTreeSnapshot result, OperationContext context)
        {
            ArgumentNullException.ThrowIfNull(result);
            ArgumentNullException.ThrowIfNull(context);

            return $"{result.AddonName} rootCount={result.Roots.Length.ToString(CultureInfo.InvariantCulture)}";
        }
    }

    [SupportedOSPlatform("windows")]
    private static Func<Request, CancellationToken, ValueTask<AddonTreeSnapshot>> CreateDalamudExecutor(
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
    private static unsafe AddonTreeSnapshot ReadCurrentCore(
        IClientState clientState,
        IGameGui gameGui,
        string addonName,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (!clientState.IsLoggedIn)
            throw new InvalidOperationException("Addon tree is not available because the local player is not logged in.");

        AtkUnitBasePtr addon = gameGui.GetAddonByName(addonName, 1);
        if (addon.IsNull || !addon.IsReady)
            throw new InvalidOperationException($"Addon '{addonName}' is not ready.");

        AtkUnitBase* addonStruct = gameGui.GetAddonByName<AtkUnitBase>(addonName, 1);
        if (addonStruct is null)
            throw new InvalidOperationException($"Addon '{addonName}' did not expose a native tree.");

        HashSet<nint> visited = new();
        List<AddonTreeNode> children = [];
        AppendSiblingSnapshots(addonStruct->RootNode, children, visited, 0);
        AddonTreeNode root = new(
            addon.Id,
            "addon",
            addon.IsVisible,
            addon.X,
            addon.Y,
            addon.Width,
            addon.Height,
            string.IsNullOrWhiteSpace(addon.Name) ? addonName : addon.Name,
            [.. children]);

        return new AddonTreeSnapshot(
            addonName,
            DateTimeOffset.UtcNow,
            [root]);
    }

    private static string NormalizeAddonName(string addonName)
    {
        return string.IsNullOrWhiteSpace(addonName)
            ? throw new ArgumentException("Addon name is required.", nameof(addonName))
            : addonName.Trim();
    }

    [SupportedOSPlatform("windows")]
    private static unsafe void AppendSiblingSnapshots(
        AtkResNode* node,
        List<AddonTreeNode> snapshots,
        HashSet<nint> visited,
        int depth)
    {
        const int maxDepth = 12;
        while (node is not null)
        {
            IntPtr pointer = (nint)node;
            if (!visited.Add(pointer))
                return;

            snapshots.Add(CreateNodeSnapshot(node, visited, depth, maxDepth));
            node = node->NextSiblingNode;
        }
    }

    [SupportedOSPlatform("windows")]
    private static unsafe AddonTreeNode CreateNodeSnapshot(
        AtkResNode* node,
        HashSet<nint> visited,
        int depth,
        int maxDepth)
    {
        List<AddonTreeNode> children = [];
        if (depth < maxDepth)
        {
            AppendSiblingSnapshots(node->ChildNode, children, visited, depth + 1);
            AppendComponentSnapshots(node, children, visited, depth + 1);
        }

        return new AddonTreeNode(
            (int)node->NodeId,
            node->GetNodeType().ToString(),
            node->IsVisible(),
            node->X,
            node->Y,
            node->Width,
            node->Height,
            BuildNodeText(node),
            [.. children]);
    }

    [SupportedOSPlatform("windows")]
    private static unsafe string? BuildNodeText(AtkResNode* node)
    {
        List<string> parts = [];
        string? nodeText = ReadNodeText(node);
        if (!string.IsNullOrWhiteSpace(nodeText))
            parts.Add(nodeText);

        string? componentText = ReadComponentText(node);
        if (!string.IsNullOrWhiteSpace(componentText))
            parts.Add(componentText);

        return parts.Count == 0 ? null : string.Join(" | ", parts);
    }

    [SupportedOSPlatform("windows")]
    private static unsafe string? ReadNodeText(AtkResNode* node)
    {
        AtkTextNode* textNode = node->GetAsAtkTextNode();
        if (textNode is null)
            return null;

        try
        {
            return FormatValue(textNode->NodeText.ToString());
        }
        catch (NotImplementedException)
        {
            return null;
        }
    }

    [SupportedOSPlatform("windows")]
    private static unsafe string? ReadComponentText(AtkResNode* node)
    {
        AtkComponentNode* componentNode = node->GetAsAtkComponentNode();
        if (componentNode is null)
            return null;

        AtkComponentBase* component = componentNode->GetComponent();
        if (component is null)
            return "component=null";

        return $"componentType={component->GetComponentType()}";
    }

    [SupportedOSPlatform("windows")]
    private static unsafe void AppendComponentSnapshots(
        AtkResNode* node,
        List<AddonTreeNode> children,
        HashSet<nint> visited,
        int depth)
    {
        AtkComponentNode* componentNode = node->GetAsAtkComponentNode();
        if (componentNode is null)
            return;

        AtkComponentBase* component = componentNode->GetComponent();
        if (component is null)
            return;

        foreach (Pointer<AtkResNode> uldNode in component->UldManager.Nodes)
        {
            AtkResNode* childNode = uldNode;
            if (childNode is null)
                continue;

            IntPtr pointer = (nint)childNode;
            if (!visited.Add(pointer))
                continue;

            children.Add(CreateNodeSnapshot(childNode, visited, depth, 12));
        }
    }

    private static string? FormatValue(object? value)
    {
        return value switch
        {
            null => null,
            string text when string.IsNullOrWhiteSpace(text) => null,
            string text => text,
            bool flag => flag ? "true" : "false",
            IFormattable formattable => formattable.ToString(null, CultureInfo.InvariantCulture),
            _ => value.ToString()
        };
    }
}

[MemoryPackable]
public sealed partial record AddonTreeNode(
    int NodeId,
    string NodeType,
    bool Visible,
    float X,
    float Y,
    float Width,
    float Height,
    string? Text,
    AddonTreeNode[] Children);

[MemoryPackable]
public sealed partial record AddonTreeSnapshot(
    string AddonName,
    DateTimeOffset CapturedAt,
    AddonTreeNode[] Roots);