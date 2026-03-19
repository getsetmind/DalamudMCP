using DalamudMCP.Application.Abstractions.Readers;
using DalamudMCP.Domain.Snapshots;

namespace DalamudMCP.Application.Tests.Fakes;

internal sealed class FakeDutyContextReader : IDutyContextReader
{
    public DutyContextSnapshot? Snapshot { get; set; }

    public Task<DutyContextSnapshot?> ReadCurrentAsync(CancellationToken cancellationToken) =>
        Task.FromResult(Snapshot);
}
