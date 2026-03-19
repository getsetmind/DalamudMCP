using System.Runtime.Versioning;
using Dalamud.Plugin.Services;
using DalamudMCP.Application.Abstractions.Actions;
using DalamudMCP.Domain.Actions;
using FFXIVClientStructs.FFXIV.Component.GUI;
using AtkValueType = FFXIVClientStructs.FFXIV.Component.GUI.ValueType;

namespace DalamudMCP.Plugin.Readers;

[SupportedOSPlatform("windows")]
public sealed unsafe class DalamudAddonCallbackController : IAddonCallbackController
{
    private readonly IClientState clientState;
    private readonly IFramework framework;
    private readonly IGameGui gameGui;

    public DalamudAddonCallbackController(
        IFramework framework,
        IClientState clientState,
        IGameGui gameGui)
    {
        ArgumentNullException.ThrowIfNull(framework);
        ArgumentNullException.ThrowIfNull(clientState);
        ArgumentNullException.ThrowIfNull(gameGui);
        this.framework = framework;
        this.clientState = clientState;
        this.gameGui = gameGui;
    }

    public Task<AddonCallbackIntResult> SendCallbackIntAsync(string addonName, int value, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (!framework.IsInFrameworkUpdateThread)
        {
            return framework.RunOnFrameworkThread(() => SendCore(addonName, value, cancellationToken));
        }

        return Task.FromResult(SendCore(addonName, value, cancellationToken));
    }

    public Task<AddonCallbackValuesResult> SendCallbackValuesAsync(string addonName, IReadOnlyList<int> values, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(values);
        cancellationToken.ThrowIfCancellationRequested();
        if (!framework.IsInFrameworkUpdateThread)
        {
            return framework.RunOnFrameworkThread(() => SendValuesCore(addonName, values, cancellationToken));
        }

        return Task.FromResult(SendValuesCore(addonName, values, cancellationToken));
    }

    private AddonCallbackIntResult SendCore(string addonName, int value, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (!clientState.IsLoggedIn)
        {
            return new AddonCallbackIntResult(addonName, value, false, "not_logged_in", "Player is not logged in.");
        }

        var addon = gameGui.GetAddonByName(addonName, 1);
        if (addon.IsNull || !addon.IsReady)
        {
            return new AddonCallbackIntResult(addonName, value, false, "addon_not_ready", $"{addonName} is not ready.");
        }

        var addonStruct = gameGui.GetAddonByName<AtkUnitBase>(addonName, 1);
        if (addonStruct == null)
        {
            return new AddonCallbackIntResult(addonName, value, false, "addon_struct_unavailable", $"{addonName} does not expose a native addon pointer.");
        }

        addonStruct->FireCallbackInt(value);
        return new AddonCallbackIntResult(addonName, value, true, null, $"Sent callback int {value} to {addonName}.");
    }

    private AddonCallbackValuesResult SendValuesCore(string addonName, IReadOnlyList<int> values, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (!clientState.IsLoggedIn)
        {
            return new AddonCallbackValuesResult(addonName, [.. values], false, "not_logged_in", "Player is not logged in.");
        }

        if (values.Count == 0)
        {
            return new AddonCallbackValuesResult(addonName, [], false, "no_values", "At least one callback value is required.");
        }

        var addon = gameGui.GetAddonByName(addonName, 1);
        if (addon.IsNull || !addon.IsReady)
        {
            return new AddonCallbackValuesResult(addonName, [.. values], false, "addon_not_ready", $"{addonName} is not ready.");
        }

        var addonStruct = gameGui.GetAddonByName<AtkUnitBase>(addonName, 1);
        if (addonStruct == null)
        {
            return new AddonCallbackValuesResult(addonName, [.. values], false, "addon_struct_unavailable", $"{addonName} does not expose a native addon pointer.");
        }

        var copiedValues = values.ToArray();
        var atkValues = stackalloc AtkValue[copiedValues.Length];
        for (var index = 0; index < copiedValues.Length; index++)
        {
            atkValues[index] = new AtkValue
            {
                Type = AtkValueType.Int,
                Int = copiedValues[index],
            };
        }

        addonStruct->FireCallback((uint)copiedValues.Length, atkValues, false);
        return new AddonCallbackValuesResult(addonName, copiedValues, true, null, $"Sent callback values [{string.Join(", ", copiedValues)}] to {addonName}.");
    }
}
