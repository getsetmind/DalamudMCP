using DalamudMCP.Plugin.Hosting;
using DalamudMCP.Plugin.Services;
using DalamudMCP.Protocol;
using Manifold;

namespace DalamudMCP.Plugin.Operations.Tests;

public sealed class PluginManagementOperationsTests
{
    [Theory]
    [InlineData(typeof(PluginListOperation), typeof(PluginListOperation.Request), "plugin.inspect.list", "plugin_list")]
    [InlineData(typeof(PluginDescribeOperation), typeof(PluginDescribeOperation.Request), "plugin.inspect.describe", "plugin_describe")]
    [InlineData(typeof(PluginLifecycleOperation), typeof(PluginLifecycleOperation.Request), "plugin.lifecycle.control", "plugin_lifecycle")]
    [InlineData(typeof(PluginPackageOperation), typeof(PluginPackageOperation.Request), "plugin.package.control", "plugin_package")]
    public void Operations_carry_mcp_and_protocol_metadata(
        Type operationType,
        Type requestType,
        string operationId,
        string toolName)
    {
        Assert.Equal(operationId, operationType.GetCustomAttribute<OperationAttribute>()?.OperationId);
        Assert.Equal(toolName, operationType.GetCustomAttribute<McpToolAttribute>()?.Name);
        Assert.Equal(operationId, requestType.GetCustomAttribute<ProtocolOperationAttribute>()?.OperationId);
    }

    [Fact]
    public async Task Lifecycle_operation_delegates_typed_request_to_adapter()
    {
        PluginManagementResult expected = new(
            "ExamplePlugin",
            "reload",
            true,
            "completed",
            false,
            "Loaded",
            "Loaded",
            "1.0.0",
            null,
            null,
            "Reloaded.");
        FakePluginManagerAdapter adapter = new(expected);
        PluginLifecycleOperation operation = new(adapter);

        PluginManagementResult actual = await operation.ExecuteAsync(
            new PluginLifecycleOperation.Request
            {
                PluginName = "ExamplePlugin",
                Action = "reload",
            },
            new OperationContext(
                "plugin.lifecycle.control",
                InvocationSurface.Protocol,
                cancellationToken: TestContext.Current.CancellationToken));

        Assert.Same(expected, actual);
        Assert.Equal("ExamplePlugin", adapter.PluginName);
        Assert.Equal("reload", adapter.Action);
    }

    [Fact]
    public void Plugin_cursor_rejects_invalid_values()
    {
        Assert.False(PluginInspectionService.TryDecodeCursor("not-base64", out _));
        Assert.True(PluginInspectionService.TryDecodeCursor(PluginInspectionService.EncodeCursor(25), out int offset));
        Assert.Equal(25, offset);
    }

    private sealed class FakePluginManagerAdapter(PluginManagementResult result) : IPluginManagerAdapter
    {
        public string? PluginName { get; private set; }

        public string? Action { get; private set; }

        public ValueTask<PluginManagementResult> ExecuteLifecycleAsync(
            string pluginName,
            string action,
            bool dryRun,
            CancellationToken cancellationToken)
        {
            PluginName = pluginName;
            Action = action;
            return ValueTask.FromResult(result);
        }

        public ValueTask<PluginManagementResult> ExecutePackageAsync(
            string pluginName,
            string action,
            bool useTesting,
            bool dryRun,
            bool supervisorMode,
            CancellationToken cancellationToken) =>
            ValueTask.FromResult(result);
    }
}
