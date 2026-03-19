using DalamudMCP.Application.Abstractions.Readers;
using DalamudMCP.Domain.Snapshots;

namespace DalamudMCP.Plugin.Readers;

public sealed class EmptyAddonCatalogReader : IAddonCatalogReader, IPluginReaderDiagnostics
{
    public string ComponentName => "addon_catalog";

    public bool IsReady => false;

    public string Status => "not_attached";

    public Task<IReadOnlyList<AddonSummary>> ReadCurrentAsync(CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<AddonSummary>>([]);
}
