using HpacSafety.Core.Features.QuestionBank;
using HpacSafety.Core.Features.Reporting;
using HpacSafety.Core.SharedKernel;

using Shouldly;

namespace HpacSafety.Core.Tests;

/// <summary>
/// The reporter gives a real clock time; the coarse bucket is derived. These
/// are the boundaries, written down once here and asserted at the exact minute
/// on either side of each one. See ADR-0019 and #68.
/// </summary>
public sealed class TimeOfDayBucketsTests
{
    private static readonly DateTimeOffset Now = new(2026, 8, 22, 12, 0, 0, TimeSpan.Zero);

    [Theory]
    // Morning, up to but not including 11:00.
    [InlineData(0, 0, TimeOfDay.Morning)]
    [InlineData(6, 30, TimeOfDay.Morning)]
    [InlineData(10, 59, TimeOfDay.Morning)]
    // Mid-day, 11:00 up to but not including 14:00.
    [InlineData(11, 0, TimeOfDay.MidDay)]
    [InlineData(13, 59, TimeOfDay.MidDay)]
    // Afternoon, 14:00 up to but not including 17:00.
    [InlineData(14, 0, TimeOfDay.Afternoon)]
    [InlineData(16, 59, TimeOfDay.Afternoon)]
    // Evening, 17:00 onwards.
    [InlineData(17, 0, TimeOfDay.Evening)]
    [InlineData(23, 59, TimeOfDay.Evening)]
    public void Given_a_local_clock_time_When_the_time_of_day_is_derived_Then_it_falls_in_the_agreed_bucket(
        int hour, int minute, TimeOfDay expected)
    {
        // Given
        var localTime = new TimeOnly(hour, minute);

        // When
        var bucket = TimeOfDay.FromLocalTime(localTime);

        // Then
        bucket.ShouldBe(expected, $"{localTime:HH\\:mm} local.");
    }

    [Fact]
    public void Given_the_last_moment_before_a_boundary_When_it_is_derived_Then_it_is_still_the_earlier_bucket()
    {
        // Given — a second, not a minute, either side of every boundary.
        // Off-by-one here silently reclassifies an accident.
        var boundaries = new[]
        {
            (TimeOfDayBuckets.MidDayStarts, TimeOfDay.Morning, TimeOfDay.MidDay),
            (TimeOfDayBuckets.AfternoonStarts, TimeOfDay.MidDay, TimeOfDay.Afternoon),
            (TimeOfDayBuckets.EveningStarts, TimeOfDay.Afternoon, TimeOfDay.Evening),
        };

        // When / Then
        foreach (var (boundary, before, atAndAfter) in boundaries)
        {
            TimeOfDay.FromLocalTime(boundary.Add(TimeSpan.FromSeconds(-1))).ShouldBe(before, $"one second before {boundary:HH\\:mm}.");
            TimeOfDay.FromLocalTime(boundary).ShouldBe(atAndAfter, $"exactly {boundary:HH\\:mm}.");
        }
    }

    [Fact]
    public void Given_the_boundaries_When_they_are_read_Then_they_are_the_ones_the_form_and_the_scrub_share()
    {
        // Given / When / Then — #18 derives the published bucket from this same
        // type rather than re-deriving the boundaries, so a change here is a
        // change everywhere and cannot drift.
        TimeOfDayBuckets.MidDayStarts.ShouldBe(new TimeOnly(11, 0));
        TimeOfDayBuckets.AfternoonStarts.ShouldBe(new TimeOnly(14, 0));
        TimeOfDayBuckets.EveningStarts.ShouldBe(new TimeOnly(17, 0));
    }

    [Fact]
    public void Given_no_time_at_all_When_the_time_of_day_is_derived_Then_it_is_do_not_know_rather_than_midnight()
    {
        // Given — #68 makes the time optional so a reporter who does not
        // remember still files. Midnight is a real answer and this is not one.
        TimeOnly? absent = null;

        // When
        var bucket = TimeOfDay.FromLocalTime(absent);

        // Then
        bucket.ShouldBe(TimeOfDay.Unknown);
        bucket.ShouldNotBe(TimeOfDay.Morning);
    }

    [Fact]
    public void Given_a_time_that_was_given_When_the_time_of_day_is_derived_Then_it_is_never_do_not_know()
    {
        // Given / When / Then — a reading that exists always lands somewhere.
        for (var minutes = 0; minutes < 24 * 60; minutes++)
        {
            var bucket = TimeOfDay.FromLocalTime(new TimeOnly(minutes / 60, minutes % 60));
            bucket.ShouldNotBe(TimeOfDay.Unknown);
            bucket.ShouldNotBe(TimeOfDay.NotAnswered);
        }
    }

    [Fact]
    public void Given_a_report_answering_the_occurrence_time_When_it_is_recorded_Then_the_clock_time_and_the_bucket_are_both_kept()
    {
        // Given
        var question = Question.Create(
            "occurrence_time", QuestionType.ShortText, Locale.EnCa, "What time?", Now,
            role: QuestionRole.OccurrenceTime);
        var report = new Report(Locale.EnCa, Now);

        // When
        report.Answer(question, "15:20", Now);

        // Then — the precise time for a reviewer, the bucket for everything else.
        report.OccurredAtLocal.ShouldBe(new TimeOnly(15, 20));
        report.TimeOfDay.ShouldBe(TimeOfDay.Afternoon);
    }

    [Fact]
    public void Given_a_report_filed_without_a_time_When_it_is_recorded_Then_it_says_do_not_know()
    {
        // Given
        var question = Question.Create(
            "occurrence_time", QuestionType.ShortText, Locale.EnCa, "What time?", Now,
            role: QuestionRole.OccurrenceTime);
        var report = new Report(Locale.EnCa, Now);

        // When
        report.Answer(question, (string?)null, Now);

        // Then
        report.OccurredAtLocal.ShouldBeNull();
        report.TimeOfDay.ShouldBe(TimeOfDay.Unknown);
    }

    [Fact]
    public void Given_a_time_answer_that_is_not_a_time_When_it_is_recorded_Then_nothing_invents_one()
    {
        // Given — an administrator may point the time role at a free-text question.
        var question = Question.Create(
            "occurrence_time", QuestionType.ShortText, Locale.EnCa, "What time?", Now,
            role: QuestionRole.OccurrenceTime);
        var report = new Report(Locale.EnCa, Now);

        // When
        report.Answer(question, "just after lunch", Now);

        // Then — the answer is kept for a reviewer; the bucket is not guessed.
        report.OccurredAtLocal.ShouldBeNull();
        report.TimeOfDay.ShouldBe(TimeOfDay.Unknown);
        report.Answers[0].Value.ShouldBe("just after lunch");
    }

    [Fact]
    public void Given_a_form_with_no_time_question_at_all_When_a_report_is_filed_Then_the_time_of_day_is_not_answered()
    {
        // Given — a missing role is a defined state, and it is a different one
        // from a reporter saying they do not know. See ADR-0016.
        var report = new Report(Locale.EnCa, Now);

        // When / Then
        report.OccurredAtLocal.ShouldBeNull();
        report.TimeOfDay.ShouldBe(TimeOfDay.NotAnswered);
    }
}
