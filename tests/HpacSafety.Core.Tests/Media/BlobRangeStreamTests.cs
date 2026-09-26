using Shouldly;

namespace HpacSafety.Core.Tests.Media;

/// <summary>
///     A claim sniffs an upload through ranged reads, fetching only what the sniffer
///     asks for (ADR-0126, ADR-0134).
/// </summary>
public class BlobRangeStreamTests
{
	private static readonly BlobKey Key = BlobKey.ForUpload(UploadId.New());

	private static (InMemoryBlobStore Store, byte[] Content) Stored(int length)
	{
		var content = new byte[length];
		for (var index = 0; index < length; index++)
		{
			content[index] = (byte)(index % 251);
		}

		var store = new InMemoryBlobStore();
		store.Seed(Key, content);
		return (store, content);
	}

	[Fact]
	public async Task GivenLargeObject_WhenLeadingBytesRead_ThenOnlyOneWindowIsFetched()
	{
		// Given
		var (store, content) = Stored(1_000_000);
		await using var stream = new BlobRangeStream(store, Key, content.Length, windowSize: 4096);
		var header = new byte[16];

		// When
		var read = await stream.ReadAsync(header);

		// Then
		read.ShouldBe(16);
		header.ShouldBe(content[..16]);
		stream.BytesFetched.ShouldBe(4096);
		store.RangesRead.ShouldBe([(Key.Value, 0L, 4096L)]);
	}

	[Fact]
	public async Task GivenLargeObject_WhenReaderSeeksToEnd_ThenOnlyHeadAndTailAreFetched()
	{
		// Given
		var (store, content) = Stored(1_000_000);
		await using var stream = new BlobRangeStream(store, Key, content.Length, windowSize: 4096);
		var buffer = new byte[22];

		// When — the way a zip reader finds its directory.
		_ = await stream.ReadAsync(buffer.AsMemory(0, 4));
		stream.Seek(-22, SeekOrigin.End);
		var read = stream.Read(buffer, 0, 22);

		// Then
		read.ShouldBe(22);
		buffer.ShouldBe(content[^22..]);
		stream.BytesFetched.ShouldBe(4096 + 22);
	}

	[Fact]
	public async Task GivenWholeObjectReadInChunks_WhenCopied_ThenEveryByteArrivesOnceAndInOrder()
	{
		// Given
		var (store, content) = Stored(10_000);
		await using var stream = new BlobRangeStream(store, Key, content.Length, windowSize: 3000);
		using var copy = new MemoryStream();

		// When
		await stream.CopyToAsync(copy);

		// Then
		copy.ToArray().ShouldBe(content);
		stream.BytesFetched.ShouldBe(content.Length);
	}

	[Fact]
	public void GivenPositionAtEnd_WhenRead_ThenNothingIsFetched()
	{
		// Given
		var (store, content) = Stored(100);
		using var stream = new BlobRangeStream(store, Key, content.Length);
		stream.Position = content.Length;

		// When
		var read = stream.Read(new byte[10], 0, 10);

		// Then
		read.ShouldBe(0);
		store.RangesRead.ShouldBeEmpty();
	}

	[Fact]
	public async Task GivenObjectShorterThanDescribed_WhenRead_ThenOnlyStoredBytesAreSeen()
	{
		// Given
		var (store, content) = Stored(10);
		await using var stream = new BlobRangeStream(store, Key, length: 20);
		using var copy = new MemoryStream();

		// When
		await stream.CopyToAsync(copy);

		// Then
		copy.ToArray().ShouldBe(content);
	}

	[Fact]
	public void GivenStream_WhenAskedWhatItSupports_ThenReadAndSeekOnly()
	{
		// Given
		var (store, _) = Stored(10);
		using var stream = new BlobRangeStream(store, Key, 10);

		// When / Then
		stream.CanRead.ShouldBeTrue();
		stream.CanSeek.ShouldBeTrue();
		stream.CanWrite.ShouldBeFalse();
		stream.Length.ShouldBe(10);
		stream.Seek(3, SeekOrigin.Begin).ShouldBe(3);
		stream.Seek(2, SeekOrigin.Current).ShouldBe(5);
		Should.Throw<ArgumentOutOfRangeException>(() => stream.Position = -1);
		Should.Throw<ArgumentOutOfRangeException>(() => stream.Seek(0, (SeekOrigin)9));
		Should.Throw<NotSupportedException>(() => stream.Write([1], 0, 1));
		Should.Throw<NotSupportedException>(() => stream.SetLength(1));
		stream.Flush();
	}

	[Fact]
	public async Task GivenArrayOverload_WhenReadAsync_ThenSameBytesAsMemoryOverload()
	{
		// Given
		var (store, content) = Stored(50);
		await using var stream = new BlobRangeStream(store, Key, content.Length);
		var buffer = new byte[10];

		// When
#pragma warning disable CA1835 // The array overload is the one under test.
		var read = await stream.ReadAsync(buffer, 0, 10, CancellationToken.None);
#pragma warning restore CA1835

		// Then
		read.ShouldBe(10);
		buffer.ShouldBe(content[..10]);
	}
}
