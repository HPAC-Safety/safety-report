using HpacSafety.Core.Features.Reporting;
using HpacSafety.Infrastructure.Media;
using Shouldly;

namespace HpacSafety.Infrastructure.Tests.Media;

public class VideoContainerSnifferTests
{
    private readonly VideoContainerSniffer _sniffer = new();

    [Fact]
    public async Task Given_an_mp4_When_it_is_sniffed_Then_it_is_reported_as_mp4()
    {
        // Given
        using var content = new MemoryStream(ExifFixtures.Mp4());

        // When
        var sniffed = await _sniffer.SniffAsync(content, CancellationToken.None);

        // Then
        sniffed.ShouldBe(MediaType.Mp4);
    }

    [Fact]
    public async Task Given_a_quicktime_file_When_it_is_sniffed_Then_it_is_reported_as_quicktime()
    {
        // Given
        using var content = new MemoryStream(ExifFixtures.QuickTime());

        // When
        var sniffed = await _sniffer.SniffAsync(content, CancellationToken.None);

        // Then
        sniffed.ShouldBe(MediaType.QuickTime);
    }

    [Fact]
    public async Task Given_a_heic_photo_When_it_is_sniffed_by_the_video_sniffer_Then_it_is_not_claimed_as_video()
    {
        // Given
        using var content = new MemoryStream(ExifFixtures.HeicWithGpsExif());

        // When
        var sniffed = await _sniffer.SniffAsync(content, CancellationToken.None);

        // Then
        // HEIC is the same container with a different brand. Claiming it here
        // would send a photo down a path that never strips its EXIF.
        sniffed.ShouldBeNull();
    }

    [Theory]
    [InlineData("image/jpeg")]
    [InlineData("application/pdf")]
    public async Task Given_something_that_is_not_a_container_When_it_is_sniffed_Then_it_is_unrecognised(string what)
    {
        // Given
        var bytes = what == "image/jpeg" ? ExifFixtures.JpegWithGpsExif() : ExifFixtures.NotMedia();
        using var content = new MemoryStream(bytes);

        // When
        var sniffed = await _sniffer.SniffAsync(content, CancellationToken.None);

        // Then
        sniffed.ShouldBeNull();
    }

    [Fact]
    public async Task Given_a_truncated_header_When_it_is_sniffed_Then_it_is_unrecognised()
    {
        // Given
        using var content = new MemoryStream([0, 0, 0, 0x18, (byte)'f', (byte)'t']);

        // When
        var sniffed = await _sniffer.SniffAsync(content, CancellationToken.None);

        // Then
        sniffed.ShouldBeNull();
    }
}
