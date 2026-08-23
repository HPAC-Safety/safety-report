using HpacSafety.Core.Features.Reporting;
using HpacSafety.Infrastructure.Media;
using ImageMagick;
using Shouldly;

namespace HpacSafety.Infrastructure.Tests.Media;

/// <summary>
/// Proves the runtime can actually do what this deployment promises.
/// <para>
/// Magick.NET ships native binaries per platform and the delegates compiled into
/// them are not guaranteed to be identical everywhere. If libheif were missing,
/// every iPhone reporter's upload would be refused as unrecognisable content and
/// nothing would say why. These tests are what turn that into a red build on the
/// machine that lacks it rather than a mystery in production.
/// </para>
/// </summary>
public class ImagingCapabilitiesTests
{
    [Fact]
    public void Given_this_runtime_When_heic_support_is_probed_Then_libheif_can_decode_it()
    {
        // Given / When
        var canDecode = ImagingCapabilities.CanDecode(MediaType.Heic);

        // Then
        // If this fails, the deployment must stop accepting HEIC or ship a
        // Magick.NET build that has libheif. Do not make it conditional.
        canDecode.ShouldBeTrue(
            $"HEIC is accepted by this deployment but this runtime cannot decode it. Imaging library: {MagickNET.Version}");
    }

    [Theory]
    [InlineData("image/jpeg")]
    [InlineData("image/png")]
    [InlineData("image/webp")]
    public void Given_this_runtime_When_an_accepted_image_format_is_probed_Then_it_can_be_decoded(string contentType)
    {
        // Given
        var type = MediaType.Parse(contentType);

        // When
        var canDecode = ImagingCapabilities.CanDecode(type);

        // Then
        canDecode.ShouldBeTrue();
    }

    [Fact]
    public void Given_every_type_this_deployment_accepts_When_the_startup_check_runs_Then_it_passes()
    {
        // Given / When / Then
        Should.NotThrow(() => ImagingCapabilities.EnsureCanDecode(MediaType.All));
    }

    [Fact]
    public void Given_a_video_When_it_is_probed_Then_no_imaging_codec_is_claimed_for_it()
    {
        // Given / When
        var canDecode = ImagingCapabilities.CanDecode(MediaType.Mp4);

        // Then
        // ImageMagick will report MP4 as readable because it can shell out to a
        // delegate. Nothing here does that on purpose — video is never handed to
        // an imaging library. See ADR-0025.
        canDecode.ShouldBeFalse();
    }

    [Fact]
    public void Given_the_stripper_When_it_is_constructed_Then_it_verifies_the_codecs_it_will_need()
    {
        // Given / When / Then
        // Construction is the startup check: a missing codec fails the process
        // rather than every upload of that format.
        Should.NotThrow(() => new MagickNetExifStripper(MediaType.All));
    }
}
