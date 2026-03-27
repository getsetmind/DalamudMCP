using System.Buffers;

namespace DalamudMCP.Cli;

internal sealed class PooledBufferStream : Stream
{
    private byte[] buffer;
    private int length;
    private int position;
    private bool disposed;

    public PooledBufferStream(int initialCapacity = 256)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(initialCapacity);

        buffer = ArrayPool<byte>.Shared.Rent(initialCapacity);
    }

    public ReadOnlyMemory<byte> WrittenMemory
    {
        get
        {
            ThrowIfDisposed();
            return buffer.AsMemory(0, length);
        }
    }

    public override bool CanRead => false;

    public override bool CanSeek => true;

    public override bool CanWrite => !disposed;

    public override long Length
    {
        get
        {
            ThrowIfDisposed();
            return length;
        }
    }

    public override long Position
    {
        get
        {
            ThrowIfDisposed();
            return position;
        }
        set
        {
            ThrowIfDisposed();
            if (value < 0 || value > int.MaxValue)
                throw new ArgumentOutOfRangeException(nameof(value));

            position = (int)value;
        }
    }

    public override void Flush()
    {
        ThrowIfDisposed();
    }

    public override Task FlushAsync(CancellationToken cancellationToken)
    {
        ThrowIfDisposed();
        cancellationToken.ThrowIfCancellationRequested();
        return Task.CompletedTask;
    }

    public override int Read(byte[] buffer, int offset, int count)
    {
        throw new NotSupportedException();
    }

    public override long Seek(long offset, SeekOrigin origin)
    {
        ThrowIfDisposed();

        long target = origin switch
        {
            SeekOrigin.Begin => offset,
            SeekOrigin.Current => position + offset,
            SeekOrigin.End => length + offset,
            _ => throw new ArgumentOutOfRangeException(nameof(origin))
        };

        if (target < 0 || target > int.MaxValue)
            throw new IOException("Attempted to seek outside the valid stream range.");

        position = (int)target;
        return position;
    }

    public override void SetLength(long value)
    {
        ThrowIfDisposed();
        if (value < 0 || value > int.MaxValue)
            throw new ArgumentOutOfRangeException(nameof(value));

        int newLength = (int)value;
        EnsureCapacity(newLength);
        if (newLength > length)
            buffer.AsSpan(length, newLength - length).Clear();

        length = newLength;
        if (position > length)
            position = length;
    }

    public override void Write(byte[] sourceBuffer, int offset, int count)
    {
        ArgumentNullException.ThrowIfNull(sourceBuffer);
        Write(sourceBuffer.AsSpan(offset, count));
    }

    public override void Write(ReadOnlySpan<byte> source)
    {
        ThrowIfDisposed();
        EnsureCapacity(position + source.Length);
        source.CopyTo(buffer.AsSpan(position));
        position += source.Length;
        if (position > length)
            length = position;
    }

    public override ValueTask WriteAsync(ReadOnlyMemory<byte> source, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        Write(source.Span);
        return ValueTask.CompletedTask;
    }

    public override void WriteByte(byte value)
    {
        ThrowIfDisposed();
        EnsureCapacity(position + 1);
        buffer[position++] = value;
        if (position > length)
            length = position;
    }

    protected override void Dispose(bool disposing)
    {
        if (disposed)
            return;

        ArrayPool<byte>.Shared.Return(buffer);
        buffer = [];
        length = 0;
        position = 0;
        disposed = true;
        base.Dispose(disposing);
    }

    private void EnsureCapacity(int required)
    {
        if (required <= buffer.Length)
            return;

        int newSize = Math.Max(required, buffer.Length * 2);
        byte[] newBuffer = ArrayPool<byte>.Shared.Rent(newSize);
        buffer.AsSpan(0, length).CopyTo(newBuffer);
        ArrayPool<byte>.Shared.Return(buffer);
        buffer = newBuffer;
    }

    private void ThrowIfDisposed()
    {
        ObjectDisposedException.ThrowIf(disposed, this);
    }
}
