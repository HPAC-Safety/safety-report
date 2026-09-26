using HpacSafety.Core.Features.QuestionBank;
using HpacSafety.Core.Features.Reporting;
using Shouldly;

namespace HpacSafety.Core.Tests;

/// <summary>
///     Whether a date answer may lie after today, and which today the API means
///     (ADR-0138).
/// </summary>
public class FutureDateTests
{
	// 10:00 UTC on 26 September is already 00:00 on 27 September at UTC+14.
	private static readonly DateTimeOffset Now = new(2026, 9, 26, 10, 0, 0, TimeSpan.Zero);

	[Theory]
	[InlineData("2026-09-26", false)]
	[InlineData("2026-09-27", false)]
	[InlineData("2026-09-28", true)]
	public void GivenInstant_WhenDateIsJudged_ThenTodayIsTheLatestTimeZones(string date,
																		   bool future)
	{
		// Given / When
		var judged = QuestionRevision.IsInTheFuture(DateOnly.Parse(date, System.Globalization.CultureInfo.InvariantCulture), Now);

		// Then
		judged.ShouldBe(future);
	}

	[Fact]
	public void GivenLastMinuteBeforeUtc14Midnight_WhenTomorrowThereIsJudged_ThenItIsFuture()
	{
		// Given — 09:59 UTC is 23:59 on the 26th at UTC+14
		var at = new DateTimeOffset(2026, 9, 26, 9, 59, 0, TimeSpan.Zero);

		// When / Then
		QuestionRevision.IsInTheFuture(new DateOnly(2026, 9, 27), at).ShouldBeTrue();
	}

	[Fact]
	public void GivenNoSetting_WhenDateQuestionIsCreated_ThenFutureDatesAreNotAllowed()
	{
		// Given / When
		var question = Question.Create("synthetic", QuestionType.Date, "When?", "Quand?", Now);

		// Then
		question.CurrentRevision.AllowFutureDates.ShouldBeFalse();
	}

	[Theory]
	[InlineData(QuestionType.ShortText)]
	[InlineData(QuestionType.Time)]
	[InlineData(QuestionType.Number)]
	public void GivenNonDateType_WhenAllowedFutureDates_ThenRefused(QuestionType type)
	{
		// Given / When
		var creating = () => Question.Create("synthetic", type, "A question", "Une question", Now, allowFutureDates: true);

		// Then
		creating.ShouldThrow<DomainRuleViolationException>();
	}

	[Fact]
	public void GivenDateQuestionAllowingFutureDates_WhenRewordedWithoutSaying_ThenKeepsItsSetting()
	{
		// Given
		var question = Question.Create("synthetic", QuestionType.Date, "When?", "Quand?", Now, allowFutureDates: true);

		// When
		question.Revise(QuestionType.Date, "On what date?", "À quelle date?", true, false, 0, Now.AddHours(1));

		// Then
		question.CurrentRevision.RevisionNumber.ShouldBe(2);
		question.CurrentRevision.AllowFutureDates.ShouldBeTrue();
	}

	[Fact]
	public void GivenDateQuestionAllowingFutureDates_WhenRetypedWithoutSaying_ThenSettingIsDropped()
	{
		// Given
		var question = Question.Create("synthetic", QuestionType.Date, "When?", "Quand?", Now, allowFutureDates: true);

		// When
		question.Revise(QuestionType.Time, "When?", "Quand?", true, false, 0, Now.AddHours(1));

		// Then
		question.CurrentRevision.AllowFutureDates.ShouldBeFalse();
	}

	[Theory]
	[InlineData(false, "2026-09-28", false)]
	[InlineData(false, "2026-09-27", true)]
	[InlineData(true, "2026-09-28", true)]
	public void GivenDateQuestion_WhenFutureDateIsAnswered_ThenSettingDecides(bool allowFutureDates,
																			   string date,
																			   bool accepted)
	{
		// Given
		var question = Question.Create(
			"synthetic", QuestionType.Date, "When?", "Quand?", Now, isActive: true, allowFutureDates: allowFutureDates);
		var report = new Report(Locale.EnCa, Now);

		// When
		var answering = () => report.Answer(question, date, Now);

		// Then
		if (accepted)
		{
			answering.ShouldNotThrow();
			report.Answers.ShouldHaveSingleItem().Text.ShouldBe(date);
		}
		else
		{
			answering.ShouldThrow<DomainRuleViolationException>().Message.ShouldNotContain(date);
		}
	}
}
