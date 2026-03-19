using DalamudMCP.Application.Abstractions.Actions;
using DalamudMCP.Domain.Actions;

namespace DalamudMCP.Application.Tests.Fakes;

public sealed class FakeAddonCallbackController : IAddonCallbackController
{
    public string? LastAddonName { get; private set; }

    public int? LastIntValue { get; private set; }

    public IReadOnlyList<int>? LastValues { get; private set; }

    public AddonCallbackIntResult IntResult { get; set; } =
        new("TestAddon", 0, true, null, "ok");

    public AddonCallbackValuesResult ValuesResult { get; set; } =
        new("TestAddon", [1], true, null, "ok");

    public Task<AddonCallbackIntResult> SendCallbackIntAsync(string addonName, int value, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        LastAddonName = addonName;
        LastIntValue = value;
        return Task.FromResult(IntResult);
    }

    public Task<AddonCallbackValuesResult> SendCallbackValuesAsync(string addonName, IReadOnlyList<int> values, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        LastAddonName = addonName;
        LastValues = [.. values];
        return Task.FromResult(ValuesResult);
    }
}
