using DalamudMCP.Domain.Actions;

namespace DalamudMCP.Application.Abstractions.Actions;

public interface IAddonCallbackController
{
    public Task<AddonCallbackIntResult> SendCallbackIntAsync(string addonName, int value, CancellationToken cancellationToken);

    public Task<AddonCallbackValuesResult> SendCallbackValuesAsync(string addonName, IReadOnlyList<int> values, CancellationToken cancellationToken);
}
