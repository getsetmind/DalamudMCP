using System.Buffers;
using System.Buffers.Binary;
using System.IO.Pipes;

namespace DalamudMCP.Protocol;

public sealed class NamedPipeProtocolServer : IAsyncDisposable
{
    private static readonly TimeSpan DefaultConnectionIdleTimeout = TimeSpan.FromSeconds(5);

    private readonly string pipeName;
    private readonly Func<ProtocolRequestEnvelope, CancellationToken, ValueTask<ProtocolResponseEnvelope>> handler;
    private readonly object syncRoot = new();
    private readonly TimeSpan connectionIdleTimeout;
    private readonly List<Task> connectionTasks = [];

    private CancellationTokenSource? runCts;
    private Task? runTask;

    public NamedPipeProtocolServer(
        string pipeName,
        Func<ProtocolRequestEnvelope, CancellationToken, ValueTask<ProtocolResponseEnvelope>> handler,
        TimeSpan? connectionIdleTimeout = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(pipeName);
        ArgumentNullException.ThrowIfNull(handler);

        this.pipeName = pipeName.Trim();
        this.handler = handler;
        this.connectionIdleTimeout = connectionIdleTimeout ?? DefaultConnectionIdleTimeout;
        if (this.connectionIdleTimeout <= TimeSpan.Zero)
            throw new ArgumentOutOfRangeException(nameof(connectionIdleTimeout), "Connection idle timeout must be positive.");
    }

    public string PipeName => pipeName;

    public bool IsRunning => runTask is { IsCompleted: false };

    public Task StartAsync(CancellationToken cancellationToken = default)
    {
        if (runTask is not null)
            return Task.CompletedTask;

        runCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        runTask = Task.Run(() => AcceptLoopAsync(runCts.Token), CancellationToken.None);
        return Task.CompletedTask;
    }

    public async Task StopAsync(CancellationToken cancellationToken = default)
    {
        if (runCts is null || runTask is null)
            return;

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
            NamedPipeServerStream server = new(
                pipeName,
                PipeDirection.InOut,
                NamedPipeServerStream.MaxAllowedServerInstances,
                PipeTransmissionMode.Byte,
                PipeOptions.Asynchronous);

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
        await using (server.ConfigureAwait(false))
        {
            using CancellationTokenSource connectionCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            connectionCts.CancelAfter(connectionIdleTimeout);

            try
            {
                byte[] requestBytes = await ReadFrameAsync(server, connectionCts.Token).ConfigureAwait(false);
                ProtocolRequestEnvelope request = ProtocolContract.DeserializeRequestEnvelope(requestBytes);
                ProtocolContract.EnsureCompatible(request.ContractVersion, nameof(request.ContractVersion));

                ProtocolResponseEnvelope response = await handler(request, connectionCts.Token).ConfigureAwait(false);
                await WriteResponseAsync(server, response, connectionCts.Token).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
            {
            }
            catch (Exception exception) when (exception is InvalidOperationException or ArgumentException)
            {
                await WriteResponseAsync(
                    server,
                    ProtocolContract.CreateErrorResponse(
                        ProtocolContract.DefaultRequestId,
                        "invalid_request",
                        exception.Message),
                    connectionCts.Token).ConfigureAwait(false);
            }
        }
    }

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

    private static async Task WriteResponseAsync(
        PipeStream stream,
        ProtocolResponseEnvelope response,
        CancellationToken cancellationToken)
    {
        ProtocolContract.EnsureCompatible(response.ContractVersion, nameof(response.ContractVersion));
        byte[] payload = ProtocolContract.SerializeEnvelope(response);
        await WriteFrameAsync(stream, payload, cancellationToken).ConfigureAwait(false);
        await stream.FlushAsync(cancellationToken).ConfigureAwait(false);
    }

    private static async Task WriteFrameAsync(PipeStream stream, byte[] payload, CancellationToken cancellationToken)
    {
        byte[] header = ArrayPool<byte>.Shared.Rent(sizeof(int));
        try
        {
            BinaryPrimitives.WriteInt32LittleEndian(header, payload.Length);
            await stream.WriteAsync(header.AsMemory(0, sizeof(int)), cancellationToken).ConfigureAwait(false);
            await stream.WriteAsync(payload, cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(header);
        }
    }

    private static async Task<byte[]> ReadFrameAsync(PipeStream stream, CancellationToken cancellationToken)
    {
        byte[] header = ArrayPool<byte>.Shared.Rent(sizeof(int));
        int length;
        try
        {
            await ReadExactAsync(stream, header.AsMemory(0, sizeof(int)), cancellationToken).ConfigureAwait(false);
            length = BinaryPrimitives.ReadInt32LittleEndian(header);
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(header);
        }

        if (length <= 0)
            throw new InvalidOperationException("Protocol request length was invalid.");

        byte[] payload = new byte[length];
        await ReadExactAsync(stream, payload.AsMemory(), cancellationToken).ConfigureAwait(false);
        return payload;
    }

    private static async Task ReadExactAsync(PipeStream stream, Memory<byte> buffer, CancellationToken cancellationToken)
    {
        int offset = 0;
        while (offset < buffer.Length)
        {
            int bytesRead = await stream.ReadAsync(buffer[offset..], cancellationToken)
                .ConfigureAwait(false);
            if (bytesRead == 0)
                throw new InvalidOperationException("Protocol stream closed unexpectedly.");

            offset += bytesRead;
        }
    }
}
