using HpacSafety.Core.Features.Reporting;
using Shouldly;

namespace HpacSafety.Core.Tests.Media;

public class MediaTypeTests
{
	[Fact]
	public void GivenAcceptedContentType_WhenParsed_ThenRecognised()
	{
		// Given
		const string declared = "image/jpeg";

		// When
		var parsed = MediaType.TryParse(declared, out var type);

		// Then
		parsed.ShouldBeTrue();
		type.ShouldBe(MediaType.Jpeg);
		type.Extension.ShouldBe("jpg");
	}

	[Fact]
	public void GivenContentTypeWithParametersAndCasing_WhenParsed_ThenRecognised()
	{
		// Given
		const string declared = "IMAGE/JPEG; charset=binary";

		// When
		var parsed = MediaType.TryParse(declared, out var type);

		// Then
		parsed.ShouldBeTrue();
		type.ShouldBe(MediaType.Jpeg);
	}

	[Theory]
	[InlineData("application/pdf")]
	[InlineData("image/svg+xml")]
	[InlineData("image/gif")]
	[InlineData("video/x-matroska")]
	[InlineData("")]
	[InlineData(null)]
	public void GivenContentTypeThisSystemDoesNotAccept_WhenParsed_ThenRefused(string? declared)
	{
		// Given / When
		var parsed = MediaType.TryParse(declared, out _);

		// Then
		parsed.ShouldBeFalse();
		Should.Throw<DomainRuleViolationException>(() => MediaType.Parse(declared));
	}

	[Theory]
	[InlineData("image/heic")]
	[InlineData("video/mp4")]
	[InlineData("video/quicktime")]
	public void GivenFormatPhoneProducesByDefault_WhenParsed_ThenAccepted(string declared)
	{
		// Given / When
		var parsed = MediaType.TryParse(declared, out _);

		// Then
		parsed.ShouldBeTrue();
	}

	[Fact]
	public void GivenHeicPhoto_WhenStrippedFormIsRead_ThenJpeg()
	{
		// Given / When
		var strippedForm = MediaType.Heic.StrippedForm;

		// Then
		// The runtime imaging library decodes HEIC but cannot encode it, and a
		// reviewer needs something every browser renders. See ADR-0025.
		strippedForm.ShouldBe(MediaType.Jpeg);
		MediaType.Heic.CanBeStripped.ShouldBeTrue();
	}

	[Theory]
	[InlineData("image/jpeg")]
	[InlineData("image/png")]
	[InlineData("image/webp")]
	public void GivenOrdinaryImage_WhenStrippedFormIsRead_ThenKeepsOwnFormat(string declared)
	{
		// Given
		var type = MediaType.Parse(declared);

		// When
		var strippedForm = type.StrippedForm;

		// Then
		strippedForm.ShouldBe(type);
	}

	[Theory]
	[InlineData("video/mp4")]
	[InlineData("video/quicktime")]
	public void GivenVideo_WhenStrippedFormIsRead_ThenNone(string declared)
	{
		// Given
		var type = MediaType.Parse(declared);

		// When
		var strippedForm = type.StrippedForm;

		// Then
		// Accepted and retained, but nothing can strip it yet — see #65. A
		// reviewer sees nothing for it rather than something unsafe.
		strippedForm.ShouldBeNull();
		type.CanBeStripped.ShouldBeFalse();
		type.Kind.ShouldBe(MediaKind.Video);
	}

	[Fact]
	public void GivenStrippableSet_WhenRead_ThenEveryAcceptedTypeHasStrippedForm()
	{
		// Given / When
		var strippable = MediaType.Strippable;

		// Then
		strippable.ShouldBe(MediaType.All.Where(t => t.CanBeStripped).ToArray(), true);
		strippable.ShouldAllBe(t => t.Kind == MediaKind.Image);
	}

	[Fact]
	public void GivenAcceptedSet_WhenRead_ThenEveryMemberIsImageOrVideo()
	{
		// Given / When
		var all = MediaType.All;

		// Then
		all.ShouldContain(MediaType.Jpeg);
		all.ShouldContain(MediaType.Heic);
		all.ShouldContain(MediaType.Mp4);
		all.ShouldAllBe(t => t.ContentType.StartsWith("image/", StringComparison.Ordinal)
							 || t.ContentType.StartsWith("video/", StringComparison.Ordinal));
	}
}
