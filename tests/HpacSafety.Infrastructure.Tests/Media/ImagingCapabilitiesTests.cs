using HpacSafety.Core.Features.Reporting;
using HpacSafety.Infrastructure.Media;
using ImageMagick;
using Shouldly;

namespace HpacSafety.Infrastructure.Tests.Media;

/// <summary>
///     Proves the runtime can actually do what this deployment promises.
///     <para>
///         Magick.NET ships native binaries per platform and the delegates compiled into
///         them are not guaranteed to be identical everywhere. If libheif were missing,
///         every iPhone reporter's upload would be refused as unrecognisable content and
///         nothing would say why. These tests are what turn that into a red build on the
///         machine that lacks it rather than a mystery in production.
///     </para>
/// </summary>
public class ImagingCapabilitiesTests
{
    [Fact]
    public void GivenThisRuntime_WhenHeicSupportIsProbed_ThenLibheifCanDecode()
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
    public void GivenThisRuntime_WhenAcceptedImageFormatIsProbed_ThenCanBeDecoded(string contentType)
    {
        // Given
        var type = MediaType.Parse(contentType);

        // When
        var canDecode = ImagingCapabilities.CanDecode(type);

        // Then
        canDecode.ShouldBeTrue();
    }

    [Fact]
    public void GivenEveryTypeThisDeploymentAccepts_WhenStartupCheckRuns_ThenPasses()
    {
        // Given / When / Then
        Should.NotThrow(() => ImagingCapabilities.EnsureCanDecode(MediaType.All));
    }

    [Fact]
    public void GivenVideo_WhenProbed_ThenNoImagingCodecIsClaimedFor()
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
    public void GivenStripper_WhenConstructed_ThenVerifiesCodecsWillNeed()
    {
        // Given / When / Then
        // Construction is the startup check: a missing codec fails the process
        // rather than every upload of that format.
        Should.NotThrow(() => new MagickNetExifStripper(MediaType.All));
    }
}
