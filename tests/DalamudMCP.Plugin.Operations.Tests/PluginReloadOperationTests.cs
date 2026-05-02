using Manifold;

namespace DalamudMCP.Plugin.Operations.Tests;

public sealed class PluginReloadOperationTests
{
    [Fact]
    public void Validate_blocks_self_reload()
    {
        PluginReloadResult? result = PluginReloadOperation.Validate(
            "DalamudMCP",
            ["DalamudMCP", "OtherPlugin"],
            new PluginReloadOperation.Request { PluginName = "DalamudMCP" });

        Assert.NotNull(result);
        Assert.False(result.Success);
        Assert.Equal("self_reload_blocked", result.Status);
    }

    [Fact]
    public void Validate_rejects_missing_plugin()
    {
        PluginReloadResult? result = PluginReloadOperation.Validate(
            "DalamudMCP",
            ["OtherPlugin"],
            new PluginReloadOperation.Request { PluginName = "MissingPlugin" });

        Assert.NotNull(result);
        Assert.False(result.Success);
        Assert.Equal("plugin_not_found", result.Status);
    }

    [Fact]
    public void Validate_accepts_installed_non_self_plugin()
    {
        PluginReloadResult? result = PluginReloadOperation.Validate(
            "DalamudMCP",
            ["OtherPlugin"],
            new PluginReloadOperation.Request { PluginName = "OtherPlugin" });

        Assert.Null(result);
    }

    [Fact]
    public async Task ExecuteAsync_uses_injected_executor()
    {
        PluginReloadResult expected = new("OtherPlugin", true, "reload_initiated", null, "ok");
        PluginReloadOperation operation = new((request, _) =>
        {
            Assert.Equal("OtherPlugin", request.PluginName);
            return ValueTask.FromResult(expected);
        });

        PluginReloadResult actual = await operation.ExecuteAsync(
            new PluginReloadOperation.Request { PluginName = "OtherPlugin" },
            OperationContext.ForCli("plugin.reload", cancellationToken: TestContext.Current.CancellationToken));

        Assert.Equal(expected, actual);
    }
}
