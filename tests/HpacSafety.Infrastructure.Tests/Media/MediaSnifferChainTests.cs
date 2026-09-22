using HpacSafety.Core.Features.Reporting;
using HpacSafety.Infrastructure.Media;
using Shouldly;

namespace HpacSafety.Infrastructure.Tests.Media;

/// <summary>
///     The chain is what lets images and video be identified by different means
///     without the caller knowing there is more than one sniffer.
/// </summary>
public class MediaSnifferChainTests
{
	private readonly MediaSnifferChain _chain = MediaSnifferChain.Default();

	[Fact]
	public async Task GivenHeicPhoto_WhenChainSniffs_ThenImageLinkAnswersBeforeVideoLink()
	{
		// Given
		using var content = new MemoryStream(ExifFixtures.HeicWithGpsExif());

		// When
		var sniffed = await _chain.SniffAsync(content, CancellationToken.None);

		// Then
		// HEIC and MP4 share a container. Order is the only thing keeping a photo
		// out of the video path.
		sniffed.ShouldBe(MediaType.Heic);
	}

	[Fact]
	public async Task GivenVideo_WhenChainSniffs_ThenLaterLinkStillGetsWholeStream()
	{
		// Given
		// The image link runs first and consumes the stream. If the chain did not
		// rewind, video would silently stop being recognised.
		using var content = new MemoryStream(ExifFixtures.Mp4());

		// When
		var sniffed = await _chain.SniffAsync(content, CancellationToken.None);

		// Then
		sniffed.ShouldBe(MediaType.Mp4);
	}

	[Fact]
	public async Task GivenJpeg_WhenChainSniffs_ThenRecognised()
	{
		// Given
		using var content = new MemoryStream(ExifFixtures.JpegWithGpsExif());

		// When
		var sniffed = await _chain.SniffAsync(content, CancellationToken.None);

		// Then
		sniffed.ShouldBe(MediaType.Jpeg);
	}

	[Fact]
	public async Task GivenSomethingNoLinkRecognises_WhenChainSniffs_ThenUnrecognised()
	{
		// Given
		using var content = new MemoryStream(ExifFixtures.NotMedia());

		// When
		var sniffed = await _chain.SniffAsync(content, CancellationToken.None);

		// Then
		sniffed.ShouldBeNull();
	}

	[Fact]
	public void GivenChainWithNoLinks_WhenBuilt_ThenRefused()
	{
		// Given / When / Then
		// A chain that recognises nothing would reject every upload.
		Should.Throw<ArgumentException>(() => new MediaSnifferChain());
	}
}
