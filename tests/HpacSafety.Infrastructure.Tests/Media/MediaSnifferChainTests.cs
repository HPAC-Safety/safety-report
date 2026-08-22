using HpacSafety.Core.Features.Reporting;
using HpacSafety.Infrastructure.Media;
using Shouldly;

namespace HpacSafety.Infrastructure.Tests.Media;

/// <summary>
/// The chain is what lets images and video be identified by different means
/// without the caller knowing there is more than one sniffer.
/// </summary>
public class MediaSnifferChainTests
{
    private readonly MediaSnifferChain _chain = MediaSnifferChain.Default();

    [Fact]
    public async Task Given_a_heic_photo_When_the_chain_sniffs_it_Then_the_image_link_answers_before_the_video_link()
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
    public async Task Given_a_video_When_the_chain_sniffs_it_Then_a_later_link_still_gets_the_whole_stream()
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
    public async Task Given_a_jpeg_When_the_chain_sniffs_it_Then_it_is_recognised()
    {
        // Given
        using var content = new MemoryStream(ExifFixtures.JpegWithGpsExif());

        // When
        var sniffed = await _chain.SniffAsync(content, CancellationToken.None);

        // Then
        sniffed.ShouldBe(MediaType.Jpeg);
    }

    [Fact]
    public async Task Given_something_no_link_recognises_When_the_chain_sniffs_it_Then_it_is_unrecognised()
    {
        // Given
        using var content = new MemoryStream(ExifFixtures.NotMedia());

        // When
        var sniffed = await _chain.SniffAsync(content, CancellationToken.None);

        // Then
        sniffed.ShouldBeNull();
    }

    [Fact]
    public void Given_a_chain_with_no_links_When_it_is_built_Then_it_is_refused()
    {
        // Given / When / Then
        // A chain that recognises nothing would reject every upload.
        Should.Throw<ArgumentException>(() => new MediaSnifferChain());
    }
}
