using HpacSafety.Core.Features.Reporting;
using Shouldly;

namespace HpacSafety.Core.Tests.Media;

/// <summary>
/// The client's declared content type is evidence, never authority. See
/// docs/data-handling.md — "Content type is sniffed, not trusted from the client".
/// </summary>
public class MediaPolicyTests
{
    private static readonly MediaPolicy Policy = new(maxByteSize: 1_000, MediaType.All);

    [Fact]
    public void GivenJpegReallyIsJpeg_WhenValidated_ThenAccepted()
    {
        // Given / When
        var result = Policy.Validate("image/jpeg", MediaType.Jpeg, byteSize: 500);

        // Then
        result.IsAccepted.ShouldBeTrue();
        result.RejectionReason.ShouldBe(MediaRejectionReason.None);
        result.Type.ShouldBe(MediaType.Jpeg);
    }

    [Fact]
    public void GivenFileClaimingImageJpegButContainingPng_WhenValidated_ThenRejected()
    {
        // Given / When
        var result = Policy.Validate("image/jpeg", MediaType.Png, byteSize: 500);

        // Then
        result.IsAccepted.ShouldBeFalse();
        result.RejectionReason.ShouldBe(MediaRejectionReason.DeclaredTypeMismatch);
    }

    [Fact]
    public void GivenFileClaimingImageJpegButContainingSomethingUnrecognisable_WhenValidated_ThenRejected()
    {
        // Given / When
        var result = Policy.Validate("image/jpeg", sniffed: null, byteSize: 500);

        // Then
        result.IsAccepted.ShouldBeFalse();
        result.RejectionReason.ShouldBe(MediaRejectionReason.UnrecognisedContent);
    }

    [Fact]
    public void GivenFileLargerThanLimit_WhenValidated_ThenRejected()
    {
        // Given / When
        var result = Policy.Validate("image/jpeg", MediaType.Jpeg, byteSize: 1_001);

        // Then
        result.IsAccepted.ShouldBeFalse();
        result.RejectionReason.ShouldBe(MediaRejectionReason.TooLarge);
    }

    [Fact]
    public void GivenEmptyFile_WhenValidated_ThenRejected()
    {
        // Given / When
        var result = Policy.Validate("image/jpeg", MediaType.Jpeg, byteSize: 0);

        // Then
        result.IsAccepted.ShouldBeFalse();
        result.RejectionReason.ShouldBe(MediaRejectionReason.Empty);
    }

    [Fact]
    public void GivenTypeThisDeploymentDoesNotAccept_WhenValidated_ThenRejected()
    {
        // Given
        var jpegOnly = new MediaPolicy(maxByteSize: 1_000, [MediaType.Jpeg]);

        // When
        var result = jpegOnly.Validate("image/png", MediaType.Png, byteSize: 500);

        // Then
        result.IsAccepted.ShouldBeFalse();
        result.RejectionReason.ShouldBe(MediaRejectionReason.UnacceptedMediaType);
    }

}
