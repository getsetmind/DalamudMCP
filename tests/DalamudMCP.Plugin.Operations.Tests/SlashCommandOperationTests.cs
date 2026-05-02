using Manifold;

namespace DalamudMCP.Plugin.Operations.Tests;

public sealed class SlashCommandOperationTests
{
    [Fact]
    public void Validate_accepts_slash_command()
    {
        SlashCommandResult? result = SlashCommandOperation.Validate("/xlreload SamplePlugin");

        Assert.Null(result);
    }

    [Fact]
    public void Validate_rejects_command_without_slash()
    {
        SlashCommandResult? result = SlashCommandOperation.Validate("xlreload SamplePlugin");

        Assert.NotNull(result);
        Assert.False(result.Success);
        Assert.Equal("validation_failed", result.Status);
    }

    [Fact]
    public async Task ExecuteAsync_uses_injected_executor()
    {
        SlashCommandResult expected = new("/ping", true, "command_sent", "ok");
        SlashCommandOperation operation = new((request, _) =>
        {
            Assert.Equal("/ping", request.Command);
            return ValueTask.FromResult(expected);
        });

        SlashCommandResult actual = await operation.ExecuteAsync(
            new SlashCommandOperation.Request { Command = "/ping" },
            OperationContext.ForCli("command.slash", cancellationToken: TestContext.Current.CancellationToken));

        Assert.Equal(expected, actual);
    }
}
