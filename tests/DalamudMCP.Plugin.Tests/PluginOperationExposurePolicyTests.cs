using DalamudMCP.Plugin.Configuration;
using DalamudMCP.Plugin.Hosting;
using Manifold;

namespace DalamudMCP.Plugin.Tests;

public sealed class PluginOperationExposurePolicyTests
{
    public static TheoryData<string> UnsafeOperationIds => new()
    {
        "unsafe.invoke.plugin-ipc",
        "plugin.ipc",
        "plugin.reload",
        "plugin.lifecycle.control",
        "plugin.package.control",
        "game.screenshot",
        "events.configure",
        "command.slash",
        "plugin.data.subscribe",
        "plugin.data.poll",
        "plugin.data.unsubscribe"
    };

    [Theory]
    [MemberData(nameof(UnsafeOperationIds))]
    public void IsUnsafeOperation_marks_fork_integration_operations_as_unsafe(string operationId)
    {
        Assert.True(PluginOperationExposurePolicy.IsUnsafeOperation(operationId));
    }

    [Fact]
    public void Explicit_capability_deny_overrides_legacy_action_toggle()
    {
        PluginUiConfiguration configuration = new() { EnableActionOperations = true };
        configuration.CapabilityPolicies["game.action.execute"] = new CapabilityPolicyConfiguration
        {
            Access = CapabilityAccessMode.Deny,
        };
        OperationDescriptor operation = CreateDescriptor("game.action.execute");

        Assert.False(PluginOperationExposurePolicy.IsVisible(operation, configuration));
        Assert.False(PluginOperationExposurePolicy.Authorize(operation, configuration, null).IsAllowed);
    }

    [Fact]
    public void Action_allowlist_is_enforced_for_direct_dispatch_authorization()
    {
        PluginUiConfiguration configuration = new();
        configuration.CapabilityPolicies["game.action.execute"] = new CapabilityPolicyConfiguration
        {
            Access = CapabilityAccessMode.Allow,
            AllowedActionTypes = ["Action"],
            AllowedActionIds = [42],
        };
        OperationDescriptor operation = CreateDescriptor("game.action.execute");

        CapabilityAuthorizationDecision allowed = PluginOperationExposurePolicy.Authorize(
            operation,
            configuration,
            new TestActionRequest("Action", 42));
        CapabilityAuthorizationDecision denied = PluginOperationExposurePolicy.Authorize(
            operation,
            configuration,
            new TestActionRequest("Item", 43));
        CapabilityAuthorizationDecision missingType = PluginOperationExposurePolicy.Authorize(
            operation,
            configuration,
            new TestActionRequest(null, 42));

        Assert.True(allowed.IsAllowed);
        Assert.False(denied.IsAllowed);
        Assert.False(missingType.IsAllowed);
    }

    [Fact]
    public void Plugin_target_allowlist_is_enforced()
    {
        PluginUiConfiguration configuration = new();
        configuration.CapabilityPolicies["plugin.lifecycle"] = new CapabilityPolicyConfiguration
        {
            Access = CapabilityAccessMode.Allow,
            AllowedPluginNames = ["AllowedPlugin"],
        };
        OperationDescriptor operation = CreateDescriptor("plugin.lifecycle.control");

        Assert.True(PluginOperationExposurePolicy.Authorize(
            operation,
            configuration,
            new TestPluginRequest("AllowedPlugin", "reload")).IsAllowed);
        Assert.False(PluginOperationExposurePolicy.Authorize(
            operation,
            configuration,
            new TestPluginRequest("OtherPlugin", "reload")).IsAllowed);
    }

    [Fact]
    public void Target_allowlist_recognizes_legacy_game_object_parameter_names()
    {
        PluginUiConfiguration configuration = new();
        configuration.CapabilityPolicies["game.action.execute"] = new CapabilityPolicyConfiguration
        {
            Access = CapabilityAccessMode.Allow,
            AllowedTargetObjectIds = ["0x42"],
        };
        OperationDescriptor operation = CreateDescriptor("target.object");

        Assert.True(PluginOperationExposurePolicy.Authorize(
            operation,
            configuration,
            new TestTargetRequest("0x42")).IsAllowed);
        Assert.False(PluginOperationExposurePolicy.Authorize(
            operation,
            configuration,
            new TestTargetRequest("0x43")).IsAllowed);
    }

    [Fact]
    public void Confirm_capability_remains_visible_but_requires_confirmation()
    {
        PluginUiConfiguration configuration = new();
        configuration.CapabilityPolicies["command.execute"] = new CapabilityPolicyConfiguration
        {
            Access = CapabilityAccessMode.Confirm,
        };
        OperationDescriptor operation = CreateDescriptor("command.slash");

        CapabilityAuthorizationDecision decision = PluginOperationExposurePolicy.Authorize(operation, configuration, null);

        Assert.True(PluginOperationExposurePolicy.IsVisible(operation, configuration));
        Assert.True(decision.RequiresConfirmation);
        Assert.False(decision.IsAllowed);
    }

    [Fact]
    public void Confirm_capability_validates_target_constraints_before_queuing_approval()
    {
        PluginUiConfiguration configuration = new();
        configuration.CapabilityPolicies["plugin.lifecycle"] = new CapabilityPolicyConfiguration
        {
            Access = CapabilityAccessMode.Confirm,
            AllowedPluginNames = ["AllowedPlugin"],
            MaximumCallsPerMinute = 3,
        };
        OperationDescriptor operation = CreateDescriptor("plugin.lifecycle.control");

        CapabilityAuthorizationDecision allowedTarget = PluginOperationExposurePolicy.Authorize(
            operation,
            configuration,
            new TestPluginRequest("AllowedPlugin", "reload"));
        CapabilityAuthorizationDecision deniedTarget = PluginOperationExposurePolicy.Authorize(
            operation,
            configuration,
            new TestPluginRequest("OtherPlugin", "reload"));

        Assert.True(allowedTarget.RequiresConfirmation);
        Assert.Equal(3, allowedTarget.MaximumCallsPerMinute);
        Assert.False(deniedTarget.IsAllowed);
        Assert.False(deniedTarget.RequiresConfirmation);
        Assert.Contains("outside the allowed targets", deniedTarget.Reason, StringComparison.Ordinal);
    }

    [Fact]
    public void Result_limit_and_rate_limit_are_enforced_by_capability_policy()
    {
        PluginUiConfiguration configuration = new();
        configuration.CapabilityPolicies["game.data.read"] = new CapabilityPolicyConfiguration
        {
            Access = CapabilityAccessMode.Allow,
            MaximumResultCount = 25,
            MaximumCallsPerMinute = 4,
        };
        OperationDescriptor operation = CreateDescriptor("game-data.search");

        CapabilityAuthorizationDecision allowed = PluginOperationExposurePolicy.Authorize(
            operation,
            configuration,
            new TestLimitRequest(25));
        CapabilityAuthorizationDecision denied = PluginOperationExposurePolicy.Authorize(
            operation,
            configuration,
            new TestLimitRequest(26));
        CapabilityAuthorizationDecision missing = PluginOperationExposurePolicy.Authorize(
            operation,
            configuration,
            new TestLimitRequest(null));

        Assert.True(allowed.IsAllowed);
        Assert.Equal(4, allowed.MaximumCallsPerMinute);
        Assert.False(denied.IsAllowed);
        Assert.Contains("maximum '25'", denied.Reason, StringComparison.Ordinal);
        Assert.False(missing.IsAllowed);
        Assert.Contains("explicit limit", missing.Reason, StringComparison.Ordinal);
    }

    [Fact]
    public void Dynamic_sensitive_modes_use_separate_permission_scopes()
    {
        PluginUiConfiguration configuration = new() { EnableUnsafeOperations = true };
        configuration.CapabilityPolicies["screenshot.capture"] = new CapabilityPolicyConfiguration { Access = CapabilityAccessMode.Allow };
        configuration.CapabilityPolicies["screenshot.save"] = new CapabilityPolicyConfiguration { Access = CapabilityAccessMode.Deny };
        configuration.CapabilityPolicies["plugin.self.manage"] = new CapabilityPolicyConfiguration { Access = CapabilityAccessMode.Confirm };

        CapabilityAuthorizationDecision capture = PluginOperationExposurePolicy.Authorize(
            CreateDescriptor("game.screenshot"),
            configuration,
            new TestScreenshotRequest(false));
        CapabilityAuthorizationDecision save = PluginOperationExposurePolicy.Authorize(
            CreateDescriptor("game.screenshot"),
            configuration,
            new TestScreenshotRequest(true));
        CapabilityAuthorizationDecision selfManage = PluginOperationExposurePolicy.Authorize(
            CreateDescriptor("plugin.package.control"),
            configuration,
            new TestPackageRequest("DalamudMCP", "update", true));

        Assert.True(capture.IsAllowed);
        Assert.False(save.IsAllowed);
        Assert.Equal("screenshot.save", save.PermissionScope);
        Assert.True(selfManage.RequiresConfirmation);
        Assert.Equal("plugin.self.manage", selfManage.PermissionScope);
    }

    private static OperationDescriptor CreateDescriptor(string operationId) =>
        new(
            operationId,
            typeof(object),
            "ExecuteAsync",
            typeof(object),
            OperationVisibility.Both,
            []);

    private sealed record TestActionRequest(string? ActionType, long? ActionId);

    private sealed record TestPluginRequest(string PluginName, string Action);

    private sealed record TestLimitRequest(int? Limit);

    private sealed record TestTargetRequest(string GameObjectId);

    private sealed record TestScreenshotRequest(bool Save);

    private sealed record TestPackageRequest(string PluginName, string Action, bool SupervisorMode);
}
