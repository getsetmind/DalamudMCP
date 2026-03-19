using System.Runtime.Versioning;
using Dalamud.Plugin.Services;
using DalamudMCP.Application.Abstractions.Readers;
using DalamudMCP.Domain.Snapshots;

namespace DalamudMCP.Plugin.Readers;

[SupportedOSPlatform("windows")]
public sealed class DalamudAddonTreeReader : IAddonTreeReader, IPluginReaderDiagnostics
{
    private readonly IClientState clientState;
    private readonly IFramework framework;
    private readonly IGameGui gameGui;

    public DalamudAddonTreeReader(
        IFramework framework,
        IClientState clientState,
        IGameGui gameGui)
    {
        ArgumentNullException.ThrowIfNull(framework);
        ArgumentNullException.ThrowIfNull(clientState);
        ArgumentNullException.ThrowIfNull(gameGui);
        this.framework = framework;
        this.clientState = clientState;
        this.gameGui = gameGui;
    }

    public string ComponentName => "addon_tree";

    public bool IsReady => clientState.IsLoggedIn;

    public string Status => IsReady ? "ready" : "not_logged_in";

    public Task<AddonTreeSnapshot?> ReadCurrentAsync(string addonName, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (!framework.IsInFrameworkUpdateThread)
        {
            return framework.RunOnFrameworkThread(() => ReadCurrentCore(addonName, cancellationToken));
        }

        return Task.FromResult(ReadCurrentCore(addonName, cancellationToken));
    }

    private AddonTreeSnapshot? ReadCurrentCore(string addonName, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(addonName);
        cancellationToken.ThrowIfCancellationRequested();
        if (!clientState.IsLoggedIn)
        {
            return null;
        }

        var addon = gameGui.GetAddonByName(addonName, 1);
        if (addon.IsNull || !addon.IsReady)
        {
            return null;
        }

        var root = PluginReaderValueFormatter.CreateAddonRootNode(
            nodeId: addon.Id,
            addonName: string.IsNullOrWhiteSpace(addon.Name) ? addonName : addon.Name,
            visible: addon.IsVisible,
            x: addon.X,
            y: addon.Y,
            width: addon.Width,
            height: addon.Height);

        return new AddonTreeSnapshot(
            AddonName: addonName,
            CapturedAt: DateTimeOffset.UtcNow,
            Roots: [root]);
    }
}
