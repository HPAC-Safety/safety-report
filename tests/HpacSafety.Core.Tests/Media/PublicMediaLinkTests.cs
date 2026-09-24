using HpacSafety.Core.Features.Reporting;
using Shouldly;

namespace HpacSafety.Core.Tests.Media;

/// <summary>
///     The public may be shown a published report's stripped derivative and
///     nothing else (ADR-0117). The view decides which files are public; this is
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
	public void GivenNoBlobStore_WhenLinkIsCreated_ThenRefused()
	{
		// When / Then
		Should.Throw<ArgumentNullException>(() => new PublicMediaLink(null!));
	}
}
