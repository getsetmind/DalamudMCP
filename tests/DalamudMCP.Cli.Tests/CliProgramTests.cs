using System.Buffers.Binary;
using System.IO.Pipes;
using DalamudMCP.Protocol;

namespace DalamudMCP.Cli.Tests;

[Collection(DiscoveryEnvironmentSerialGroup.Name)]
public sealed class CliProgramTests : IDisposable
{
    private readonly DiscoveryEnvironmentScope scope = new();

    [Fact]
    public async Task RunAsync_returns_unavailable_for_session_status_without_pipe()
    {
        StringWriter output = new();
        StringWriter error = new();

        int exitCode = await CliProgram.RunAsync(["session", "status"], output, error, TestContext.Current.CancellationToken);

        Assert.Equal(Manifold.Cli.CliExitCodes.Unavailable, exitCode);
        Assert.Equal(string.Empty, output.ToString());
        Assert.Contains("No live plugin connection was found", error.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task RunAsync_returns_unavailable_for_session_status_json_without_pipe()
    {
        StringWriter output = new();
        StringWriter error = new();

        int exitCode = await CliProgram.RunAsync(["session", "status", "--json"], output, error, TestContext.Current.CancellationToken);

        Assert.Equal(Manifold.Cli.CliExitCodes.Unavailable, exitCode);
        Assert.Equal(string.Empty, output.ToString());
        Assert.Contains("No live plugin connection was found", error.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task RunAsync_returns_unavailable_for_player_context_without_pipe()
    {
        StringWriter output = new();
        StringWriter error = new();

        int exitCode = await CliProgram.RunAsync(["player", "context"], output, error, TestContext.Current.CancellationToken);

        Assert.Equal(Manifold.Cli.CliExitCodes.Unavailable, exitCode);
        Assert.Equal(string.Empty, output.ToString());
        Assert.Contains("No live plugin connection was found", error.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task RunAsync_returns_unavailable_for_duty_context_without_pipe()
    {
        StringWriter output = new();
        StringWriter error = new();

        int exitCode = await CliProgram.RunAsync(["duty", "context"], output, error, TestContext.Current.CancellationToken);

        Assert.Equal(Manifold.Cli.CliExitCodes.Unavailable, exitCode);
        Assert.Equal(string.Empty, output.ToString());
        Assert.Contains("No live plugin connection was found", error.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task RunAsync_executes_player_context_command_from_pipe()
    {
        StringWriter output = new();
        StringWriter error = new();
        string pipeName = "DalamudMCP.Test." + Guid.NewGuid().ToString("N");

        Task server = RunProtocolServerAsync(
            pipeName,
            CreateDescribeOperationsResponse("player.context", ["player", "context"], "get_player_context"),
            static request =>
            {
                Assert.Equal("player.context", request.RequestType);
            },
            CreatePlayerContextResponse());

        int exitCode = await CliProgram.RunAsync(["--pipe", pipeName, "player", "context"], output, error, TestContext.Current.CancellationToken);
        await server;

        Assert.Equal(Manifold.Cli.CliExitCodes.Success, exitCode);
        Assert.Equal("Test Adventurer @ ExampleWorld (Dancer 100)" + Environment.NewLine, output.ToString());
        Assert.Equal(string.Empty, error.ToString());
    }

    [Fact]
    public async Task RunAsync_executes_player_context_command_from_discovered_pipe()
    {
        StringWriter output = new();
        StringWriter error = new();
        string pipeName = "DalamudMCP.Test." + Guid.NewGuid().ToString("N");
        scope.WriteDiscovery(pipeName);

        Task server = RunProtocolServerAsync(
            pipeName,
            CreateDescribeOperationsResponse("player.context", ["player", "context"], "get_player_context"),
            static request =>
            {
                Assert.Equal("player.context", request.RequestType);
            },
            CreatePlayerContextResponse());

        int exitCode = await CliProgram.RunAsync(["player", "context"], output, error, TestContext.Current.CancellationToken);
        await server;

        Assert.Equal(Manifold.Cli.CliExitCodes.Success, exitCode);
        Assert.Equal("Test Adventurer @ ExampleWorld (Dancer 100)" + Environment.NewLine, output.ToString());
        Assert.Equal(string.Empty, error.ToString());
    }

    [Fact]
    public async Task RunAsync_executes_duty_context_command_from_pipe()
    {
        StringWriter output = new();
        StringWriter error = new();
        string pipeName = "DalamudMCP.Test." + Guid.NewGuid().ToString("N");

        Task server = RunProtocolServerAsync(
            pipeName,
            CreateDescribeOperationsResponse("duty.context", ["duty", "context"], "get_duty_context"),
            static request =>
            {
                Assert.Equal("duty.context", request.RequestType);
            },
            CreateDutyContextResponse());

        int exitCode = await CliProgram.RunAsync(["--pipe", pipeName, "duty", "context"], output, error, TestContext.Current.CancellationToken);
        await server;

        Assert.Equal(Manifold.Cli.CliExitCodes.Success, exitCode);
        Assert.Equal("Territory#777 is active." + Environment.NewLine, output.ToString());
        Assert.Equal(string.Empty, error.ToString());
    }

    [Fact]
    public async Task RunAsync_executes_session_status_command_from_pipe()
    {
        StringWriter output = new();
        StringWriter error = new();
        string pipeName = "DalamudMCP.Test." + Guid.NewGuid().ToString("N");

        Task server = RunProtocolServerAsync(
            pipeName,
            CreateDescribeOperationsResponse("session.status", ["session", "status"], "get_session_status"),
            static request =>
            {
                Assert.Equal("session.status", request.RequestType);
            },
            CreateSessionStatusResponse());

        int exitCode = await CliProgram.RunAsync(["--pipe", pipeName, "session", "status"], output, error, TestContext.Current.CancellationToken);
        await server;

        Assert.Equal(Manifold.Cli.CliExitCodes.Success, exitCode);
        Assert.Equal("2/2 readers ready" + Environment.NewLine, output.ToString());
        Assert.Equal(string.Empty, error.ToString());
    }

    [Fact]
    public async Task RunAsync_executes_session_status_command_as_json_from_pipe()
    {
        StringWriter output = new();
        StringWriter error = new();
        string pipeName = "DalamudMCP.Test." + Guid.NewGuid().ToString("N");

        Task server = RunProtocolServerAsync(
            pipeName,
            CreateDescribeOperationsResponse("session.status", ["session", "status"], "get_session_status"),
            static request =>
            {
                Assert.Equal("session.status", request.RequestType);
            },
            CreateSessionStatusResponse());

        int exitCode = await CliProgram.RunAsync(["--pipe", pipeName, "session", "status", "--json"], output, error, TestContext.Current.CancellationToken);
        await server;

        Assert.Equal(Manifold.Cli.CliExitCodes.Success, exitCode);
        Assert.Contains("\"state\":2", output.ToString(), StringComparison.OrdinalIgnoreCase);
        Assert.Contains("\"summaryText\":\"2/2 readers ready\"", output.ToString(), StringComparison.OrdinalIgnoreCase);
        Assert.Equal(string.Empty, error.ToString());
    }

    [Fact]
    public async Task RunAsync_returns_usage_error_for_invalid_runtime_options()
    {
        StringWriter output = new();
        StringWriter error = new();

        int exitCode = await CliProgram.RunAsync(["serve", "mcp", "--json"], output, error, TestContext.Current.CancellationToken);

        Assert.Equal(Manifold.Cli.CliExitCodes.UsageError, exitCode);
        Assert.Equal(string.Empty, output.ToString());
        Assert.Contains("serve mcp", error.ToString(), StringComparison.Ordinal);
    }

    private static async Task RunProtocolServerAsync(
        string pipeName,
        ProtocolResponseEnvelope catalogResponse,
        Action<ProtocolRequestEnvelope> assertOperationRequest,
        ProtocolResponseEnvelope operationResponse)
    {
        await RunSingleRequestServerAsync(
            pipeName,
            static request =>
            {
                Assert.Equal("__system.describe-operations", request.RequestType);
                Assert.Equal(ProtocolPayloadFormat.MemoryPack, request.PreferredResponseFormat);
            },
            catalogResponse);
        await RunSingleRequestServerAsync(pipeName, assertOperationRequest, operationResponse);
    }

    private static async Task RunSingleRequestServerAsync(
        string pipeName,
        Action<ProtocolRequestEnvelope> assertRequest,
        ProtocolResponseEnvelope response)
    {
        await using NamedPipeServerStream server = new(
            pipeName,
            PipeDirection.InOut,
            1,
            PipeTransmissionMode.Byte,
            PipeOptions.Asynchronous);

        await server.WaitForConnectionAsync(TestContext.Current.CancellationToken);

        ProtocolRequestEnvelope request = await ReadFrameAsync(server, TestContext.Current.CancellationToken);
        assertRequest(request);
        await WriteFrameAsync(server, response, TestContext.Current.CancellationToken);
    }

    private static async Task<ProtocolRequestEnvelope> ReadFrameAsync(PipeStream stream, CancellationToken cancellationToken)
    {
        byte[] header = new byte[sizeof(int)];
        await ReadExactAsync(stream, header, cancellationToken);
        int length = BinaryPrimitives.ReadInt32LittleEndian(header);
        byte[] payload = new byte[length];
        await ReadExactAsync(stream, payload, cancellationToken);
        return ProtocolContract.DeserializeRequestEnvelope(payload);
    }

    private static async Task WriteFrameAsync(
        PipeStream stream,
        ProtocolResponseEnvelope response,
        CancellationToken cancellationToken)
    {
        byte[] bytes = ProtocolContract.SerializeEnvelope(response);
        byte[] header = new byte[sizeof(int)];
        BinaryPrimitives.WriteInt32LittleEndian(header, bytes.Length);
        await stream.WriteAsync(header, cancellationToken);
        await stream.WriteAsync(bytes, cancellationToken);
        await stream.FlushAsync(cancellationToken);
    }

    private static async Task ReadExactAsync(PipeStream stream, byte[] buffer, CancellationToken cancellationToken)
    {
        int offset = 0;
        while (offset < buffer.Length)
        {
            int bytesRead = await stream.ReadAsync(buffer.AsMemory(offset, buffer.Length - offset), cancellationToken);
            if (bytesRead == 0)
                throw new InvalidOperationException("Pipe closed unexpectedly.");

            offset += bytesRead;
        }
    }

    private static ProtocolResponseEnvelope CreateDescribeOperationsResponse(
        string operationId,
        string[] cliCommandPath,
        string mcpToolName)
    {
        return ProtocolContract.CreateSuccessResponse(
            "req-0",
            new DescribeOperationsResponse(
                [
                    new ProtocolOperationDescriptor(
                        operationId,
                        ProtocolOperationVisibility.Both,
                        [],
                        "test operation",
                        "test operation",
                        cliCommandPath,
                        null,
                        mcpToolName,
                        false)
                ]),
            typeof(DescribeOperationsResponse),
            preferredPayloadFormat: ProtocolPayloadFormat.MemoryPack);
    }

    private static ProtocolResponseEnvelope CreatePlayerContextResponse()
    {
        object payload = new
        {
            characterName = "Test Adventurer",
            homeWorld = "ExampleWorld",
            jobName = "Dancer",
            jobLevel = 100,
            territoryName = "Sample Plaza",
            position = new
            {
                x = 1.2,
                y = 3.4,
                z = 5.6
            }
        };
        return ProtocolContract.CreateSuccessResponse("req-1", payload, payload.GetType(), "Test Adventurer @ ExampleWorld (Dancer 100)");
    }

    private static ProtocolResponseEnvelope CreateDutyContextResponse()
    {
        object payload = new
        {
            territoryId = 777,
            dutyName = "Territory#777",
            dutyType = "duty",
            inDuty = true,
            isDutyComplete = false,
            summaryText = "Territory#777 is active."
        };
        return ProtocolContract.CreateSuccessResponse("req-2", payload, payload.GetType(), "Territory#777 is active.");
    }

    private static ProtocolResponseEnvelope CreateSessionStatusResponse()
    {
        object payload = new
        {
            state = 2,
            summaryText = "2/2 readers ready",
            readers = new[]
            {
                new { readerKey = "player.context", isReady = true, detail = "ready" },
                new { readerKey = "duty.context", isReady = true, detail = "ready" }
            }
        };
        return ProtocolContract.CreateSuccessResponse("req-3", payload, payload.GetType(), "2/2 readers ready");
    }

    public void Dispose()
    {
        scope.Dispose();
    }
}
