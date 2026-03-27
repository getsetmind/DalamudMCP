using System.Buffers.Binary;
using System.IO.Pipes;
using DalamudMCP.Protocol;
using Microsoft.Extensions.Hosting;

namespace DalamudMCP.Cli.Tests;

public sealed class CliMcpServerRunnerTests
{
    [Fact]
    public void BuildHost_requires_a_live_pipe()
    {
        InvalidOperationException exception = Assert.Throws<InvalidOperationException>(() => CliMcpServerRunner.BuildHost());
        Assert.Contains("live --pipe connection", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task BuildHost_accepts_runtime_options_with_pipe()
    {
        bool parsed = CliRuntimeOptions.TryParse(["serve", "mcp", "--pipe", "DalamudMCP.1234"], out CliRuntimeOptions? options, out string? errorMessage);

        Assert.True(parsed);
        Assert.Null(errorMessage);

        Task server = RunDescribeOperationsServerAsync(options!.PipeName!);
        using IHost host = CliMcpServerRunner.BuildHost(options);
        await server;

        Assert.NotNull(host);
    }

    private static async Task RunDescribeOperationsServerAsync(string pipeName)
    {
        await using NamedPipeServerStream server = new(
            pipeName,
            PipeDirection.InOut,
            1,
            PipeTransmissionMode.Byte,
            PipeOptions.Asynchronous);
        await server.WaitForConnectionAsync(TestContext.Current.CancellationToken);
        ProtocolRequestEnvelope request = await ReadFrameAsync(server, TestContext.Current.CancellationToken);
        Assert.Equal("__system.describe-operations", request.RequestType);
        Assert.Equal(ProtocolPayloadFormat.MemoryPack, request.PreferredResponseFormat);
        await WriteFrameAsync(server, CreateDescribeOperationsResponse(), TestContext.Current.CancellationToken);
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

    private static ProtocolResponseEnvelope CreateDescribeOperationsResponse()
    {
        return ProtocolContract.CreateSuccessResponse(
            "req-1",
            new DescribeOperationsResponse([]),
            typeof(DescribeOperationsResponse),
            preferredPayloadFormat: ProtocolPayloadFormat.MemoryPack);
    }
}
