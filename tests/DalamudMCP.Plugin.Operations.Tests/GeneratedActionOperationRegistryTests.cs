using Manifold;
using Manifold.Generated;

namespace DalamudMCP.Plugin.Operations.Tests;

public sealed class GeneratedActionOperationRegistryTests
{
    [Theory]
    [InlineData("target.object", typeof(TargetObjectOperation), typeof(TargetObjectResult), typeof(TargetObjectOperation.Request), "target_object")]
    [InlineData("interact.with.target", typeof(InteractWithTargetOperation), typeof(InteractWithTargetResult), typeof(InteractWithTargetOperation.Request), "interact_with_target")]
    [InlineData("move.to.entity", typeof(MoveToEntityOperation), typeof(MoveToEntityResult), typeof(MoveToEntityOperation.Request), "move_to_entity")]
    [InlineData("move.to.nearby.interactable", typeof(MoveToNearbyInteractableOperation), typeof(MoveToEntityResult), typeof(MoveToNearbyInteractableOperation.Request), "move_to_nearby_interactable")]
    [InlineData("teleport.to.aetheryte", typeof(TeleportToAetheryteOperation), typeof(TeleportToAetheryteResult), typeof(TeleportToAetheryteOperation.Request), "teleport_to_aetheryte")]
    [InlineData("duty.action", typeof(DutyActionOperation), typeof(DutyActionResult), typeof(DutyActionOperation.Request), "use_duty_action")]
    [InlineData("addon.callback.values", typeof(AddonCallbackValuesOperation), typeof(AddonCallbackValuesResult), typeof(AddonCallbackValuesOperation.Request), "send_addon_callback_values")]
    [InlineData("addon.input", typeof(AddonInputOperation), typeof(AddonInputResult), typeof(AddonInputOperation.Request), "send_addon_input")]
    [InlineData("addon.event", typeof(AddonEventOperation), typeof(AddonEventResult), typeof(AddonEventOperation.Request), "send_addon_event")]
    [InlineData("addon.select.menu-item", typeof(AddonSelectMenuItemOperation), typeof(AddonSelectMenuItemResult), typeof(AddonSelectMenuItemOperation.Request), "select_addon_menu_item")]
    public void GeneratedOperationRegistry_exposes_action_operation(
        string operationId,
        Type declaringType,
        Type resultType,
        Type requestType,
        string mcpToolName)
    {
        bool found = GeneratedOperationRegistry.TryFind(operationId, out OperationDescriptor? descriptor);

        Assert.True(found);
        Assert.NotNull(descriptor);
        Assert.Equal(declaringType, descriptor!.DeclaringType);
        Assert.Equal(resultType, descriptor.ResultType);
        Assert.Equal(requestType, descriptor.RequestType);
        Assert.Equal(mcpToolName, descriptor.McpToolName);
    }
}