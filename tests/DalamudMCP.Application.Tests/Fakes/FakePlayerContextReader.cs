using DalamudMCP.Application.Abstractions.Readers;
using DalamudMCP.Domain.Snapshots;

namespace DalamudMCP.Application.Tests.Fakes;

internal sealed class FakePlayerContextReader : IPlayerContextReader
{
    public PlayerContextSnapshot? Snapshot { get; set; }

    public Task<PlayerContextSnapshot?> ReadCurrentAsync(CancellationToken cancellationToken) =>
        Task.FromResult(Snapshot);
}
