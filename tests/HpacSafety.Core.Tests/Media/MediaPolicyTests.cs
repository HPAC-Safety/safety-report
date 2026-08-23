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
    public void Given_a_jpeg_that_really_is_a_jpeg_When_it_is_validated_Then_it_is_accepted()
    {
        // Given / When
        var result = Policy.Validate("image/jpeg", MediaType.Jpeg, byteSize: 500);

        // Then
        result.IsAccepted.ShouldBeTrue();
        result.RejectionReason.ShouldBe(MediaRejectionReason.None);
        result.Type.ShouldBe(MediaType.Jpeg);
    }

    [Fact]
    public void Given_a_file_claiming_image_jpeg_but_containing_a_png_When_it_is_validated_Then_it_is_rejected()
    {
        // Given / When
        var result = Policy.Validate("image/jpeg", MediaType.Png, byteSize: 500);

        // Then
        result.IsAccepted.ShouldBeFalse();
        result.RejectionReason.ShouldBe(MediaRejectionReason.DeclaredTypeMismatch);
    }

    [Fact]
    public void Given_a_file_claiming_image_jpeg_but_containing_something_unrecognisable_When_it_is_validated_Then_it_is_rejected()
    {
        // Given / When
        var result = Policy.Validate("image/jpeg", sniffed: null, byteSize: 500);

        // Then
        result.IsAccepted.ShouldBeFalse();
        result.RejectionReason.ShouldBe(MediaRejectionReason.UnrecognisedContent);
    }

    [Fact]
    public void Given_a_file_larger_than_the_limit_When_it_is_validated_Then_it_is_rejected()
    {
        // Given / When
        var result = Policy.Validate("image/jpeg", MediaType.Jpeg, byteSize: 1_001);

        // Then
        result.IsAccepted.ShouldBeFalse();
        result.RejectionReason.ShouldBe(MediaRejectionReason.TooLarge);
    }

    [Fact]
    public void Given_an_empty_file_When_it_is_validated_Then_it_is_rejected()
    {
        // Given / When
        var result = Policy.Validate("image/jpeg", MediaType.Jpeg, byteSize: 0);

        // Then
        result.IsAccepted.ShouldBeFalse();
        result.RejectionReason.ShouldBe(MediaRejectionReason.Empty);
    }

    [Fact]
    public void Given_a_type_this_deployment_does_not_accept_When_it_is_validated_Then_it_is_rejected()
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
