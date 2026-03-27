using DalamudMCP.Framework;
using DalamudMCP.Plugin.Readers;
using DalamudMCP.Protocol;

namespace DalamudMCP.Plugin.Operations.Tests;

public sealed class SessionStatusOperationTests
{
    [Fact]
    public void SessionStatusOperation_CarriesCliAndMcpMetadata_OnTheOperationClass()
    {
        Type operationType = typeof(SessionStatusOperation);

        OperationAttribute? operation = operationType.GetCustomAttribute<OperationAttribute>();
        CliCommandAttribute? cli = operationType.GetCustomAttribute<CliCommandAttribute>();
        McpToolAttribute? mcp = operationType.GetCustomAttribute<McpToolAttribute>();

        Assert.NotNull(operation);
        Assert.NotNull(cli);
        Assert.NotNull(mcp);
        Assert.Equal("session.status", operation.OperationId);
        Assert.Equal(["session", "status"], cli.PathSegments);
        Assert.Equal("get_session_status", mcp.Name);
    }

    [Fact]
    public void SessionStatusOperation_RequestCarriesProtocolIdentity()
    {
        ProtocolOperationAttribute? protocol = typeof(SessionStatusOperation.Request)
            .GetCustomAttribute<ProtocolOperationAttribute>();

        Assert.NotNull(protocol);
        Assert.Equal("session.status", protocol.OperationId);
    }

    [Fact]
    public async Task ExecuteAsync_UsesInjectedExecutor_AndContextCancellation()
    {
        SessionStatusSnapshot expected = new(
            SessionRuntimeState.Ready,
            "7/7 readers ready; bridge server running.",
            [new SessionReaderStatus("player_context", true)]);
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        CancellationToken observedCancellationToken = default;
        SessionStatusOperation operation = new(
            cancellation =>
            {
                observedCancellationToken = cancellation;
                return ValueTask.FromResult(expected);
            });

        SessionStatusSnapshot actual = await operation.ExecuteAsync(
            new SessionStatusOperation.Request(),
            OperationContext.ForCli("session.status", cancellationToken: cancellationToken));

        Assert.Equal(expected, actual);
        Assert.Equal(cancellationToken, observedCancellationToken);
    }

    [Fact]
    public void TextFormatter_uses_summary_text()
    {
        SessionStatusSnapshot snapshot = new(
            SessionRuntimeState.Ready,
            "2/2 readers ready",
            [new SessionReaderStatus("player.context", true, "ready")]);

        string? text = new SessionStatusOperation.TextFormatter().FormatText(
            snapshot,
            OperationContext.ForCli("session.status", cancellationToken: CancellationToken.None));

        Assert.Equal("2/2 readers ready", text);
    }

    [Fact]
    public void CreateReaderStatus_degrades_when_reader_status_throws()
    {
        MethodInfo createReaderStatus = typeof(SessionStatusOperation)
            .GetMethod("CreateReaderStatus", BindingFlags.NonPublic | BindingFlags.Static)!;

        SessionReaderStatus reader = (SessionReaderStatus)createReaderStatus.Invoke(null, [new ThrowingReaderStatus()])!;

        Assert.Equal("throwing.reader", reader.ReaderKey);
        Assert.False(reader.IsReady);
        Assert.Equal("Not on main thread!", reader.Detail);
    }

    private sealed class ThrowingReaderStatus : IPluginReaderStatus
    {
        public string ReaderKey => "throwing.reader";

        public bool IsReady => throw new InvalidOperationException("Not on main thread!");

        public string Detail => throw new InvalidOperationException("Not on main thread!");
    }
}
