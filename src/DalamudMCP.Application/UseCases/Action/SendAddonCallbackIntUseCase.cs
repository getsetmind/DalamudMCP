using DalamudMCP.Application.Abstractions.Actions;
using DalamudMCP.Application.Abstractions.Repositories;
using DalamudMCP.Application.Common;
using DalamudMCP.Domain.Actions;
using DalamudMCP.Domain.Registry;

namespace DalamudMCP.Application.UseCases.Action;

public sealed class SendAddonCallbackIntUseCase
{
    private const string ToolName = "send_addon_callback_int";

    private readonly IAddonCallbackController controller;
    private readonly ISettingsRepository settingsRepository;
    private readonly CapabilityRegistry capabilityRegistry;

    public SendAddonCallbackIntUseCase(
        IAddonCallbackController controller,
        ISettingsRepository settingsRepository,
        CapabilityRegistry capabilityRegistry)
    {
        this.controller = controller;
        this.settingsRepository = settingsRepository;
        this.capabilityRegistry = capabilityRegistry;
    }

    public async Task<QueryResult<AddonCallbackIntResult>> ExecuteAsync(
        string addonName,
        int value,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(addonName);

        if (!capabilityRegistry.TryGetToolBinding(ToolName, out var binding) || binding is null)
        {
            return QueryResults.Denied<AddonCallbackIntResult>("capability_missing");
        }

        if (!capabilityRegistry.TryGetCapability(binding.CapabilityId.Value, out var capability) || capability is null || capability.Denied)
        {
            return QueryResults.Denied<AddonCallbackIntResult>("capability_denied");
        }

        var policy = await settingsRepository.LoadAsync(cancellationToken).ConfigureAwait(false);
        if (!policy.CanExposeTool(capability, ToolName))
        {
            return QueryResults.Disabled<AddonCallbackIntResult>("tool_disabled");
        }

        if (!policy.EnabledAddons.Contains(addonName, StringComparer.OrdinalIgnoreCase))
        {
            return QueryResults.Disabled<AddonCallbackIntResult>("addon_disabled");
        }

        var result = await controller.SendCallbackIntAsync(addonName.Trim(), value, cancellationToken).ConfigureAwait(false);
        return QueryResults.Success(result, DateTimeOffset.UtcNow, 0);
    }
}
