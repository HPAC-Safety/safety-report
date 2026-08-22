using HpacSafety.Core.Features.Reporting;
using HpacSafety.Infrastructure.Media;
using Shouldly;

namespace HpacSafety.Infrastructure.Tests.Media;

/// <summary>
/// "Content type is sniffed, not trusted from the client" — docs/data-handling.md.
/// </summary>
public class MagickNetMediaSnifferTests
{
    private readonly MagickNetMediaSniffer _sniffer = new();

    [Fact]
    public async Task Given_jpeg_bytes_When_they_are_sniffed_Then_they_are_reported_as_jpeg()
    {
        // Given
        using var content = new MemoryStream(ExifFixtures.JpegWithGpsExif());

        // When
        var sniffed = await _sniffer.SniffAsync(content, CancellationToken.None);

        // Then
        sniffed.ShouldBe(MediaType.Jpeg);
    }

    [Fact]
    public async Task Given_png_bytes_When_they_are_sniffed_Then_they_are_reported_as_png()
    {
        // Given
        using var content = new MemoryStream(ExifFixtures.Png());

        // When
        var sniffed = await _sniffer.SniffAsync(content, CancellationToken.None);

        // Then
        sniffed.ShouldBe(MediaType.Png);
    }

    [Fact]
    public async Task Given_heic_bytes_When_they_are_sniffed_Then_they_are_reported_as_heic()
    {
        // Given
        using var content = new MemoryStream(ExifFixtures.HeicWithGpsExif());

        // When
        var sniffed = await _sniffer.SniffAsync(content, CancellationToken.None);

        // Then
        sniffed.ShouldBe(MediaType.Heic);
    }

    [Fact]
    public async Task Given_an_mp4_When_it_is_sniffed_by_the_image_sniffer_Then_it_is_left_for_the_video_sniffer()
    {
        // Given
        using var content = new MemoryStream(ExifFixtures.Mp4());

        // When
        var sniffed = await _sniffer.SniffAsync(content, CancellationToken.None);

        // Then
        // MP4 and HEIC share the ISO base media container and differ only by
        // brand. This sniffer must not claim a video, or ImageMagick would end up
        // decoding one.
        sniffed.ShouldBeNull();
    }

    [Fact]
    public async Task Given_a_pdf_renamed_to_a_photo_When_it_is_sniffed_Then_it_is_unrecognised()
    {
        // Given
        using var content = new MemoryStream(ExifFixtures.NotMedia());

        // When
        var sniffed = await _sniffer.SniffAsync(content, CancellationToken.None);

        // Then
        sniffed.ShouldBeNull();
    }

    [Fact]
    public async Task Given_a_jpeg_magic_number_glued_onto_rubbish_When_it_is_sniffed_Then_it_is_unrecognised()
    {
        // Given
        // The leading bytes say JPEG; nothing after them does. Magic numbers alone
        // are not enough, which is why the sniffer also parses the header.
        var bytes = new byte[] { 0xFF, 0xD8, 0xFF }.Concat(new byte[64]).ToArray();
        using var content = new MemoryStream(bytes);

        // When
        var sniffed = await _sniffer.SniffAsync(content, CancellationToken.None);

        // Then
        sniffed.ShouldBeNull();
    }

    [Fact]
    public async Task Given_an_empty_stream_When_it_is_sniffed_Then_it_is_unrecognised()
    {
        // Given
        using var content = new MemoryStream();

        // When
        var sniffed = await _sniffer.SniffAsync(content, CancellationToken.None);

        // Then
        sniffed.ShouldBeNull();
    }
}
