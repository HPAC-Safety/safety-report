using HpacSafety.Core.Features.Reporting;
using HpacSafety.Core.SharedKernel;
using Shouldly;

namespace HpacSafety.Core.Tests.Media;

/// <summary>
/// "Original bytes stay in the Restricted record; the stripped derivative is
/// what a reviewer sees" — docs/data-handling.md. Storage will happily sign a
/// URL for either, so the rule is enforced here, once, rather than remembered at
/// every call site.
/// </summary>
public class ReviewerMediaLinkTests
{
    private static readonly BlobKey Original = BlobKey.Parse("reports/9f1c/photo.jpg");

    [Fact]
    public async Task Given_a_stripped_derivative_When_a_view_url_is_requested_Then_one_is_issued()
    {
        // Given
        var store = new InMemoryBlobStore();
        var derivative = Original.WithPrefix(MediaIngestor.DerivativePrefix);

        // When
        var url = await new ReviewerMediaLink(store).CreateViewUrlAsync(derivative, TimeSpan.FromMinutes(5), CancellationToken.None);

        // Then
        url.ShouldNotBeNull();
    }

    [Fact]
    public async Task Given_the_original_upload_When_a_view_url_is_requested_Then_it_is_refused()
    {
        // Given
        var store = new InMemoryBlobStore();

        // When / Then
        await Should.ThrowAsync<DomainRuleViolationException>(
            () => new ReviewerMediaLink(store).CreateViewUrlAsync(Original, TimeSpan.FromMinutes(5), CancellationToken.None));
    }

    [Fact]
    public async Task Given_a_key_that_merely_starts_with_the_prefix_letters_When_a_view_url_is_requested_Then_it_is_refused()
    {
        // Given
        // "strippedish/..." is not "stripped/...". The prefix is a path segment,
        // not a string prefix, and a near-miss must not read as a derivative.
        var lookalike = BlobKey.Parse("strippedish/reports/9f1c/photo.jpg");

        // When / Then
        await Should.ThrowAsync<DomainRuleViolationException>(
            () => new ReviewerMediaLink(new InMemoryBlobStore()).CreateViewUrlAsync(lookalike, TimeSpan.FromMinutes(5), CancellationToken.None));
    }

    [Fact]
    public void Given_an_ingested_photo_When_its_outcome_is_inspected_Then_its_derivative_key_is_reviewable()
    {
        // Given
        var outcome = MediaIngestOutcome.Ingested(
            MediaType.Jpeg, 100, "abc", Original.WithPrefix(MediaIngestor.DerivativePrefix), DateTimeOffset.UnixEpoch);

        // When
        var reviewable = ReviewerMediaLink.IsDerivative(outcome.DerivativeKey);

        // Then
        reviewable.ShouldBeTrue();
    }
}
