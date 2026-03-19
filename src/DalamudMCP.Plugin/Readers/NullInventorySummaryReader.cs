using DalamudMCP.Application.Abstractions.Readers;
using DalamudMCP.Domain.Snapshots;

namespace DalamudMCP.Plugin.Readers;

public sealed class NullInventorySummaryReader : IInventorySummaryReader, IPluginReaderDiagnostics
{
    public string ComponentName => "inventory_summary";

    public bool IsReady => false;

    public string Status => "not_attached";

    public Task<InventorySummarySnapshot?> ReadCurrentAsync(CancellationToken cancellationToken) =>
        Task.FromResult<InventorySummarySnapshot?>(null);
}
