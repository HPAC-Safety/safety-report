using HpacSafety.Core.Features.Anonymization;
using HpacSafety.Core.Features.Reporting;
using HpacSafety.Core.SharedKernel;
using Shouldly;

namespace HpacSafety.Anonymization.Tests;

/// <summary>
/// The reporter now submits an actual date and time; the anonymizer derives the
/// coarse bucket. These pin the boundaries and, more importantly, pin that a
/// precise time never reaches stage 2.
/// </summary>
public class OccurrenceNarrowingTests
{
    [Theory]
    [InlineData(0, 0, TimeOfDay.Morning)]
    [InlineData(10, 59, TimeOfDay.Morning)]
    [InlineData(11, 0, TimeOfDay.MidDay)]
    [InlineData(13, 59, TimeOfDay.MidDay)]
    [InlineData(14, 0, TimeOfDay.Afternoon)]
    [InlineData(16, 59, TimeOfDay.Afternoon)]
    [InlineData(17, 0, TimeOfDay.Evening)]
    [InlineData(23, 59, TimeOfDay.Evening)]
    public void Given_a_wall_clock_time_When_it_is_narrowed_Then_it_lands_in_the_expected_bucket(
        int hour,
        int minute,
        TimeOfDay expected)
    {
        // Given
        var occurredAt = new TimeOnly(hour, minute);

        // When
        var bucket = OccurrenceNarrowing.Bucket(occurredAt);

        // Then
        bucket.ShouldBe(expected);
    }

    [Fact]
    public void Given_no_time_was_given_When_it_is_narrowed_Then_it_is_unknown_rather_than_midnight()
    {
        // Given — the time input is optional and a reporter who does not
        // remember still files. Treating the default as midnight would publish
        // "morning" about a crash nobody timed.
        TimeOnly? absent = null;

        // When
        var bucket = OccurrenceNarrowing.Bucket(absent);

        // Then
        bucket.ShouldBe(TimeOfDay.Unknown);
        bucket.ShouldNotBe(TimeOfDay.Morning);
        bucket.ShouldNotBe(TimeOfDay.NotAnswered);
    }

    [Fact]
    public void Given_a_precise_time_in_a_report_When_it_is_scrubbed_Then_only_the_bucket_survives()
    {
        // Given
        var report = new ScrubRequest
        {
            Province = Province.BritishColumbia,
            OccurredAt = new TimeOnly(15, 42),
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
        scrubbed.Text.ShouldContain(EnumCode.Of(TimeOfDay.Afternoon));
    }

    [Fact]
    public void Given_no_time_in_a_report_When_it_is_scrubbed_Then_the_field_says_unknown()
    {
        // Given
        var report = new ScrubRequest
        {
            Province = Province.BritishColumbia,
            OccurredAt = null,
            Fields =
            [
                new ScrubField(ScrubFieldKind.OccurrenceTime, "Time of day", "07:15"),
                new ScrubField(ScrubFieldKind.Narrative, "Description", "A collapse on the ridge."),
            ],
        };

        // When
        var scrubbed = ScrubFixture.Scrub().Scrub(report);

        // Then — the stale field value must not win over the absent answer.
        scrubbed.Text.ShouldNotContain("07:15");
        scrubbed.Text.ShouldContain(EnumCode.Of(TimeOfDay.Unknown));
        scrubbed.Text.ShouldNotContain(EnumCode.Of(TimeOfDay.Morning));
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
}
