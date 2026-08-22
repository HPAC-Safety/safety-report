using HpacSafety.Core.Features.Anonymization;
using HpacSafety.Core.Features.Reporting;
using HpacSafety.Core.SharedKernel;
using Shouldly;

namespace HpacSafety.Anonymization.Tests;

/// <summary>
/// The reporter submits an actual date and clock time; only the coarse forms
/// travel onward. These pin the invariant stage 1 owns — a precise time never
/// reaches stage 2 — rather than the bucket boundaries, which belong to the
/// reporting feature and must have exactly one definition.
/// </summary>
public class OccurrenceNarrowingTests
{
    [Theory]
    [InlineData(TimeOfDay.Morning)]
    [InlineData(TimeOfDay.MidDay)]
    [InlineData(TimeOfDay.Afternoon)]
    [InlineData(TimeOfDay.Evening)]
    public void Given_a_precise_time_in_the_field_When_it_is_scrubbed_Then_only_the_bucket_survives(TimeOfDay bucket)
    {
        // Given — the field still carries the clock time the reporter typed.
        var report = new ScrubRequest
        {
            Province = Province.BritishColumbia,
            TimeOfDay = bucket,
            Fields =
            [
                new ScrubField(ScrubFieldKind.OccurrenceTime, "Time of day", "15:42"),
                new ScrubField(ScrubFieldKind.Narrative, "Description", "A collapse on the ridge."),
            ],
        };

        // When
        var scrubbed = ScrubFixture.Scrub().Scrub(report);

        // Then
        scrubbed.Text.ShouldNotContain("15:42");
        scrubbed.Text.ShouldNotContain("42");
        scrubbed.Text.ShouldContain(EnumCode.Of(bucket));
    }

    [Fact]
    public void Given_the_reporter_did_not_answer_the_time_question_When_it_is_scrubbed_Then_it_says_unknown()
    {
        // Given — the form asked and the answer was "do not know". That is a
        // defined answer and it is published as one.
        var report = TimeReport(TimeOfDay.Unknown, "07:15");

        // When
        var scrubbed = ScrubFixture.Scrub().Scrub(report);

        // Then
        scrubbed.Text.ShouldNotContain("07:15");
        scrubbed.Text.ShouldContain(EnumCode.Of(TimeOfDay.Unknown));
        scrubbed.Text.ShouldNotContain(EnumCode.Of(TimeOfDay.Morning));
    }

    [Fact]
    public void Given_the_form_has_no_time_question_When_it_is_scrubbed_Then_the_field_is_dropped()
    {
        // Given — NotAnswered is not the same state as Unknown, and flattening
        // the two would publish "do not know" about a question nobody asked.
        var report = TimeReport(TimeOfDay.NotAnswered, "07:15");

        // When
        var scrubbed = ScrubFixture.Scrub().Scrub(report);

        // Then
        scrubbed.Text.ShouldNotContain("07:15");
        scrubbed.Fields.ShouldNotContain(field => field.Kind == ScrubFieldKind.OccurrenceTime);
        scrubbed.Text.ShouldNotContain(EnumCode.Of(TimeOfDay.Unknown));
    }

    [Fact]
    public void Given_a_midnight_occurrence_bucketed_as_morning_When_it_is_scrubbed_Then_it_publishes_morning()
    {
        // Given — midnight is a real answer, not an absent one. The mapping
        // lives in the reporting feature; what stage 1 owes is to carry the
        // answer through rather than mistake it for a default.
        var report = TimeReport(TimeOfDay.Morning, "00:00");

        // When
        var scrubbed = ScrubFixture.Scrub().Scrub(report);

        // Then
        scrubbed.Text.ShouldContain(EnumCode.Of(TimeOfDay.Morning));
        scrubbed.Text.ShouldNotContain("00:00");
    }

    [Fact]
    public void Given_a_precise_date_in_a_report_When_it_is_scrubbed_Then_only_the_month_and_year_survive()
    {
        // Given
        var report = new ScrubRequest
        {
            Province = Province.BritishColumbia,
            OccurredOn = new DateOnly(2026, 3, 17),
            Fields =
            [
                new ScrubField(ScrubFieldKind.OccurrenceDate, "Date", "2026-03-17"),
                new ScrubField(ScrubFieldKind.Narrative, "Description", "A collapse on the ridge."),
            ],
        };

        // When
        var scrubbed = ScrubFixture.Scrub().Scrub(report);

        // Then
        scrubbed.Text.ShouldNotContain("2026-03-17");
        scrubbed.Text.ShouldContain("2026-03");
    }

    [Fact]
    public void Given_no_date_was_given_When_the_report_is_scrubbed_Then_the_date_field_is_dropped()
    {
        // Given
        var report = new ScrubRequest
        {
            Province = Province.BritishColumbia,
            OccurredOn = null,
            Fields =
            [
                new ScrubField(ScrubFieldKind.OccurrenceDate, "Date", "2026-03-17"),
                new ScrubField(ScrubFieldKind.Narrative, "Description", "A collapse on the ridge."),
            ],
        };

        // When
        var scrubbed = ScrubFixture.Scrub().Scrub(report);

        // Then
        scrubbed.Text.ShouldNotContain("2026");
        scrubbed.Fields.ShouldNotContain(field => field.Kind == ScrubFieldKind.OccurrenceDate);
    }

    private static ScrubRequest TimeReport(TimeOfDay bucket, string fieldValue) => new()
    {
        Province = Province.BritishColumbia,
        TimeOfDay = bucket,
        Fields =
        [
            new ScrubField(ScrubFieldKind.OccurrenceTime, "Time of day", fieldValue),
            new ScrubField(ScrubFieldKind.Narrative, "Description", "A collapse on the ridge."),
        ],
    };
}
