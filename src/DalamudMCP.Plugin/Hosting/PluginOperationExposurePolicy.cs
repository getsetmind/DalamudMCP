using System.Reflection;
using DalamudMCP.Plugin.Configuration;
using Manifold;

namespace DalamudMCP.Plugin.Hosting;

internal static class PluginOperationExposurePolicy
{
    private static readonly HashSet<string> ActionOperationIds =
    [
        "target.object",
        "interact.with.target",
        "move.to.entity",
        "move.to.nearby.interactable",
        "teleport.to.aetheryte",
        "duty.action",
        "game.action.execute",
        "addon.input",
        "addon.event",
        "addon.callback.values",
        "addon.select.menu-item"
    ];

    private static readonly HashSet<string> UnsafeOperationIds =
    [
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
    ];

    public static bool IsActionOperation(string operationId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(operationId);
        return ActionOperationIds.Contains(operationId);
    }

    public static bool IsUnsafeOperation(string operationId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(operationId);
        return UnsafeOperationIds.Contains(operationId);
    }

    public static bool IsEnabled(OperationDescriptor operation, bool enableActionOperations, bool enableUnsafeOperations)
    {
        ArgumentNullException.ThrowIfNull(operation);
        return (enableActionOperations || !IsActionOperation(operation.OperationId)) &&
               (enableUnsafeOperations || !IsUnsafeOperation(operation.OperationId));
    }

    public static bool IsVisible(OperationDescriptor operation, PluginUiConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(operation);
        ArgumentNullException.ThrowIfNull(configuration);

        if (string.Equals(operation.OperationId, "plugin.package.control", StringComparison.Ordinal))
        {
            return ResolveAccess(operation, configuration, request: null, "plugin.package.install") is not CapabilityAccessMode.Deny ||
                   ResolveAccess(operation, configuration, request: null, "plugin.package.update") is not CapabilityAccessMode.Deny ||
                   ResolveAccess(operation, configuration, request: null, "plugin.package.uninstall") is not CapabilityAccessMode.Deny;
        }

        return ResolveAccess(operation, configuration, request: null) is not CapabilityAccessMode.Deny;
    }

    public static CapabilityAuthorizationDecision Authorize(
        OperationDescriptor operation,
        PluginUiConfiguration configuration,
        object? request)
    {
        ArgumentNullException.ThrowIfNull(operation);
        ArgumentNullException.ThrowIfNull(configuration);

        string permissionScope = ResolvePermissionScope(operation, request);
        CapabilityAccessMode access = ResolveAccess(operation, configuration, request, permissionScope);
        if (access is CapabilityAccessMode.Deny)
            return CapabilityAuthorizationDecision.Denied(permissionScope, "Capability is denied by the plugin policy.");

        CapabilityPolicyConfiguration? policy = FindPolicy(configuration, permissionScope);
        if (policy is null)
        {
            return access is CapabilityAccessMode.Confirm
                ? CapabilityAuthorizationDecision.ConfirmationRequired(permissionScope, approvalId: null, maximumCallsPerMinute: 0)
                : CapabilityAuthorizationDecision.Allowed(permissionScope);
        }

        string? pluginName = ReadStringProperty(request, "PluginName");
        if (pluginName is not null && !MatchesPlugin(policy.AllowedPluginNames, pluginName))
        {
            return CapabilityAuthorizationDecision.Denied(
                permissionScope,
                $"Plugin '{pluginName}' is outside the allowed targets for '{permissionScope}'.");
        }

        string? actionType = ReadStringProperty(request, "ActionType");
        if (policy.AllowedActionTypes.Length > 0 &&
            (actionType is null || !policy.AllowedActionTypes.Contains(actionType, StringComparer.OrdinalIgnoreCase)))
        {
            return CapabilityAuthorizationDecision.Denied(
                permissionScope,
                actionType is null
                    ? $"An explicit action type is required by the policy for '{permissionScope}'."
                    : $"Action type '{actionType}' is outside the allowed values for '{permissionScope}'.");
        }

        long? actionId = ReadLongProperty(request, "ActionId");
        if (policy.AllowedActionIds.Length > 0 &&
            (actionId is null || !policy.AllowedActionIds.Contains(actionId.Value)))
        {
            return CapabilityAuthorizationDecision.Denied(
                permissionScope,
                actionId is null
                    ? $"An explicit action ID is required by the policy for '{permissionScope}'."
                    : $"Action ID '{actionId}' is outside the allowed values for '{permissionScope}'.");
        }

        long? requestedLimit = ReadLongProperty(request, "Limit");
        bool requestSupportsLimit = request?.GetType().GetProperty(
            "Limit",
            BindingFlags.Public | BindingFlags.Instance) is not null;
        if (policy.MaximumResultCount > 0 &&
            requestSupportsLimit &&
            (requestedLimit is null || requestedLimit > policy.MaximumResultCount))
        {
            return CapabilityAuthorizationDecision.Denied(
                permissionScope,
                requestedLimit is null
                    ? $"An explicit limit is required by the policy for '{permissionScope}'."
                    : $"Requested limit '{requestedLimit}' exceeds the policy maximum '{policy.MaximumResultCount}'.");
        }

        string? targetObjectId = ReadStringProperty(request, "TargetObjectId") ??
                                 ReadStringProperty(request, "GameObjectId") ??
                                 ReadStringProperty(request, "ExpectedGameObjectId");
        if (!MatchesOptionalString(policy.AllowedSheetNames, ReadStringProperty(request, "Sheet"), "sheet", permissionScope, out string? stringError) ||
            !MatchesOptionalString(policy.AllowedCallgates, ReadStringProperty(request, "Callgate"), "callgate", permissionScope, out stringError) ||
            !MatchesOptionalString(policy.AllowedTargetObjectIds, targetObjectId, "target-object-id", permissionScope, out stringError))
        {
            return CapabilityAuthorizationDecision.Denied(permissionScope, stringError!);
        }

        long? classJobId = ReadLongProperty(request, "RequiredClassJobId");
        if (!MatchesOptionalNumber(policy.AllowedClassJobIds, classJobId, "required-class-job-id", permissionScope, out string? numberError) ||
            !MatchesOptionalNumber(policy.AllowedTerritoryIds, ReadLongProperty(request, "RequiredTerritoryId"), "required-territory-id", permissionScope, out numberError))
        {
            return CapabilityAuthorizationDecision.Denied(permissionScope, numberError!);
        }

        return access is CapabilityAccessMode.Confirm
            ? CapabilityAuthorizationDecision.ConfirmationRequired(
                permissionScope,
                approvalId: null,
                policy.MaximumCallsPerMinute)
            : CapabilityAuthorizationDecision.Allowed(permissionScope, policy.MaximumCallsPerMinute);
    }

    public static IEnumerable<OperationDescriptor> FilterProtocolOperations(
        IEnumerable<OperationDescriptor> operations,
        bool enableActionOperations,
        bool enableUnsafeOperations)
    {
        ArgumentNullException.ThrowIfNull(operations);
        return operations.Where(operation => IsEnabled(operation, enableActionOperations, enableUnsafeOperations));
    }

    public static IEnumerable<OperationDescriptor> FilterProtocolOperations(
        IEnumerable<OperationDescriptor> operations,
        PluginUiConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(operations);
        ArgumentNullException.ThrowIfNull(configuration);
        return operations.Where(operation => IsVisible(operation, configuration));
    }

    public static string[] GetExpectedMcpToolNames(
        IEnumerable<OperationDescriptor> operations,
        bool enableActionOperations,
        bool enableUnsafeOperations)
    {
        ArgumentNullException.ThrowIfNull(operations);
        return GetMcpToolNames(FilterProtocolOperations(operations, enableActionOperations, enableUnsafeOperations));
    }

    public static string[] GetExpectedMcpToolNames(
        IEnumerable<OperationDescriptor> operations,
        PluginUiConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(operations);
        ArgumentNullException.ThrowIfNull(configuration);
        return GetMcpToolNames(FilterProtocolOperations(operations, configuration));
    }

    private static string[] GetMcpToolNames(IEnumerable<OperationDescriptor> operations) =>
        operations
            .Where(static operation =>
                operation.Visibility is not OperationVisibility.CliOnly &&
                !operation.Hidden &&
                !string.IsNullOrWhiteSpace(operation.McpToolName))
            .Select(static operation => operation.McpToolName!)
            .OrderBy(static name => name, StringComparer.Ordinal)
            .ToArray();

    private static CapabilityAccessMode ResolveAccess(
        OperationDescriptor operation,
        PluginUiConfiguration configuration,
        object? request,
        string? resolvedScope = null)
    {
        string permissionScope = resolvedScope ?? ResolvePermissionScope(operation, request);
        CapabilityPolicyConfiguration? policy = FindPolicy(configuration, permissionScope);
        if (policy is not null)
            return policy.Access;

        if (IsActionOperation(operation.OperationId))
            return configuration.EnableActionOperations ? CapabilityAccessMode.Allow : CapabilityAccessMode.Deny;
        if (IsUnsafeOperation(operation.OperationId))
            return configuration.EnableUnsafeOperations ? CapabilityAccessMode.Allow : CapabilityAccessMode.Deny;

        return CapabilityAccessMode.Allow;
    }

    private static CapabilityPolicyConfiguration? FindPolicy(
        PluginUiConfiguration configuration,
        string permissionScope)
    {
        if (configuration.CapabilityPolicies.TryGetValue(permissionScope, out CapabilityPolicyConfiguration? exact))
            return exact;

        int separator = permissionScope.LastIndexOf('.');
        if (separator > 0 &&
            configuration.CapabilityPolicies.TryGetValue(permissionScope[..separator], out CapabilityPolicyConfiguration? parent))
        {
            return parent;
        }

        return null;
    }

    private static string ResolvePermissionScope(OperationDescriptor operation, object? request)
    {
        if (string.Equals(operation.OperationId, "plugin.package.control", StringComparison.Ordinal))
        {
            if (ReadProperty(request, "SupervisorMode") is true)
                return "plugin.self.manage";

            string? action = ReadStringProperty(request, "Action")?.ToLowerInvariant();
            if (action is "install" or "update" or "uninstall")
                return $"plugin.package.{action}";
        }

        if (string.Equals(operation.OperationId, "game.screenshot", StringComparison.Ordinal) &&
            ReadProperty(request, "Save") is true)
        {
            return "screenshot.save";
        }

        return PluginOperationMetadataCatalog.Resolve(operation.OperationId).PermissionScope;
    }

    private static bool MatchesPlugin(IEnumerable<string>? allowedPluginNames, string pluginName)
    {
        string[] allowed = allowedPluginNames?.ToArray() ?? [];
        return allowed.Length == 0 ||
               allowed.Contains("*", StringComparer.Ordinal) ||
               allowed.Contains(pluginName, StringComparer.OrdinalIgnoreCase);
    }

    private static bool MatchesOptionalString(
        string[] allowedValues,
        string? value,
        string parameterName,
        string permissionScope,
        out string? error)
    {
        if (allowedValues.Length == 0 || allowedValues.Contains("*", StringComparer.Ordinal))
        {
            error = null;
            return true;
        }

        if (value is not null && allowedValues.Contains(value, StringComparer.OrdinalIgnoreCase))
        {
            error = null;
            return true;
        }

        error = value is null
            ? $"An explicit {parameterName} is required by the policy for '{permissionScope}'."
            : $"{parameterName} '{value}' is outside the allowed values for '{permissionScope}'.";
        return false;
    }

    private static bool MatchesOptionalNumber(
        uint[] allowedValues,
        long? value,
        string parameterName,
        string permissionScope,
        out string? error)
    {
        if (allowedValues.Length == 0)
        {
            error = null;
            return true;
        }

        if (value is >= 0 and <= uint.MaxValue && allowedValues.Contains((uint)value.Value))
        {
            error = null;
            return true;
        }

        error = value is null
            ? $"An explicit {parameterName} is required by the policy for '{permissionScope}'."
            : $"{parameterName} '{value}' is outside the allowed values for '{permissionScope}'.";
        return false;
    }

    private static string? ReadStringProperty(object? request, string propertyName) =>
        ReadProperty(request, propertyName)?.ToString()?.Trim();

    private static long? ReadLongProperty(object? request, string propertyName)
    {
        object? value = ReadProperty(request, propertyName);
        return value switch
        {
            byte number => number,
            short number => number,
            int number => number,
            long number => number,
            uint number => number,
            _ => null,
        };
    }

    private static object? ReadProperty(object? request, string propertyName) =>
        request?.GetType().GetProperty(propertyName, BindingFlags.Public | BindingFlags.Instance)?.GetValue(request);
}

public sealed record CapabilityAuthorizationDecision(
    bool IsAllowed,
    bool RequiresConfirmation,
    string PermissionScope,
    string? Reason,
    int MaximumCallsPerMinute = 0,
    string? ApprovalId = null)
{
    public static CapabilityAuthorizationDecision Allowed(
        string permissionScope,
        int maximumCallsPerMinute = 0,
        string? approvalId = null,
        string? reason = null) =>
        new(true, false, permissionScope, reason, maximumCallsPerMinute, approvalId);

    public static CapabilityAuthorizationDecision Denied(
        string permissionScope,
        string reason,
        string? approvalId = null) =>
        new(false, false, permissionScope, reason, ApprovalId: approvalId);

    public static CapabilityAuthorizationDecision ConfirmationRequired(
        string permissionScope,
        string? approvalId,
        int maximumCallsPerMinute = 0) =>
        new(
            false,
            true,
            permissionScope,
            approvalId is null
                ? $"Capability '{permissionScope}' requires confirmation in the plugin settings."
                : $"Capability '{permissionScope}' is awaiting one-time approval '{approvalId}' in the plugin settings.",
            maximumCallsPerMinute,
            approvalId);
}
