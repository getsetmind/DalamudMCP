using DalamudMCP.Application.Abstractions.Readers;
using DalamudMCP.Domain.Snapshots;

namespace DalamudMCP.Plugin.Readers;

public sealed class NullPlayerContextReader : IPlayerContextReader, IPluginReaderDiagnostics
{
    public string ComponentName => "player_context";

    public bool IsReady => false;

    public string Status => "not_attached";

    public Task<PlayerContextSnapshot?> ReadCurrentAsync(CancellationToken cancellationToken) =>
        Task.FromResult<PlayerContextSnapshot?>(null);
}
