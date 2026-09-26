using HpacSafety.Core.Features.Reporting;
using Shouldly;

namespace HpacSafety.Core.Tests.Media;

/// <summary>
///     The public may be shown a published report's stripped derivative, or
///     download a public document's original, and nothing else (ADR-0117,
///     ADR-0119). The view decides which files are public; this is
///     the check that holds even if a query returned the wrong key.
/// </summary>
public class PublicMediaLinkTests
{
	private const string ReportId = "dQw4w9WgXcQ";

	[Fact]
	public async Task GivenStrippedDerivative_WhenPublicUrlIsRequested_ThenInlineOneIsIssued()
	{
		// Given
		var derivative = BlobKey.For(ReportId, MediaCompartment.Stripped, "photo");

		// When
		var url = await new PublicMediaLink(new InMemoryBlobStore())
			.CreateUrl(derivative, "image/jpeg", TimeSpan.FromMinutes(15), CancellationToken.None);

		// Then
		url.Query.ShouldContain("op=inline");
		url.Query.ShouldContain("ct=image%2Fjpeg");
	}

	[Theory]
	[InlineData(MediaCompartment.Original)]
	[InlineData(MediaCompartment.Quarantine)]
	[InlineData(MediaCompartment.Private)]
	public async Task GivenKeyOutsideStrippedCompartment_WhenPublicUrlIsRequested_ThenRefused(MediaCompartment compartment)
	{
		// Given
		var key = compartment == MediaCompartment.Quarantine
			? BlobKey.ForUpload(UploadId.New())
			: BlobKey.For(ReportId, compartment, "photo");

		// When / Then
		await Should.ThrowAsync<DomainRuleViolationException>(() =>
			new PublicMediaLink(new InMemoryBlobStore()).CreateUrl(key, "image/jpeg", TimeSpan.FromMinutes(15), CancellationToken.None));
	}

	[Fact]
	public async Task GivenLifetimeOverCap_WhenPublicUrlIsRequested_ThenRefused()
	{
		// Given
		var derivative = BlobKey.For(ReportId, MediaCompartment.Stripped, "photo");

		// When / Then
		await Should.ThrowAsync<DomainRuleViolationException>(() =>
			new PublicMediaLink(new InMemoryBlobStore()).CreateUrl(derivative, "image/jpeg", TimeSpan.FromHours(1), CancellationToken.None));
	}

	[Fact]
	public async Task GivenDocumentOriginal_WhenPublicDownloadIsRequested_ThenItIsForcedUnderServerMintedName()
	{
		// Given — ADR-0119: a public document is its unchanged original
		var fileId = TinyId.New();
		var original = BlobKey.For(ReportId, MediaCompartment.Original, fileId.Value);

		// When
		var url = await new PublicMediaLink(new InMemoryBlobStore())
			.CreateDocumentDownloadUrl(fileId, original, MediaType.Pdf, TimeSpan.FromMinutes(15), CancellationToken.None);

		// Then
		url.Query.ShouldContain("op=get");
		url.Query.ShouldContain($"fn={fileId.Value}.pdf");
	}

	[Theory]
	[InlineData("image/jpeg")]
	[InlineData("video/mp4")]
	public async Task GivenImageOrVideoOriginal_WhenPublicDownloadIsRequested_ThenRefused(string contentType)
	{
		// Given
		var fileId = TinyId.New();
		var original = BlobKey.For(ReportId, MediaCompartment.Original, fileId.Value);

		// When / Then
		await Should.ThrowAsync<DomainRuleViolationException>(() =>
			new PublicMediaLink(new InMemoryBlobStore())
				.CreateDocumentDownloadUrl(fileId, original, MediaType.Parse(contentType), TimeSpan.FromMinutes(15), CancellationToken.None));
	}

	[Theory]
	[InlineData(MediaCompartment.Stripped)]
	[InlineData(MediaCompartment.Quarantine)]
	[InlineData(MediaCompartment.Private)]
	public async Task GivenDocumentKeyOutsideOriginalCompartment_WhenPublicDownloadIsRequested_ThenRefused(MediaCompartment compartment)
	{
		// Given
		var fileId = TinyId.New();
		var key = compartment == MediaCompartment.Quarantine
			? BlobKey.ForUpload(UploadId.New())
			: BlobKey.For(ReportId, compartment, fileId.Value);

		// When / Then
		await Should.ThrowAsync<DomainRuleViolationException>(() =>
			new PublicMediaLink(new InMemoryBlobStore())
				.CreateDocumentDownloadUrl(fileId, key, MediaType.Pdf, TimeSpan.FromMinutes(15), CancellationToken.None));
	}

	[Fact]
	public async Task GivenLifetimeOverCap_WhenPublicDownloadIsRequested_ThenRefused()
	{
		// Given
		var fileId = TinyId.New();
		var original = BlobKey.For(ReportId, MediaCompartment.Original, fileId.Value);

		// When / Then
		await Should.ThrowAsync<DomainRuleViolationException>(() =>
			new PublicMediaLink(new InMemoryBlobStore())
				.CreateDocumentDownloadUrl(fileId, original, MediaType.Pdf, TimeSpan.FromHours(1), CancellationToken.None));
	}

	[Fact]
	public void GivenNoBlobStore_WhenLinkIsCreated_ThenRefused()
	{
		// When / Then
		Should.Throw<ArgumentNullException>(() => new PublicMediaLink(null!));
	}
}
