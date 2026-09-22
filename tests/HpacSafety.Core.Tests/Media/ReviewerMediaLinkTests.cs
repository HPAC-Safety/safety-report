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
            .CreateViewUrlAsync(derivative, TimeSpan.FromMinutes(5), CancellationToken.None);

        // Then
        url.ShouldNotBeNull();
    }

    [Theory]
    [InlineData(MediaCompartment.Original)]
    [InlineData(MediaCompartment.Quarantine)]
    public async Task GivenKeyOutsideStrippedCompartment_WhenViewUrlIsRequested_ThenRefused(MediaCompartment compartment)
    {
        // Given
        var key = BlobKey.For(ReportId, compartment, "photo.jpg");

        // When / Then
        await Should.ThrowAsync<DomainRuleViolationException>(() => new ReviewerMediaLink(new InMemoryBlobStore()).CreateViewUrlAsync(key, TimeSpan.FromMinutes(5), CancellationToken.None));
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
        await Should.ThrowAsync<DomainRuleViolationException>(() => new ReviewerMediaLink(new InMemoryBlobStore()).CreateViewUrlAsync(video, TimeSpan.FromMinutes(5), CancellationToken.None));
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
        var quarantined = BlobKey.Parse("quarantine/dQw4w9WgXcQ/photo.jpg");

        // When / Then
        ReviewerMediaLink.IsViewable(stripped).ShouldBeTrue();
        ReviewerMediaLink.IsViewable(original).ShouldBeFalse();
        ReviewerMediaLink.IsViewable(quarantined).ShouldBeFalse();
    }
}
