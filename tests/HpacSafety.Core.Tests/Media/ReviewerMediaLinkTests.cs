using HpacSafety.Core.Features.Reporting;
using Shouldly;

namespace HpacSafety.Core.Tests.Media;

/// <summary>
///     "Original bytes stay in the private source record; the stripped derivative is
///     what a reviewer sees" — docs/data-handling.md. Storage will sign a URL for any
///     key, so the rule is enforced here, once, rather than remembered at every call
///     site.
/// </summary>
public class ReviewerMediaLinkTests
{
	private const string ReportId = "dQw4w9WgXcQ";

	[Fact]
	public async Task GivenStrippedDerivative_WhenViewUrlIsRequested_ThenOneIsIssued()
	{
		// Given
		var derivative = BlobKey.For(ReportId, MediaCompartment.Stripped, "photo.jpg");

		// When
		var url = await new ReviewerMediaLink(new InMemoryBlobStore())
			.CreateViewUrl(derivative, "download.jpg", TimeSpan.FromMinutes(5), CancellationToken.None);

		// Then
		url.ShouldNotBeNull();
	}

	[Theory]
	[InlineData(MediaCompartment.Original)]
	[InlineData(MediaCompartment.Quarantine)]
	[InlineData(MediaCompartment.Private)]
	public async Task GivenKeyOutsideStrippedCompartment_WhenViewUrlIsRequested_ThenRefused(MediaCompartment compartment)
	{
		// Given
		var key = compartment == MediaCompartment.Quarantine
			? BlobKey.ForUpload(UploadId.New())
			: BlobKey.For(ReportId, compartment, "photo.jpg");

		// When / Then
		await Should.ThrowAsync<DomainRuleViolationException>(() => new ReviewerMediaLink(new InMemoryBlobStore()).CreateViewUrl(key, "download.jpg", TimeSpan.FromMinutes(5), CancellationToken.None));
	}

	[Fact]
	public async Task GivenUploadedVideo_WhenViewUrlIsRequested_ThenRefused()
	{
		// Given
		// A video has no stripped compartment until #65, so its only key is the
		// original. It is refused by the same rule as any other original rather
		// than by a special case — which is why adding video did not need a new
		// check here.
		var video = BlobKey.For(ReportId, MediaCompartment.Original, "clip.mp4");

		// When / Then
		await Should.ThrowAsync<DomainRuleViolationException>(() => new ReviewerMediaLink(new InMemoryBlobStore()).CreateViewUrl(video, "download.jpg", TimeSpan.FromMinutes(5), CancellationToken.None));
	}

	[Fact]
	public async Task GivenStrippedDerivative_WhenInlineViewUrlIsRequested_ThenOneIsIssued()
	{
		// Given
		// issue #427 decision 10: /view mints an inline link now, for the
		// lightbox, rather than a forced download.
		var derivative = BlobKey.For(ReportId, MediaCompartment.Stripped, "photo.jpg");

		// When
		var url = await new ReviewerMediaLink(new InMemoryBlobStore())
			.CreateInlineViewUrl(derivative, "image/jpeg", TimeSpan.FromMinutes(5), CancellationToken.None);

		// Then
		url.ShouldNotBeNull();
	}

	[Theory]
	[InlineData(MediaCompartment.Original)]
	[InlineData(MediaCompartment.Quarantine)]
	[InlineData(MediaCompartment.Private)]
	public async Task GivenKeyOutsideStrippedCompartment_WhenInlineViewUrlIsRequested_ThenRefused(MediaCompartment compartment)
	{
		// Given
		var key = compartment == MediaCompartment.Quarantine
			? BlobKey.ForUpload(UploadId.New())
			: BlobKey.For(ReportId, compartment, "photo.jpg");

		// When / Then
		await Should.ThrowAsync<DomainRuleViolationException>(() =>
			new ReviewerMediaLink(new InMemoryBlobStore()).CreateInlineViewUrl(key, "image/jpeg", TimeSpan.FromMinutes(5), CancellationToken.None));
	}

	[Fact]
	public void GivenReportIdMovedToFrontOfKey_WhenViewabilityIsChecked_ThenStillReadsCompartment()
	{
		// Given
		// Re-verifying the check after the layout changed: the compartment is a
		// parsed property, not a substring, so a differently-shaped key cannot
		// silently pass.
		var stripped = BlobKey.Parse("dQw4w9WgXcQ/stripped/photo.jpg");
		var original = BlobKey.Parse("dQw4w9WgXcQ/original/photo.jpg");
		var quarantined = BlobKey.Parse($"quarantine/{UploadId.New().Value}");

		// When / Then
		ReviewerMediaLink.IsViewable(stripped).ShouldBeTrue();
		ReviewerMediaLink.IsViewable(original).ShouldBeFalse();
		ReviewerMediaLink.IsViewable(quarantined).ShouldBeFalse();
	}

	[Fact]
	public async Task GivenDocumentOriginal_WhenDownloadUrlIsRequested_ThenOneIsIssued()
	{
		// Given
		var original = BlobKey.For(ReportId, MediaCompartment.Original, "report.pdf");

		// When
		var url = await new ReviewerMediaLink(new InMemoryBlobStore())
			.CreateDocumentDownloadUrl(original, AttachmentKind.Document, "report.pdf", TimeSpan.FromMinutes(5), CancellationToken.None);

		// Then
		url.ShouldNotBeNull();
	}

	[Theory]
	[InlineData(AttachmentKind.Image)]
	[InlineData(AttachmentKind.Video)]
	public async Task GivenAnImageOrVideoOriginal_WhenDocumentDownloadUrlIsRequested_ThenRefused(AttachmentKind kind)
	{
		// Given
		// Only a document has no stripped derivative to redirect a reviewer to
		// instead — an image or video original must never be exposed this way,
		// even if a caller passes the right compartment.
		var original = BlobKey.For(ReportId, MediaCompartment.Original, "photo.jpg");

		// When / Then
		await Should.ThrowAsync<DomainRuleViolationException>(() =>
			new ReviewerMediaLink(new InMemoryBlobStore()).CreateDocumentDownloadUrl(original, kind, "photo.jpg", TimeSpan.FromMinutes(5), CancellationToken.None));
	}

	[Theory]
	[InlineData(MediaCompartment.Stripped)]
	[InlineData(MediaCompartment.Quarantine)]
	[InlineData(MediaCompartment.Private)]
	public async Task GivenDocumentKeyOutsideOriginalCompartment_WhenDocumentDownloadUrlIsRequested_ThenRefused(MediaCompartment compartment)
	{
		// Given
		var key = compartment == MediaCompartment.Quarantine
			? BlobKey.ForUpload(UploadId.New())
			: BlobKey.For(ReportId, compartment, "report.pdf");

		// When / Then
		await Should.ThrowAsync<DomainRuleViolationException>(() =>
			new ReviewerMediaLink(new InMemoryBlobStore()).CreateDocumentDownloadUrl(key, AttachmentKind.Document, "report.pdf", TimeSpan.FromMinutes(5), CancellationToken.None));
	}

	[Theory]
	[InlineData(AttachmentKind.Image)]
	[InlineData(AttachmentKind.Video)]
	public async Task GivenImageOrVideoOriginalWithNoDerivative_WhenOriginalDownloadUrlIsRequested_ThenOneIsIssued(AttachmentKind kind)
	{
		// Given
		// issue #427 decision 15: a raw original is downloadable this way only
		// while there is no stripped derivative to view instead.
		var original = BlobKey.For(ReportId, MediaCompartment.Original, "clip.mp4");

		// When
		var url = await new ReviewerMediaLink(new InMemoryBlobStore())
			.CreateOriginalMediaDownloadUrl(original, kind, hasDerivative: false, "clip.mp4", TimeSpan.FromMinutes(5), CancellationToken.None);

		// Then
		url.ShouldNotBeNull();
	}

	[Fact]
	public async Task GivenADocument_WhenOriginalDownloadUrlIsRequested_ThenRefused()
	{
		// Given
		// A document downloads through CreateDocumentDownloadUrl instead — it
		// never has a stripped derivative to fall back from.
		var original = BlobKey.For(ReportId, MediaCompartment.Original, "report.pdf");

		// When / Then
		await Should.ThrowAsync<DomainRuleViolationException>(() =>
			new ReviewerMediaLink(new InMemoryBlobStore()).CreateOriginalMediaDownloadUrl(
				original, AttachmentKind.Document, hasDerivative: false, "report.pdf", TimeSpan.FromMinutes(5), CancellationToken.None));
	}

	[Theory]
	[InlineData(AttachmentKind.Image)]
	[InlineData(AttachmentKind.Video)]
	public async Task GivenAStrippedDerivativeAlreadyExists_WhenOriginalDownloadUrlIsRequested_ThenRefused(AttachmentKind kind)
	{
		// Given
		// The chokepoint's own enforcement of issue #427 decision 15 — not only the
		// calling endpoint's: once a derivative exists, a reviewer views it through
		// CreateInlineViewUrl instead. This is the ADR-0026 privacy boundary this
		// method exists to hold even if a future caller forgets to check first.
		var original = BlobKey.For(ReportId, MediaCompartment.Original, "clip.mp4");

		// When / Then
		var thrown = await Should.ThrowAsync<DomainRuleViolationException>(() =>
			new ReviewerMediaLink(new InMemoryBlobStore()).CreateOriginalMediaDownloadUrl(
				original, kind, hasDerivative: true, "clip.mp4", TimeSpan.FromMinutes(5), CancellationToken.None));
		thrown.Message.ShouldBe("The raw original is offered only while there is no derivative to view instead.");
	}

	[Theory]
	[InlineData(MediaCompartment.Stripped)]
	[InlineData(MediaCompartment.Quarantine)]
	[InlineData(MediaCompartment.Private)]
	public async Task GivenOriginalKeyOutsideOriginalCompartment_WhenOriginalDownloadUrlIsRequested_ThenRefused(MediaCompartment compartment)
	{
		// Given
		var key = compartment == MediaCompartment.Quarantine
			? BlobKey.ForUpload(UploadId.New())
			: BlobKey.For(ReportId, compartment, "clip.mp4");

		// When / Then
		await Should.ThrowAsync<DomainRuleViolationException>(() =>
			new ReviewerMediaLink(new InMemoryBlobStore()).CreateOriginalMediaDownloadUrl(
				key, AttachmentKind.Video, hasDerivative: false, "clip.mp4", TimeSpan.FromMinutes(5), CancellationToken.None));
	}
}
