using DalamudMCP.Application.Abstractions.Actions;
using DalamudMCP.Application.Abstractions.Repositories;
using DalamudMCP.Application.Common;
using DalamudMCP.Domain.Actions;
using DalamudMCP.Domain.Registry;

namespace DalamudMCP.Application.UseCases.Action;

public sealed class SendAddonCallbackValuesUseCase
{
    private const string ToolName = "send_addon_callback_values";

    private readonly IAddonCallbackController controller;
    private readonly ISettingsRepository settingsRepository;
    private readonly CapabilityRegistry capabilityRegistry;

    public SendAddonCallbackValuesUseCase(
        IAddonCallbackController controller,
        ISettingsRepository settingsRepository,
        CapabilityRegistry capabilityRegistry)
    {
        this.controller = controller;
        this.settingsRepository = settingsRepository;
        this.capabilityRegistry = capabilityRegistry;
    }

    public async Task<QueryResult<AddonCallbackValuesResult>> ExecuteAsync(
        string addonName,
        IReadOnlyList<int> values,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(addonName);
        ArgumentNullException.ThrowIfNull(values);
        if (values.Count == 0)
        {
            throw new ArgumentException("At least one callback value is required.", nameof(values));
        }

        if (!capabilityRegistry.TryGetToolBinding(ToolName, out var binding) || binding is null)
        {
            return QueryResults.Denied<AddonCallbackValuesResult>("capability_missing");
        }

        if (!capabilityRegistry.TryGetCapability(binding.CapabilityId.Value, out var capability) || capability is null || capability.Denied)
        {
            return QueryResults.Denied<AddonCallbackValuesResult>("capability_denied");
        }

        var policy = await settingsRepository.LoadAsync(cancellationToken).ConfigureAwait(false);
        if (!policy.CanExposeTool(capability, ToolName))
        {
            return QueryResults.Disabled<AddonCallbackValuesResult>("tool_disabled");
        }

        if (!policy.EnabledAddons.Contains(addonName, StringComparer.OrdinalIgnoreCase))
        {
            return QueryResults.Disabled<AddonCallbackValuesResult>("addon_disabled");
        }

        var result = await controller.SendCallbackValuesAsync(addonName.Trim(), values, cancellationToken).ConfigureAwait(false);
        return QueryResults.Success(result, DateTimeOffset.UtcNow, 0);
    }
}
