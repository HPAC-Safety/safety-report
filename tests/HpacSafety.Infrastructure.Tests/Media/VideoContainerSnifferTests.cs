using HpacSafety.Core.Features.Reporting;
using HpacSafety.Infrastructure.Media;
using Shouldly;

namespace HpacSafety.Infrastructure.Tests.Media;

public class VideoContainerSnifferTests
{
	private readonly VideoContainerSniffer _sniffer = new();

	[Fact]
	public async Task GivenMp4_WhenSniffed_ThenReportedAsMp4()
	{
		// Given
		using var content = new MemoryStream(ExifFixtures.Mp4());

		// When
		var sniffed = await _sniffer.Sniff(content, CancellationToken.None);

		// Then
		sniffed.ShouldBe(MediaType.Mp4);
	}

	[Fact]
	public async Task GivenQuicktimeFile_WhenSniffed_ThenReportedAsQuicktime()
	{
		// Given
		using var content = new MemoryStream(ExifFixtures.QuickTime());

		// When
		var sniffed = await _sniffer.Sniff(content, CancellationToken.None);

		// Then
		sniffed.ShouldBe(MediaType.QuickTime);
	}

	[Fact]
	public async Task GivenHeicPhoto_WhenSniffedByVideoSniffer_ThenNotClaimedAsVideo()
	{
		// Given
		using var content = new MemoryStream(ExifFixtures.HeicWithGpsExif());

		// When
		var sniffed = await _sniffer.Sniff(content, CancellationToken.None);

		// Then
		// HEIC is the same container with a different brand. Claiming it here
		// would send a photo down a path that never strips its EXIF.
		sniffed.ShouldBeNull();
	}

	[Theory]
	[InlineData("image/jpeg")]
	[InlineData("application/pdf")]
	public async Task GivenSomethingIsNotContainer_WhenSniffed_ThenUnrecognised(string what)
	{
		// Given
		var bytes = what == "image/jpeg" ? ExifFixtures.JpegWithGpsExif() : ExifFixtures.NotMedia();
		using var content = new MemoryStream(bytes);

		// When
		var sniffed = await _sniffer.Sniff(content, CancellationToken.None);

		// Then
		sniffed.ShouldBeNull();
	}

	[Fact]
	public async Task GivenTruncatedHeader_WhenSniffed_ThenUnrecognised()
	{
		// Given
		using var content = new MemoryStream([0, 0, 0, 0x18, (byte)'f', (byte)'t']);

		// When
		var sniffed = await _sniffer.Sniff(content, CancellationToken.None);

		// Then
		sniffed.ShouldBeNull();
	}
}
