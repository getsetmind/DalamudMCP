using DalamudMCP.Application.Abstractions.Readers;
using DalamudMCP.Domain.Session;

namespace DalamudMCP.Application.Tests.Fakes;

public sealed class FakeSessionStateReader : ISessionStateReader
{
    public SessionState Snapshot { get; set; } = new(
        new DateTimeOffset(2026, 3, 20, 0, 0, 0, TimeSpan.Zero),
        "DalamudMCP.TestPipe",
        true,
        [new SessionComponentState("player_context", false, "not_attached")],
        "0/1 readers ready; bridge server running.");

    public Task<SessionState> ReadCurrentAsync(CancellationToken cancellationToken) =>
        Task.FromResult(Snapshot);
}
