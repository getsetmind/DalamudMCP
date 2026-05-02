using DalamudMCP.Plugin.Hosting;

namespace DalamudMCP.Plugin.Tests;

public sealed class PluginOperationExposurePolicyTests
{
    public static TheoryData<string> UnsafeOperationIds => new()
    {
        "unsafe.invoke.plugin-ipc",
        "plugin.ipc",
        "plugin.reload",
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
}
