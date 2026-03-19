using System.Buffers.Binary;
using System.IO.Pipes;
using System.Text;
using DalamudMCP.Contracts.Bridge;

namespace DalamudMCP.Infrastructure.Bridge;

public sealed class NamedPipeBridgeClient
{
    private readonly TimeSpan connectTimeout;
    private readonly string pipeName;

    public NamedPipeBridgeClient(string pipeName, TimeSpan? connectTimeout = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(pipeName);
        this.pipeName = pipeName;
        this.connectTimeout = connectTimeout ?? TimeSpan.FromSeconds(1);
        if (this.connectTimeout <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(connectTimeout), "Connect timeout must be positive.");
        }
    }

    public async Task<BridgeResponseEnvelope> SendAsync(BridgeRequestEnvelope request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        BridgeTrace.Write($"client.connect.enter name={pipeName} requestType={request.RequestType} requestId={request.RequestId}");
        await using var client = new NamedPipeClientStream(
            serverName: ".",
            pipeName,
            PipeDirection.InOut,
            PipeOptions.Asynchronous);
        await client.ConnectAsync((int)connectTimeout.TotalMilliseconds, cancellationToken).ConfigureAwait(false);
        BridgeTrace.Write($"client.connect.exit name={pipeName} requestType={request.RequestType} requestId={request.RequestId}");

        var requestJson = BridgeJson.Serialize(request);
        BridgeTrace.Write($"client.write.enter name={pipeName} requestType={request.RequestType} requestId={request.RequestId}");
        var requestBytes = Encoding.UTF8.GetBytes(requestJson);
        await WriteFrameAsync(client, requestBytes, cancellationToken).ConfigureAwait(false);
        await client.FlushAsync(cancellationToken).ConfigureAwait(false);
        BridgeTrace.Write($"client.write.exit name={pipeName} requestType={request.RequestType} requestId={request.RequestId}");
        BridgeTrace.Write($"client.read.enter name={pipeName} requestType={request.RequestType} requestId={request.RequestId}");
        var responseBytes = await ReadFrameAsync(client, cancellationToken).ConfigureAwait(false);
        var line = Encoding.UTF8.GetString(responseBytes);
        BridgeTrace.Write($"client.read.exit name={pipeName} requestType={request.RequestType} requestId={request.RequestId} empty={string.IsNullOrWhiteSpace(line)}");
        if (string.IsNullOrWhiteSpace(line))
        {
            throw new InvalidOperationException("Bridge response was empty.");
        }

        var response = BridgeJson.Deserialize<BridgeResponseEnvelope>(line);
        if (response is null)
        {
            throw new InvalidOperationException("Bridge response could not be deserialized.");
        }

        ContractVersion.EnsureCompatible(response.ContractVersion, nameof(response));
        return response;
    }

    private static async Task WriteFrameAsync(PipeStream stream, byte[] payload, CancellationToken cancellationToken)
    {
        var header = new byte[sizeof(int)];
        BinaryPrimitives.WriteInt32LittleEndian(header, payload.Length);
        await stream.WriteAsync(header, cancellationToken).ConfigureAwait(false);
        await stream.WriteAsync(payload, cancellationToken).ConfigureAwait(false);
    }

    private static async Task<byte[]> ReadFrameAsync(PipeStream stream, CancellationToken cancellationToken)
    {
        var header = new byte[sizeof(int)];
        await ReadExactAsync(stream, header, cancellationToken).ConfigureAwait(false);
        var length = BinaryPrimitives.ReadInt32LittleEndian(header);
        if (length <= 0)
        {
            throw new InvalidOperationException("Bridge response length was invalid.");
        }

        var payload = new byte[length];
        await ReadExactAsync(stream, payload, cancellationToken).ConfigureAwait(false);
        return payload;
    }

    private static async Task ReadExactAsync(PipeStream stream, byte[] buffer, CancellationToken cancellationToken)
    {
        var offset = 0;
        while (offset < buffer.Length)
        {
            var bytesRead = await stream.ReadAsync(buffer.AsMemory(offset, buffer.Length - offset), cancellationToken).ConfigureAwait(false);
            if (bytesRead == 0)
            {
                throw new InvalidOperationException("Bridge stream closed unexpectedly.");
            }

            offset += bytesRead;
        }
    }
}
