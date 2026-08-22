using HpacSafety.Core.Features.Reporting;
using HpacSafety.Core.SharedKernel;
using Shouldly;

namespace HpacSafety.Core.Tests.Media;

/// <summary>
/// <see cref="MediaIngestOutcome" /> is transient; <see cref="ReportFile" /> is
/// the row an admin UI will actually project from. The fail-closed rule has to
/// hold on both, or it holds only until the first page is written.
/// </summary>
public class ReportFileTests
{
    private const string ReportId = "dQw4w9WgXcQ";

    private static readonly DateTimeOffset Now = new(2026, 8, 22, 12, 0, 0, TimeSpan.Zero);

    private static ReportFile NewFile(string fileName = "photo.jpg") =>
        new(Guid.NewGuid(), BlobKey.For(ReportId, MediaCompartment.Original, fileName).Value, "image/jpeg", 1024, Now);

    [Fact]
    public void Given_a_file_with_no_derivative_When_a_viewable_key_is_asked_for_Then_it_fails_closed()
    {
        // Given
        var file = NewFile();

        // When / Then
        // Returning BlobKey — the unstripped original — is the leak this whole
        // feature exists to prevent.
        file.AwaitsStripping.ShouldBeTrue();
        Should.Throw<DomainRuleViolationException>(() => file.ViewableKey);
    }

    [Fact]
    public void Given_a_stripped_file_When_a_viewable_key_is_asked_for_Then_it_is_the_derivative()
    {
        // Given
        var file = NewFile();
        var derivative = BlobKey.For(ReportId, MediaCompartment.Stripped, "photo.jpg");

        // When
        file.RecordStripped(derivative.Value, Now);

        // Then
        file.AwaitsStripping.ShouldBeFalse();
        file.ViewableKey.ShouldBe(derivative);
        file.ViewableKey.Compartment.ShouldBe(MediaCompartment.Stripped);
    }

    [Fact]
    public void Given_a_video_When_it_is_recorded_Then_it_stays_unviewable()
    {
        // Given
        // A video is retained with no derivative until #65. It must read as
        // awaiting stripping for as long as that is true.
        var file = NewFile("clip.mp4");

        // When / Then
        file.AwaitsStripping.ShouldBeTrue();
        Should.Throw<DomainRuleViolationException>(() => file.ViewableKey);
    }

    [Fact]
    public void Given_a_key_outside_the_stripped_compartment_When_it_is_recorded_as_a_derivative_Then_it_is_refused()
    {
        // Given
        var file = NewFile();
        var original = BlobKey.For(ReportId, MediaCompartment.Original, "photo.jpg");

        // When / Then
        Should.Throw<DomainRuleViolationException>(() => file.RecordStripped(original.Value, Now));
        file.AwaitsStripping.ShouldBeTrue();
    }

    [Fact]
    public void Given_a_derivative_is_recorded_When_the_row_is_read_Then_both_facts_were_written_together()
    {
        // Given
        var file = NewFile();

        // When
        file.RecordStripped(BlobKey.For(ReportId, MediaCompartment.Stripped, "photo.jpg").Value, Now);

        // Then
        // AwaitsStripping checks both, so a row carrying a timestamp with no key
        // cannot read as viewable.
        file.ExifStrippedAt.ShouldBe(Now);
        file.StrippedBlobKey.ShouldNotBeNull();
    }
}
