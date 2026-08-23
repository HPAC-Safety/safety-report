namespace HpacSafety.Core.Tests.Media;

/// <summary>
/// A source stream that behaves as though it were an enormous upload — far
/// larger than any deployment's <c>MediaPolicy.MaxByteSize</c> — without
/// actually allocating that much memory. It hands back zero bytes on request and
/// records how many it was asked for.
/// <para>
/// This is what lets a test prove "the oversized object is never pulled fully
/// into memory before it is rejected" without needing gigabytes of RAM to make
/// the point: if <see cref="TotalBytesServed" /> stays small after ingest
/// rejects the file, the ingestor stopped reading long before reaching the end.
/// </para>
/// </summary>
internal sealed class SyntheticOversizedStream(long length) : Stream
{
    private long _position;

    /// <summary>How many bytes this stream has handed out so far.</summary>
    public long TotalBytesServed { get; private set; }

    public override bool CanRead => true;

    public override bool CanSeek => false;

    public override bool CanWrite => false;

    public override long Length => length;

    public override long Position
    {
        get => _position;
        set => throw new NotSupportedException();
    }

    public override int Read(byte[] buffer, int offset, int count)
    {
        var remaining = length - _position;
        var served = (int)Math.Min(count, Math.Max(0, remaining));

        // The buffer already contains zeros from allocation; there is nothing
        // sensitive to fake here, only a count of how much was asked for.
        _position += served;
        TotalBytesServed += served;

        return served;
    }

    public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken) =>
        Task.FromResult(Read(buffer, offset, count));

    public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default) =>
        ValueTask.FromResult(ReadSpan(buffer.Span));

    public override int Read(Span<byte> buffer) => ReadSpan(buffer);

    private int ReadSpan(Span<byte> buffer)
    {
        var remaining = length - _position;
        var served = (int)Math.Min(buffer.Length, Math.Max(0, remaining));

        buffer[..served].Clear();
        _position += served;
        TotalBytesServed += served;

        return served;
    }

    public override void Flush() => throw new NotSupportedException();

    public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

    public override void SetLength(long value) => throw new NotSupportedException();

    public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
}
