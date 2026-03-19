using DalamudMCP.Application.Abstractions.Actions;
using DalamudMCP.Domain.Actions;

namespace DalamudMCP.Application.Tests.Fakes;

internal sealed class FakeTargetObjectController : ITargetObjectController
{
    public string? LastGameObjectId { get; private set; }

    public TargetObjectResult Result { get; set; } = new(
        "0x0",
        Succeeded: false,
        Reason: "not_configured",
        TargetedGameObjectId: null,
        TargetName: null,
        ObjectKind: null,
        SummaryText: "No target configured.");

    public Task<TargetObjectResult> TargetAsync(string gameObjectId, CancellationToken cancellationToken)
    {
        LastGameObjectId = gameObjectId;
        return Task.FromResult(Result);
    }
}
