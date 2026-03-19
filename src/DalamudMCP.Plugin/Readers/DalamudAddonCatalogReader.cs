using System.Runtime.Versioning;
using Dalamud.Plugin.Services;
using DalamudMCP.Application.Abstractions.Readers;
using DalamudMCP.Domain.Registry;
using DalamudMCP.Domain.Snapshots;

namespace DalamudMCP.Plugin.Readers;

[SupportedOSPlatform("windows")]
public sealed class DalamudAddonCatalogReader : IAddonCatalogReader, IPluginReaderDiagnostics
{
    private readonly IClientState clientState;
    private readonly IFramework framework;
    private readonly IGameGui gameGui;
    private readonly IReadOnlyList<AddonMetadata> knownAddons;

    public DalamudAddonCatalogReader(
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
        this.knownAddons = KnownCapabilityRegistry.CreateDefault().Addons;
    }

    public string ComponentName => "addon_catalog";

    public bool IsReady => clientState.IsLoggedIn;

    public string Status => IsReady ? "ready" : "not_logged_in";

    public Task<IReadOnlyList<AddonSummary>> ReadCurrentAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (!framework.IsInFrameworkUpdateThread)
        {
            return framework.RunOnFrameworkThread<IReadOnlyList<AddonSummary>>(() => ReadCurrentCore(cancellationToken));
        }

        return Task.FromResult<IReadOnlyList<AddonSummary>>(ReadCurrentCore(cancellationToken));
    }

    private AddonSummary[] ReadCurrentCore(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (!clientState.IsLoggedIn)
        {
            return [];
        }

        var capturedAt = DateTimeOffset.UtcNow;
        return knownAddons
            .Select(metadata => CreateSummary(metadata, capturedAt))
            .ToArray();
    }

    private AddonSummary CreateSummary(AddonMetadata metadata, DateTimeOffset capturedAt)
    {
        var addon = gameGui.GetAddonByName(metadata.AddonName, 1);
        var isReady = !addon.IsNull && addon.IsReady;
        var isVisible = !addon.IsNull && addon.IsVisible;
        var summary = PluginReaderValueFormatter.FormatAddonSummary(metadata.DisplayName, isReady, isVisible);

        return new AddonSummary(
            metadata.AddonName,
            isReady,
            isVisible,
            capturedAt,
            summary);
    }
}
