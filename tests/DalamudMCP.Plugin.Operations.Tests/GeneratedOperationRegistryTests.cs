using DalamudMCP.Framework;
using DalamudMCP.Framework.Generated;

namespace DalamudMCP.Plugin.Operations.Tests;

public sealed class GeneratedOperationRegistryTests
{
    [Fact]
    public void GeneratedOperationRegistry_exposes_session_status_operation()
    {
        bool found = GeneratedOperationRegistry.TryFind("session.status", out OperationDescriptor? descriptor);

        Assert.True(found);
        Assert.NotNull(descriptor);
        Assert.Equal(typeof(SessionStatusOperation), descriptor!.DeclaringType);
        Assert.Equal("ExecuteAsync", descriptor.MethodName);
        Assert.Equal(typeof(SessionStatusSnapshot), descriptor.ResultType);
        Assert.Equal(typeof(SessionStatusOperation.Request), descriptor.RequestType);
        Assert.Equal(["session", "status"], descriptor.CliCommandPath);
        Assert.Equal("get_session_status", descriptor.McpToolName);
        Assert.Empty(descriptor.Parameters);
    }

    [Fact]
    public void GeneratedOperationRegistry_exposes_player_context_operation()
    {
        bool found = GeneratedOperationRegistry.TryFind("player.context", out OperationDescriptor? descriptor);

        Assert.True(found);
        Assert.NotNull(descriptor);
        Assert.Equal(typeof(PlayerContextOperation), descriptor!.DeclaringType);
        Assert.Equal("ExecuteAsync", descriptor.MethodName);
        Assert.Equal(typeof(PlayerContextSnapshot), descriptor.ResultType);
        Assert.Equal(typeof(PlayerContextOperation.Request), descriptor.RequestType);
        Assert.Equal(["player", "context"], descriptor.CliCommandPath);
        Assert.Equal("get_player_context", descriptor.McpToolName);
        Assert.Empty(descriptor.Parameters);
    }

    [Fact]
    public void GeneratedOperationRegistry_exposes_duty_context_operation()
    {
        bool found = GeneratedOperationRegistry.TryFind("duty.context", out OperationDescriptor? descriptor);

        Assert.True(found);
        Assert.NotNull(descriptor);
        Assert.Equal(typeof(DutyContextOperation), descriptor!.DeclaringType);
        Assert.Equal("ExecuteAsync", descriptor.MethodName);
        Assert.Equal(typeof(DutyContextSnapshot), descriptor.ResultType);
        Assert.Equal(typeof(DutyContextOperation.Request), descriptor.RequestType);
        Assert.Equal(["duty", "context"], descriptor.CliCommandPath);
        Assert.Equal("get_duty_context", descriptor.McpToolName);
        Assert.Empty(descriptor.Parameters);
    }

    [Fact]
    public void GeneratedOperationRegistry_exposes_inventory_summary_operation()
    {
        bool found = GeneratedOperationRegistry.TryFind("inventory.summary", out OperationDescriptor? descriptor);

        Assert.True(found);
        Assert.NotNull(descriptor);
        Assert.Equal(typeof(InventorySummaryOperation), descriptor!.DeclaringType);
        Assert.Equal("ExecuteAsync", descriptor.MethodName);
        Assert.Equal(typeof(InventorySummarySnapshot), descriptor.ResultType);
        Assert.Equal(typeof(InventorySummaryOperation.Request), descriptor.RequestType);
        Assert.Equal(["inventory", "summary"], descriptor.CliCommandPath);
        Assert.Equal("get_inventory_summary", descriptor.McpToolName);
        Assert.Empty(descriptor.Parameters);
    }

    [Fact]
    public void GeneratedOperationRegistry_exposes_nearby_interactables_operation()
    {
        bool found = GeneratedOperationRegistry.TryFind("nearby.interactables", out OperationDescriptor? descriptor);

        Assert.True(found);
        Assert.NotNull(descriptor);
        Assert.Equal(typeof(NearbyInteractablesOperation), descriptor!.DeclaringType);
        Assert.Equal(typeof(NearbyInteractablesSnapshot), descriptor.ResultType);
        Assert.Equal(typeof(NearbyInteractablesOperation.Request), descriptor.RequestType);
        Assert.Equal(["nearby", "interactables"], descriptor.CliCommandPath);
        Assert.Equal("get_nearby_interactables", descriptor.McpToolName);
        Assert.Equal(3, descriptor.Parameters.Count);
    }

    [Fact]
    public void GeneratedOperationRegistry_exposes_fate_context_operation()
    {
        bool found = GeneratedOperationRegistry.TryFind("fate.context", out OperationDescriptor? descriptor);

        Assert.True(found);
        Assert.NotNull(descriptor);
        Assert.Equal(typeof(FateContextOperation), descriptor!.DeclaringType);
        Assert.Equal(typeof(FateContextSnapshot), descriptor.ResultType);
        Assert.Equal(typeof(FateContextOperation.Request), descriptor.RequestType);
        Assert.Equal(["fate", "context"], descriptor.CliCommandPath);
        Assert.Equal("get_fate_context", descriptor.McpToolName);
        Assert.Equal(2, descriptor.Parameters.Count);
    }

    [Fact]
    public void GeneratedOperationRegistry_exposes_quest_status_operation()
    {
        bool found = GeneratedOperationRegistry.TryFind("quest.status", out OperationDescriptor? descriptor);

        Assert.True(found);
        Assert.NotNull(descriptor);
        Assert.Equal(typeof(QuestStatusOperation), descriptor!.DeclaringType);
        Assert.Equal(typeof(QuestStatusSnapshot), descriptor.ResultType);
        Assert.Equal(typeof(QuestStatusOperation.Request), descriptor.RequestType);
        Assert.Equal(["quest", "status"], descriptor.CliCommandPath);
        Assert.Equal("get_quest_status", descriptor.McpToolName);
        Assert.Equal(3, descriptor.Parameters.Count);
    }

    [Fact]
    public void GeneratedOperationRegistry_exposes_available_quests_operation()
    {
        bool found = GeneratedOperationRegistry.TryFind("quest.available", out OperationDescriptor? descriptor);

        Assert.True(found);
        Assert.NotNull(descriptor);
        Assert.Equal(typeof(AvailableQuestsOperation), descriptor!.DeclaringType);
        Assert.Equal(typeof(AvailableQuestsSnapshot), descriptor.ResultType);
        Assert.Equal(typeof(AvailableQuestsOperation.Request), descriptor.RequestType);
        Assert.Equal(["quest", "available"], descriptor.CliCommandPath);
        Assert.Equal("get_available_quests", descriptor.McpToolName);
        Assert.Equal(2, descriptor.Parameters.Count);
    }

    [Fact]
    public void GeneratedOperationRegistry_exposes_current_quest_objective_operation()
    {
        bool found = GeneratedOperationRegistry.TryFind("quest.current-objective", out OperationDescriptor? descriptor);

        Assert.True(found);
        Assert.NotNull(descriptor);
        Assert.Equal(typeof(CurrentQuestObjectiveOperation), descriptor!.DeclaringType);
        Assert.Equal(typeof(CurrentQuestObjectiveSnapshot), descriptor.ResultType);
        Assert.Equal(typeof(CurrentQuestObjectiveOperation.Request), descriptor.RequestType);
        Assert.Equal(["quest", "current-objective"], descriptor.CliCommandPath);
        Assert.Equal("get_current_quest_objective", descriptor.McpToolName);
        Assert.Empty(descriptor.Parameters);
    }

    [Fact]
    public void GeneratedOperationRegistry_exposes_addon_list_operation()
    {
        bool found = GeneratedOperationRegistry.TryFind("addon.list", out OperationDescriptor? descriptor);

        Assert.True(found);
        Assert.NotNull(descriptor);
        Assert.Equal(typeof(AddonListOperation), descriptor!.DeclaringType);
        Assert.Equal(typeof(AddonSummary[]), descriptor.ResultType);
        Assert.Equal(typeof(AddonListOperation.Request), descriptor.RequestType);
        Assert.Equal(["addon", "list"], descriptor.CliCommandPath);
        Assert.Equal("get_addon_list", descriptor.McpToolName);
        Assert.Empty(descriptor.Parameters);
    }

    [Fact]
    public void GeneratedOperationRegistry_exposes_addon_strings_operation()
    {
        bool found = GeneratedOperationRegistry.TryFind("addon.strings", out OperationDescriptor? descriptor);

        Assert.True(found);
        Assert.NotNull(descriptor);
        Assert.Equal(typeof(AddonStringsOperation), descriptor!.DeclaringType);
        Assert.Equal(typeof(AddonStringsSnapshot), descriptor.ResultType);
        Assert.Equal(typeof(AddonStringsOperation.Request), descriptor.RequestType);
        Assert.Equal(["addon", "strings"], descriptor.CliCommandPath);
        Assert.Equal("get_addon_strings", descriptor.McpToolName);
        Assert.Single(descriptor.Parameters);
    }

    [Fact]
    public void GeneratedOperationRegistry_exposes_game_screenshot_operation()
    {
        bool found = GeneratedOperationRegistry.TryFind("game.screenshot", out OperationDescriptor? descriptor);

        Assert.True(found);
        Assert.NotNull(descriptor);
        Assert.Equal(typeof(GameScreenshotOperation), descriptor!.DeclaringType);
        Assert.Equal(typeof(GameScreenshotSnapshot), descriptor.ResultType);
        Assert.Equal(typeof(GameScreenshotOperation.Request), descriptor.RequestType);
        Assert.Equal(["game", "screenshot"], descriptor.CliCommandPath);
        Assert.Equal("capture_game_screenshot", descriptor.McpToolName);
        Assert.Single(descriptor.Parameters);
    }

    [Fact]
    public void GeneratedOperationRegistry_exposes_addon_tree_operation()
    {
        bool found = GeneratedOperationRegistry.TryFind("addon.tree", out OperationDescriptor? descriptor);

        Assert.True(found);
        Assert.NotNull(descriptor);
        Assert.Equal(typeof(AddonTreeOperation), descriptor!.DeclaringType);
        Assert.Equal(typeof(AddonTreeSnapshot), descriptor.ResultType);
        Assert.Equal(typeof(AddonTreeOperation.Request), descriptor.RequestType);
        Assert.Equal(["addon", "tree"], descriptor.CliCommandPath);
        Assert.Equal("get_addon_tree", descriptor.McpToolName);
        Assert.Single(descriptor.Parameters);
    }

    [Fact]
    public void GeneratedOperationRegistry_exposes_unsafe_invoke_plugin_ipc_operation()
    {
        bool found = GeneratedOperationRegistry.TryFind("unsafe.invoke.plugin-ipc", out OperationDescriptor? descriptor);

        Assert.True(found);
        Assert.NotNull(descriptor);
        Assert.Equal(typeof(UnsafeInvokePluginIpcOperation), descriptor!.DeclaringType);
        Assert.Equal(typeof(UnsafeInvokePluginIpcResult), descriptor.ResultType);
        Assert.Equal(typeof(UnsafeInvokePluginIpcOperation.Request), descriptor.RequestType);
        Assert.Equal(["unsafe", "invoke", "plugin-ipc"], descriptor.CliCommandPath);
        Assert.Equal("unsafe_invoke_plugin_ipc", descriptor.McpToolName);
        Assert.Equal(5, descriptor.Parameters.Count);
    }
}
