using DalamudMCP.Application.Abstractions.Readers;
using DalamudMCP.Domain.Snapshots;

namespace DalamudMCP.Plugin.Readers;

public sealed class NullAddonTreeReader : IAddonTreeReader, IPluginReaderDiagnostics
{
    public string ComponentName => "addon_tree";

    public bool IsReady => false;

    public string Status => "not_attached";

    public Task<AddonTreeSnapshot?> ReadCurrentAsync(string addonName, CancellationToken cancellationToken) =>
        Task.FromResult<AddonTreeSnapshot?>(null);
}
