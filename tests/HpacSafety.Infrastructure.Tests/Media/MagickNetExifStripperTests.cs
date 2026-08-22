using System.Text;
using HpacSafety.Core.Features.Reporting;
using HpacSafety.Infrastructure.Media;
using ImageMagick;
using Shouldly;

namespace HpacSafety.Infrastructure.Tests.Media;

public class MagickNetExifStripperTests
{
    private readonly MagickNetExifStripper _stripper = new();

    [Fact]
    public async Task Given_a_photo_with_GPS_EXIF_When_it_is_stripped_Then_no_exif_profile_survives()
    {
        // Given
        var original = ExifFixtures.JpegWithGpsExif();
        using var originalImage = new MagickImage(original);
        originalImage.GetExifProfile().ShouldNotBeNull();

        // When
        using var source = new MemoryStream(original);
        using var destination = new MemoryStream();
        await _stripper.StripAsync(source, destination, MediaType.Jpeg, CancellationToken.None);

        // Then
        using var stripped = new MagickImage(destination.ToArray());
        stripped.GetExifProfile().ShouldBeNull();
        stripped.GetXmpProfile().ShouldBeNull();
        stripped.GetIptcProfile().ShouldBeNull();
    }

    [Fact]
    public async Task Given_a_photo_with_GPS_EXIF_When_it_is_stripped_Then_the_derivative_is_still_a_readable_image()
    {
        // Given
        using var source = new MemoryStream(ExifFixtures.JpegWithGpsExif());
        using var destination = new MemoryStream();

        // When
        await _stripper.StripAsync(source, destination, MediaType.Jpeg, CancellationToken.None);

        // Then
        using var stripped = new MagickImage(destination.ToArray());
        stripped.Width.ShouldBe(64u);
        stripped.Height.ShouldBe(64u);
        stripped.Format.ShouldBe(MagickFormat.Jpeg);
    }

    [Fact]
    public async Task Given_a_photo_with_GPS_EXIF_When_it_is_stripped_Then_the_APP1_segment_and_its_ascii_are_gone_from_the_bytes()
    {
        // Given
        var original = ExifFixtures.JpegWithGpsExif();

        // The same assertions run against the original first. A byte-level check
        // that passes on both is a check that proves nothing, and that is exactly
        // how a redaction test rots.
        original.AsSpan().IndexOf("Exif\u0000\u0000"u8).ShouldBeGreaterThanOrEqualTo(0);
        Encoding.ASCII.GetString(original).ShouldContain(ExifFixtures.CameraMake);
        Encoding.ASCII.GetString(original).ShouldContain(ExifFixtures.CapturedAt);

        // When
        using var source = new MemoryStream(original);
        using var destination = new MemoryStream();
        await _stripper.StripAsync(source, destination, MediaType.Jpeg, CancellationToken.None);

        // Then
        var derivative = destination.ToArray();
        derivative.AsSpan().IndexOf("Exif\u0000\u0000"u8).ShouldBe(-1);
        Encoding.ASCII.GetString(derivative).ShouldNotContain(ExifFixtures.CameraMake);
        Encoding.ASCII.GetString(derivative).ShouldNotContain(ExifFixtures.CapturedAt);
    }

    [Fact]
    public async Task Given_bytes_that_are_not_the_declared_format_When_they_are_stripped_Then_it_throws_rather_than_writing_a_derivative()
    {
        // Given
        using var source = new MemoryStream(ExifFixtures.NotAnImage());
        using var destination = new MemoryStream();

        // When / Then
        await Should.ThrowAsync<MagickException>(
            () => _stripper.StripAsync(source, destination, MediaType.Jpeg, CancellationToken.None));
    }
}
