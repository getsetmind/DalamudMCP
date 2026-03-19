using DalamudMCP.Application.Abstractions.Actions;
using DalamudMCP.Domain.Actions;

namespace DalamudMCP.Plugin.Readers;

public sealed class NullAddonCallbackController : IAddonCallbackController
{
    public Task<AddonCallbackIntResult> SendCallbackIntAsync(string addonName, int value, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(
            new AddonCallbackIntResult(
                addonName,
                value,
                Succeeded: false,
                Reason: "addon_callback_unavailable",
                SummaryText: $"Addon callback is not available for {addonName}."));
    }

    public Task<AddonCallbackValuesResult> SendCallbackValuesAsync(string addonName, IReadOnlyList<int> values, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(
            new AddonCallbackValuesResult(
                addonName,
                [.. values],
                Succeeded: false,
                Reason: "addon_callback_unavailable",
                SummaryText: $"Addon callback is not available for {addonName}."));
    }
}
