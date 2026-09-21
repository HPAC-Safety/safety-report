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
    public async Task GivenJpegBytes_WhenTheyAreSniffed_ThenTheyAreReportedAsJpeg()
    {
        // Given
        using var content = new MemoryStream(ExifFixtures.JpegWithGpsExif());

        // When
        var sniffed = await _sniffer.SniffAsync(content, CancellationToken.None);

        // Then
        sniffed.ShouldBe(MediaType.Jpeg);
    }

    [Fact]
    public async Task GivenPngBytes_WhenTheyAreSniffed_ThenTheyAreReportedAsPng()
    {
        // Given
        using var content = new MemoryStream(ExifFixtures.Png());

        // When
        var sniffed = await _sniffer.SniffAsync(content, CancellationToken.None);

        // Then
        sniffed.ShouldBe(MediaType.Png);
    }

    [Fact]
    public async Task GivenHeicBytes_WhenTheyAreSniffed_ThenTheyAreReportedAsHeic()
    {
        // Given
        using var content = new MemoryStream(ExifFixtures.HeicWithGpsExif());

        // When
        var sniffed = await _sniffer.SniffAsync(content, CancellationToken.None);

        // Then
        sniffed.ShouldBe(MediaType.Heic);
    }

    [Fact]
    public async Task GivenMp4_WhenSniffedByImageSniffer_ThenLeftForVideoSniffer()
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
    public async Task GivenPdfRenamedToPhoto_WhenSniffed_ThenUnrecognised()
    {
        // Given
        using var content = new MemoryStream(ExifFixtures.NotMedia());

        // When
        var sniffed = await _sniffer.SniffAsync(content, CancellationToken.None);

        // Then
        sniffed.ShouldBeNull();
    }

    [Fact]
    public async Task GivenJpegMagicNumberGluedOntoRubbish_WhenSniffed_ThenUnrecognised()
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
    public async Task GivenEmptyStream_WhenSniffed_ThenUnrecognised()
    {
        // Given
        using var content = new MemoryStream();

        // When
        var sniffed = await _sniffer.SniffAsync(content, CancellationToken.None);

        // Then
        sniffed.ShouldBeNull();
    }
}
