using HpacSafety.Core.Features.Reporting;
using Shouldly;

namespace HpacSafety.Core.Tests.Media;

/// <summary>
///     The client's declared content type is evidence, never authority. See
///     docs/data-handling.md — "Content type is sniffed, not trusted from the client".
/// </summary>
public class MediaPolicyTests
{
	private static readonly MediaPolicy Policy = new(MediaSizeLimits.Uniform(1_000), MediaType.All);

	[Fact]
	public void GivenJpegReallyIsJpeg_WhenValidated_ThenAccepted()
	{
		// Given / When
		var result = Policy.Validate("image/jpeg", MediaType.Jpeg, 500);

		// Then
		result.IsAccepted.ShouldBeTrue();
		result.RejectionReason.ShouldBe(MediaRejectionReason.None);
		result.Type.ShouldBe(MediaType.Jpeg);
	}

	[Fact]
	public void GivenFileClaimingImageJpegButContainingPng_WhenValidated_ThenRejected()
	{
		// Given / When
		var result = Policy.Validate("image/jpeg", MediaType.Png, 500);

		// Then
		result.IsAccepted.ShouldBeFalse();
		result.RejectionReason.ShouldBe(MediaRejectionReason.DeclaredTypeMismatch);
	}

	[Fact]
	public void GivenFileClaimingImageJpegButContainingSomethingUnrecognisable_WhenValidated_ThenRejected()
	{
		// Given / When
		var result = Policy.Validate("image/jpeg", null, 500);

		// Then
		result.IsAccepted.ShouldBeFalse();
		result.RejectionReason.ShouldBe(MediaRejectionReason.UnrecognisedContent);
	}

	[Fact]
	public void GivenFileLargerThanLimit_WhenValidated_ThenRejected()
	{
		// Given / When
		var result = Policy.Validate("image/jpeg", MediaType.Jpeg, 1_001);

		// Then
		result.IsAccepted.ShouldBeFalse();
		result.RejectionReason.ShouldBe(MediaRejectionReason.TooLarge);
	}

	[Fact]
	public void GivenEmptyFile_WhenValidated_ThenRejected()
	{
		// Given / When
		var result = Policy.Validate("image/jpeg", MediaType.Jpeg, 0);

		// Then
		result.IsAccepted.ShouldBeFalse();
		result.RejectionReason.ShouldBe(MediaRejectionReason.Empty);
	}

	[Fact]
	public void GivenTypeThisDeploymentDoesNotAccept_WhenValidated_ThenRejected()
	{
		// Given
		var jpegOnly = new MediaPolicy(MediaSizeLimits.Uniform(1_000), [MediaType.Jpeg]);

		// When
		var result = jpegOnly.Validate("image/png", MediaType.Png, 500);

		// Then
		result.IsAccepted.ShouldBeFalse();
		result.RejectionReason.ShouldBe(MediaRejectionReason.UnacceptedMediaType);
	}

	private static readonly MediaPolicy PerKind = new(new MediaSizeLimits(image: 100, video: 1_000, document: 200), MediaType.All);

	[Theory]
	[InlineData("video/mp4", 1_000)]
	[InlineData("image/png", 100)]
	[InlineData("application/pdf", 200)]
	public void GivenDeclarationAtItsKindsLimit_WhenJudged_ThenAcceptedAsDeclaredType(string declared,
																						 long byteSize)
	{
		// Given / When
		var result = PerKind.JudgeDeclaration(declared, byteSize);

		// Then
		result.IsAccepted.ShouldBeTrue();
		result.Type.ShouldBe(MediaType.Parse(declared));
	}

	[Theory]
	[InlineData("video/mp4", 1_001)]
	[InlineData("image/png", 101)]
	[InlineData("application/pdf", 201)]
	public void GivenDeclarationOneBytePastItsKindsLimit_WhenJudged_ThenTooLarge(string declared,
																				 long byteSize)
	{
		// Given / When
		var result = PerKind.JudgeDeclaration(declared, byteSize);

		// Then
		result.RejectionReason.ShouldBe(MediaRejectionReason.TooLarge);
	}

	[Theory]
	[InlineData(null)]
	[InlineData("")]
	[InlineData("application/zip")]
	[InlineData("text/html")]
	public void GivenDeclaredTypeOffAllowlist_WhenJudged_ThenUnacceptedMediaType(string? declared)
	{
		// Given / When
		var result = PerKind.JudgeDeclaration(declared, 10);

		// Then
		result.RejectionReason.ShouldBe(MediaRejectionReason.UnacceptedMediaType);
	}

	[Fact]
	public void GivenDeclarationOfZeroBytes_WhenJudged_ThenEmpty()
	{
		// Given / When
		var result = PerKind.JudgeDeclaration("image/jpeg", 0);

		// Then
		result.RejectionReason.ShouldBe(MediaRejectionReason.Empty);
	}

	[Fact]
	public void GivenTypeDeploymentDoesNotAccept_WhenDeclarationJudged_ThenUnacceptedMediaType()
	{
		// Given
		var jpegOnly = new MediaPolicy(MediaSizeLimits.Uniform(1_000), [MediaType.Jpeg]);

		// When
		var result = jpegOnly.JudgeDeclaration("image/png", 10);

		// Then
		result.RejectionReason.ShouldBe(MediaRejectionReason.UnacceptedMediaType);
	}

	[Fact]
	public void GivenFileDeclaredAsVideoButSniffedAsImagePastImageLimit_WhenValidated_ThenTooLarge()
	{
		// Given / When — within the video limit it was signed for, past the image
		// limit of what it really is (REQ-SUB-075).
		var result = PerKind.Validate("video/mp4", MediaType.Png, 500);

		// Then
		result.RejectionReason.ShouldBe(MediaRejectionReason.TooLarge);
	}

	[Fact]
	public void GivenFileDeclaredAsVideoButSniffedAsImageWithinImageLimit_WhenValidated_ThenDeclaredTypeMismatch()
	{
		// Given / When
		var result = PerKind.Validate("video/mp4", MediaType.Png, 50);

		// Then
		result.RejectionReason.ShouldBe(MediaRejectionReason.DeclaredTypeMismatch);
	}

	[Fact]
	public void GivenUnrecognisedBytesPastEveryLimit_WhenValidated_ThenTooLarge()
	{
		// Given / When
		var result = PerKind.Validate("video/mp4", null, 1_001);

		// Then
		result.RejectionReason.ShouldBe(MediaRejectionReason.TooLarge);
	}

	[Fact]
	public void GivenVideoWithinVideoLimitButPastOthers_WhenValidated_ThenAccepted()
	{
		// Given / When
		var result = PerKind.Validate("video/mp4", MediaType.Mp4, 1_000);

		// Then
		result.IsAccepted.ShouldBeTrue();
	}
}
