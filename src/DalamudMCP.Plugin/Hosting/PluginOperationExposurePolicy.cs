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
        "addon.input",
        "addon.event",
        "addon.callback.values",
        "addon.select.menu-item"
    ];

    private static readonly HashSet<string> UnsafeOperationIds =
    [
        "unsafe.invoke.plugin-ipc"
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

    public static IEnumerable<OperationDescriptor> FilterProtocolOperations(
        IEnumerable<OperationDescriptor> operations,
        bool enableActionOperations,
        bool enableUnsafeOperations)
    {
        ArgumentNullException.ThrowIfNull(operations);

        return operations.Where(operation => IsEnabled(operation, enableActionOperations, enableUnsafeOperations));
    }

    public static string[] GetExpectedMcpToolNames(
        IEnumerable<OperationDescriptor> operations,
        bool enableActionOperations,
        bool enableUnsafeOperations)
    {
        ArgumentNullException.ThrowIfNull(operations);

        return FilterProtocolOperations(operations, enableActionOperations, enableUnsafeOperations)
            .Where(static operation =>
                operation.Visibility is not OperationVisibility.CliOnly &&
                !operation.Hidden &&
                !string.IsNullOrWhiteSpace(operation.McpToolName))
            .Select(static operation => operation.McpToolName!)
            .OrderBy(static name => name, StringComparer.Ordinal)
            .ToArray();
    }
}



