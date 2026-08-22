using System.Text;
using HpacSafety.Core.Features.Reporting;
using HpacSafety.Infrastructure.Media;
using ImageMagick;
using Shouldly;

namespace HpacSafety.Infrastructure.Tests.Media;

public class MagickNetExifStripperTests
{
    // "Exif" followed by two NULs - the APP1 marker that introduces an EXIF
    // block in a JPEG. Written as bytes rather than as a string literal because
    // two of them are NULs, which do not survive a copy-paste intact.
    private static ReadOnlySpan<byte> ExifApp1Marker => [0x45, 0x78, 0x69, 0x66, 0x00, 0x00];

    private readonly MagickNetExifStripper _stripper = new(MediaType.All);

    [Fact]
    public async Task Given_a_photo_with_GPS_EXIF_When_it_is_stripped_Then_no_metadata_profile_survives()
    {
        // Given
        var original = ExifFixtures.JpegWithGpsExif();
        using var originalImage = new MagickImage(original);
        originalImage.GetExifProfile()!.GetValue(ExifTag.GPSLatitude).ShouldNotBeNull();

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
    public async Task Given_a_photo_with_GPS_EXIF_When_it_is_stripped_Then_the_APP1_segment_and_its_ascii_are_gone_from_the_bytes()
    {
        // Given
        var original = ExifFixtures.JpegWithGpsExif();

        // The same assertions run against the original first. A byte-level check
        // that passes on both is a check that proves nothing, and that is exactly
        // how a redaction test rots.
        original.AsSpan().IndexOf(ExifApp1Marker).ShouldBeGreaterThanOrEqualTo(0);
        Encoding.ASCII.GetString(original).ShouldContain(ExifFixtures.CameraMake);
        Encoding.ASCII.GetString(original).ShouldContain(ExifFixtures.CapturedAt);

        // When
        using var source = new MemoryStream(original);
        using var destination = new MemoryStream();
        await _stripper.StripAsync(source, destination, MediaType.Jpeg, CancellationToken.None);

        // Then
        var derivative = destination.ToArray();
        derivative.AsSpan().IndexOf(ExifApp1Marker).ShouldBe(-1);
        Encoding.ASCII.GetString(derivative).ShouldNotContain(ExifFixtures.CameraMake);
        Encoding.ASCII.GetString(derivative).ShouldNotContain(ExifFixtures.CapturedAt);
    }

    [Fact]
    public async Task Given_a_heic_photo_with_GPS_EXIF_When_it_is_stripped_Then_the_derivative_is_a_jpeg_with_no_location_data()
    {
        // Given
        var original = ExifFixtures.HeicWithGpsExif();
        using (var originalImage = new MagickImage(original))
        {
            originalImage.Format.ShouldBe(MagickFormat.Heic);
            originalImage.GetExifProfile()!.GetValue(ExifTag.GPSLatitude).ShouldNotBeNull();
        }

        // When
        using var source = new MemoryStream(original);
        using var destination = new MemoryStream();
        await _stripper.StripAsync(source, destination, MediaType.Heic, CancellationToken.None);

        // Then
        // HEIC cannot be encoded here, and a reviewer needs something every
        // browser renders, so the derivative is a JPEG. See ADR-0025.
        var derivative = destination.ToArray();
        using var stripped = new MagickImage(derivative);
        stripped.Format.ShouldBe(MagickFormat.Jpeg);
        stripped.GetExifProfile().ShouldBeNull();
        derivative.AsSpan().IndexOf(ExifApp1Marker).ShouldBe(-1);
        Encoding.ASCII.GetString(derivative).ShouldNotContain(ExifFixtures.CameraMake);
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
    public async Task Given_a_video_When_it_is_handed_to_the_stripper_Then_it_refuses_rather_than_writing_a_derivative()
    {
        // Given
        using var source = new MemoryStream(ExifFixtures.Mp4());
        using var destination = new MemoryStream();

        // When / Then
        // Nothing can strip a video yet - see #65 - and producing a derivative
        // that had not been stripped would be the leak.
        await Should.ThrowAsync<NotSupportedException>(
            () => _stripper.StripAsync(source, destination, MediaType.Mp4, CancellationToken.None));
        destination.Length.ShouldBe(0);
    }

    [Fact]
    public async Task Given_bytes_that_are_not_the_declared_format_When_they_are_stripped_Then_it_throws_rather_than_writing_a_derivative()
    {
        // Given
        using var source = new MemoryStream(ExifFixtures.NotMedia());
        using var destination = new MemoryStream();

        // When / Then
        await Should.ThrowAsync<MagickException>(
            () => _stripper.StripAsync(source, destination, MediaType.Jpeg, CancellationToken.None));
    }
}
