namespace HpacSafety.Core;

/// <summary>
///     A read-only, seekable view of one stored object that fetches only the ranges a
///     reader actually asks for, one bounded window at a time.
///     <para>
///         How a submission sniffs an upload it claims without downloading it
///         (ADR-0126). A signature check reads a few leading bytes; a DOCX or ODT is
///         a zip, whose directory sits at its end, so the sniffer seeks there and
///         reads that too. Neither pulls the file, and at most one window is ever
///         held in memory.
///     </para>
/// </summary>
public sealed class BlobRangeStream : Stream
{
	/// <summary>How much one fetch asks storage for: enough that a header parse is one or two requests.</summary>
	public const int DefaultWindowSize = 64 * 1024;

	private readonly IBlobStore _blobStore;
	private readonly BlobKey _key;
	private readonly long _length;
	private readonly int _windowSize;
	private byte[] _window = [];
	private long _windowStart;
	private long _position;

	/// <summary>Creates a view of <paramref name="key" />, whose stored size the caller already knows.</summary>
	/// <param name="blobStore">Where the object lives.</param>
	/// <param name="key">The object.</param>
	/// <param name="length">Its stored size, from <see cref="IBlobStore.Describe" />.</param>
	/// <param name="windowSize">How many bytes one fetch asks for.</param>
	public BlobRangeStream(IBlobStore blobStore,
						   BlobKey key,
						   long length,
						   int windowSize = DefaultWindowSize)
	{
		ArgumentNullException.ThrowIfNull(blobStore);
		ArgumentOutOfRangeException.ThrowIfNegative(length);
		ArgumentOutOfRangeException.ThrowIfNegativeOrZero(windowSize);

		_blobStore = blobStore;
		_key = key;
		_length = length;
		_windowSize = windowSize;
	}

	/// <summary>How many bytes this stream has fetched from storage in all.</summary>
	public long BytesFetched { get; private set; }

	/// <inheritdoc />
	public override bool CanRead => true;

	/// <inheritdoc />
	public override bool CanSeek => true;

	/// <inheritdoc />
	public override bool CanWrite => false;

	/// <inheritdoc />
	public override long Length => _length;

	/// <inheritdoc />
	public override long Position
	{
		get => _position;
		set => _position = value >= 0 ? value : throw new ArgumentOutOfRangeException(nameof(value));
	}

	/// <inheritdoc />
	public override int Read(byte[] buffer,
							 int offset,
							 int count)
	{
		return Read(buffer.AsSpan(offset, count));
	}

	/// <inheritdoc />
	public override int Read(Span<byte> buffer)
	{
		// Synchronous readers exist — a zip archive and an imaging library both
		// read this way — and there is no async path to offer them.
		if (!EnsureWindowHolds(_position))
		{
			if (_position >= _length)
			{
				return 0;
			}

			Fetch(_position, CancellationToken.None).GetAwaiter().GetResult();
		}

		return CopyFromWindow(buffer);
	}

	/// <inheritdoc />
	public override async ValueTask<int> ReadAsync(Memory<byte> buffer,
												   CancellationToken cancellationToken = default)
	{
		if (!EnsureWindowHolds(_position))
		{
			if (_position >= _length)
			{
				return 0;
			}

			await Fetch(_position, cancellationToken).ConfigureAwait(false);
		}

		return CopyFromWindow(buffer.Span);
	}

	/// <inheritdoc />
	public override Task<int> ReadAsync(byte[] buffer,
										int offset,
										int count,
										CancellationToken cancellationToken)
	{
		return ReadAsync(buffer.AsMemory(offset, count), cancellationToken).AsTask();
	}

	/// <inheritdoc />
	public override long Seek(long offset,
							  SeekOrigin origin)
	{
		Position = origin switch
		{
			SeekOrigin.Begin => offset,
			SeekOrigin.Current => _position + offset,
			SeekOrigin.End => _length + offset,
			_ => throw new ArgumentOutOfRangeException(nameof(origin)),
		};

		return _position;
	}

	/// <inheritdoc />
	public override void Flush()
	{
	}

	/// <inheritdoc />
	public override void SetLength(long value)
	{
		throw new NotSupportedException();
	}

	/// <inheritdoc />
	public override void Write(byte[] buffer,
							   int offset,
							   int count)
	{
		throw new NotSupportedException();
	}

	private bool EnsureWindowHolds(long position)
	{
		return position >= _windowStart && position < _windowStart + _window.Length;
	}

	private int CopyFromWindow(Span<byte> buffer)
	{
		var available = _window.AsSpan((int)(_position - _windowStart));
		var copied = Math.Min(available.Length, buffer.Length);
		available[..copied].CopyTo(buffer);
		_position += copied;
		return copied;
	}

	private async Task Fetch(long start,
							 CancellationToken cancellationToken)
	{
		var length = (int)Math.Min(_windowSize, _length - start);
		var window = new byte[length];

		await using (var range = await _blobStore.OpenReadRange(_key, start, length, cancellationToken).ConfigureAwait(false))
		{
			var read = await range.ReadAtLeastAsync(window, length, throwOnEndOfStream: false, cancellationToken)
				.ConfigureAwait(false);

			// The object is shorter than it was described as: what storage holds
			// is what the reader sees, never zero-filled bytes that were never sent.
			if (read < length)
			{
				Array.Resize(ref window, read);
			}
		}

		BytesFetched += window.Length;
		_window = window;
		_windowStart = start;
	}
}
