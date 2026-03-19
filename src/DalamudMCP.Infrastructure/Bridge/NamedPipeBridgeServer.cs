using System.Buffers.Binary;
using System.IO.Pipes;
using System.Text;
using DalamudMCP.Contracts.Bridge;

namespace DalamudMCP.Infrastructure.Bridge;

public sealed class NamedPipeBridgeServer : IAsyncDisposable
{
    private static readonly TimeSpan DefaultConnectionIdleTimeout = TimeSpan.FromSeconds(5);

    private readonly string pipeName;
    private readonly Func<BridgeRequestEnvelope, CancellationToken, Task<BridgeResponseEnvelope>> handler;
    private readonly object syncRoot = new();
    private readonly TimeSpan connectionIdleTimeout;
    private readonly List<Task> connectionTasks = [];

    private CancellationTokenSource? runCts;
    private Task? runTask;

    public NamedPipeBridgeServer(
        string pipeName,
        Func<BridgeRequestEnvelope, CancellationToken, Task<BridgeResponseEnvelope>> handler,
        TimeSpan? connectionIdleTimeout = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(pipeName);
        ArgumentNullException.ThrowIfNull(handler);
        this.pipeName = pipeName;
        this.handler = handler;
        this.connectionIdleTimeout = connectionIdleTimeout ?? DefaultConnectionIdleTimeout;
        if (this.connectionIdleTimeout <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(connectionIdleTimeout), "Connection idle timeout must be positive.");
        }
    }

    public string PipeName => pipeName;

    public bool IsRunning => runTask is { IsCompleted: false };

    public Task StartAsync(CancellationToken cancellationToken = default)
    {
        if (runTask is not null)
        {
            return Task.CompletedTask;
        }

        runCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        runTask = Task.Run(() => AcceptLoopAsync(runCts.Token), CancellationToken.None);
        return Task.CompletedTask;
    }

    public async Task StopAsync(CancellationToken cancellationToken = default)
    {
        if (runCts is null || runTask is null)
        {
            return;
        }

        runCts.Cancel();

        try
        {
            await runTask.WaitAsync(cancellationToken).ConfigureAwait(false);
            await Task.WhenAll(GetPendingConnectionTasks()).WaitAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
        }

        runCts.Dispose();
        runCts = null;
        runTask = null;
    }

    public async ValueTask DisposeAsync()
    {
        await StopAsync().ConfigureAwait(false);
    }

    private async Task AcceptLoopAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            var server = CreateServer();

            try
            {
                await server.WaitForConnectionAsync(cancellationToken).ConfigureAwait(false);
                RegisterConnectionTask(HandleConnectionAsync(server, cancellationToken));
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                await server.DisposeAsync().ConfigureAwait(false);
                break;
            }
            catch
            {
                await server.DisposeAsync().ConfigureAwait(false);
            }
        }
    }

    private async Task HandleConnectionAsync(NamedPipeServerStream server, CancellationToken cancellationToken)
    {
        try
        {
            BridgeTrace.Write($"pipe.accepted name={pipeName}");
            using var connectionCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            connectionCts.CancelAfter(connectionIdleTimeout);

            try
            {
                var requestBytes = await ReadFrameAsync(server, connectionCts.Token).ConfigureAwait(false);
                var line = Encoding.UTF8.GetString(requestBytes);
                BridgeTrace.Write($"pipe.received name={pipeName} empty={string.IsNullOrWhiteSpace(line)}");
                var response = await CreateResponseAsync(line, connectionCts.Token).ConfigureAwait(false);
                BridgeTrace.Write($"pipe.responding name={pipeName} success={response.Success} responseType={response.ResponseType}");
                var responseBytes = Encoding.UTF8.GetBytes(BridgeJson.Serialize(response));
                await WriteFrameAsync(server, responseBytes, connectionCts.Token).ConfigureAwait(false);
                await server.FlushAsync(connectionCts.Token).ConfigureAwait(false);
                BridgeTrace.Write($"pipe.responded name={pipeName}");
            }
            catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
            {
                BridgeTrace.Write($"pipe.connection_timeout name={pipeName}");
            }
            catch (InvalidOperationException exception) when (string.Equals(exception.Message, "Bridge stream closed unexpectedly.", StringComparison.Ordinal))
            {
                BridgeTrace.Write($"pipe.connection_closed name={pipeName}");
            }
        }
        finally
        {
            await server.DisposeAsync().ConfigureAwait(false);
        }
    }

    private NamedPipeServerStream CreateServer() =>
        new(
            pipeName,
            PipeDirection.InOut,
            NamedPipeServerStream.MaxAllowedServerInstances,
            PipeTransmissionMode.Byte,
            PipeOptions.Asynchronous);

    private void RegisterConnectionTask(Task connectionTask)
    {
        lock (syncRoot)
        {
            connectionTasks.Add(connectionTask);
        }

        _ = connectionTask.ContinueWith(
            completedTask =>
            {
                lock (syncRoot)
                {
                    connectionTasks.Remove(completedTask);
                }
            },
            CancellationToken.None,
            TaskContinuationOptions.ExecuteSynchronously,
            TaskScheduler.Default);
    }

    private Task[] GetPendingConnectionTasks()
    {
        lock (syncRoot)
        {
            return connectionTasks.ToArray();
        }
    }

    private async Task<BridgeResponseEnvelope> CreateResponseAsync(string? line, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(line))
        {
            return CreateInvalidRequestResponse("Bridge request was empty.");
        }

        try
        {
            var request = BridgeJson.Deserialize<BridgeRequestEnvelope>(line);
            if (request is null)
            {
                return CreateInvalidRequestResponse("Bridge request could not be deserialized.");
            }

            if (!ContractVersion.IsCompatible(request.ContractVersion))
            {
                return CreateErrorResponse(request.RequestId, "invalid_contract_version", "Unsupported contract version.");
            }

            var response = await handler(request, cancellationToken).ConfigureAwait(false);
            if (!ContractVersion.IsCompatible(response.ContractVersion))
            {
                return CreateErrorResponse(request.RequestId, "invalid_contract_version", "Unsupported contract version.");
            }

            return response;
        }
        catch (Exception exception) when (exception is InvalidOperationException or ArgumentException or System.Text.Json.JsonException)
        {
            return CreateInvalidRequestResponse(exception.Message);
        }
    }

    private static BridgeResponseEnvelope CreateInvalidRequestResponse(string errorMessage) =>
        new(
            ContractVersion.Current,
            string.Empty,
            BridgeResponseTypes.Error,
            Success: false,
            ErrorCode: "invalid_request",
            ErrorMessage: errorMessage,
            Payload: null);

    private static BridgeResponseEnvelope CreateErrorResponse(string requestId, string errorCode, string errorMessage) =>
        new(
            ContractVersion.Current,
            requestId,
            BridgeResponseTypes.Error,
            Success: false,
            ErrorCode: errorCode,
            ErrorMessage: errorMessage,
            Payload: null);

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
            throw new InvalidOperationException("Bridge request length was invalid.");
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
