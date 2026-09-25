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
	[InlineData("application/pdf-fake")]
	[InlineData("image/svg+xml")]
	[InlineData("image/gif")]
	[InlineData("video/x-matroska")]
	[InlineData("application/zip")]
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
	[InlineData("video/mp4", "video/mp4")]
	[InlineData("video/quicktime", "video/mp4")]
	[InlineData("image/heic", "image/jpeg")]
	[InlineData("image/png", "image/png")]
	public void GivenMediaType_WhenDerivativeFormIsRead_ThenItNamesTheDerivativesBytes(string declared,
																					string expected)
	{
		// Given — every video's derivative is an MP4 (ADR-0122); an image's is its stripped form
		var type = MediaType.Parse(declared);

		// When
		var derivative = type.DerivativeForm;

		// Then
		derivative.ShouldNotBeNull();
		derivative.Value.ContentType.ShouldBe(expected);
	}

	[Fact]
	public void GivenDocument_WhenDerivativeFormIsRead_ThenNone()
	{
		// Given / When / Then — a document is never transformed
		MediaType.Pdf.DerivativeForm.ShouldBeNull();
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
	public void GivenAcceptedSet_WhenRead_ThenEveryMemberIsImageVideoOrDocument()
	{
		// Given / When
		var all = MediaType.All;

		// Then
		all.ShouldContain(MediaType.Jpeg);
		all.ShouldContain(MediaType.Heic);
		all.ShouldContain(MediaType.Mp4);
		all.ShouldContain(MediaType.Pdf);
		all.ShouldAllBe(t => t.Kind == MediaKind.Image || t.Kind == MediaKind.Video || t.Kind == MediaKind.Document);
	}

	[Theory]
	[InlineData("application/pdf", MediaKind.Document)]
	[InlineData("application/msword", MediaKind.Document)]
	[InlineData("application/vnd.openxmlformats-officedocument.wordprocessingml.document", MediaKind.Document)]
	[InlineData("application/rtf", MediaKind.Document)]
	[InlineData("text/plain", MediaKind.Document)]
	[InlineData("application/vnd.oasis.opendocument.text", MediaKind.Document)]
	public void GivenADocumentContentType_WhenParsed_ThenAcceptedAsDocument(string declared,
																			MediaKind kind)
	{
		// Given / When
		var parsed = MediaType.TryParse(declared, out var type);

		// Then
		parsed.ShouldBeTrue();
		type.Kind.ShouldBe(kind);
		type.StrippedForm.ShouldBeNull();
		type.CanBeStripped.ShouldBeFalse();
	}

	[Theory]
	[InlineData("text/rtf", "application/rtf")]
	[InlineData("text/markdown", "text/plain")]
	public void GivenAnAliasContentType_WhenParsed_ThenResolvesToTheCanonicalType(string alias,
																				  string canonical)
	{
		// Given — a format with more than one real-world MIME declaration for the
		// same bytes; a sniffer only ever sees bytes, so it reports one canonical
		// answer regardless of which alias a client declared
		var expected = MediaType.Parse(canonical);

		// When
		var parsed = MediaType.TryParse(alias, out var type);

		// Then
		parsed.ShouldBeTrue();
		type.ShouldBe(expected);
	}
}
