using HpacSafety.Core.Features.QuestionBank;
using Shouldly;

namespace HpacSafety.Core.Tests;

/// <summary>
///     A reviewer's hand on type-ahead values: approve, correct in place, remove,
///     and a removed value a reporter types again (ADR-0129).
/// </summary>
public class TypeAheadReviewTests
{
	private const string Reviewer = "synthetic-safety-officer";
	private static readonly DateTimeOffset Now = new(2026, 9, 25, 12, 0, 0, TimeSpan.Zero);

	[Fact]
	public void GivenAnotherLiveValueReadsTheSame_WhenCorrected_ThenRefusedAsADuplicateToMerge()
	{
		// Given
		var question = TypeAhead();
		question.AddChoiceFromReporter("Cooper's", Locale.EnCa, Now);
		var coopers = question.AddChoiceFromReporter("Coopers", Locale.EnCa, Now);

		// When
		var correcting = () => question.CorrectValue(coopers.Id, "cooper's", null, Reviewer, Now);

		// Then
		correcting.ShouldThrow<DomainRuleViolationException>().Message.ShouldContain("Merge the two instead");
		coopers.LabelEn.ShouldBe("Coopers");
		coopers.NeedsReview.ShouldBeTrue();
	}

	[Fact]
	public void GivenAReporterValue_WhenCorrectedInBothLanguages_ThenItHasBothAndIsReviewed()
	{
		// Given
		var question = TypeAhead();
		var value = question.AddChoiceFromReporter("mount 7", Locale.EnCa, Now);

		// When
		question.CorrectValue(value.Id, " Mount 7 ", "Mont 7", Reviewer, Now.AddHours(1));

		// Then
		(value.LabelEn, value.LabelFr).ShouldBe(("Mount 7", "Mont 7"));
		value.NeedsReview.ShouldBeFalse();
		value.ReviewedAt.ShouldBe(Now.AddHours(1));
	}

	[Fact]
	public void GivenARemovedValue_WhenRemovedAgain_ThenItKeepsWhenItWasRemoved()
	{
		// Given
		var question = TypeAhead();
		var value = question.AddChoiceFromReporter("Test site", Locale.EnCa, Now);
		question.RemoveValue(value.Id, Reviewer, Now.AddHours(1));

		// When
		question.RemoveValue(value.Id, Reviewer, Now.AddHours(2));

		// Then
		value.Deleted.ShouldBe(Now.AddHours(1));
		value.ReviewedAt.ShouldBe(Now.AddHours(2));
	}

	[Fact]
	public void GivenARemovedValueCorrectedAwayFromItsCode_WhenAReporterTypesItsWording_ThenItIsNamedAndFlaggedAgain()
	{
		// Given — its code reads "ridge", its wording no longer does
		var question = TypeAhead();
		var value = question.AddChoiceFromReporter("ridge", Locale.EnCa, Now);
		question.CorrectValue(value.Id, "Ridge Top", null, Reviewer, Now);
		question.RemoveValue(value.Id, Reviewer, Now);

		// When
		var named = question.AddChoiceFromReporter("ridge top", Locale.EnCa, Now);

		// Then
		named.ShouldBeSameAs(value);
		value.Deleted.ShouldNotBeNull();
		value.NeedsReview.ShouldBeTrue();
	}

	[Fact]
	public void GivenAValueOfAnotherQuestion_WhenReviewed_ThenRefused()
	{
		// Given
		var question = TypeAhead();
		var other = TypeAhead().AddChoiceFromReporter("Elsewhere", Locale.EnCa, Now);

		// When
		var approving = () => question.ApproveValue(other.Id, Reviewer, Now);

		// Then
		approving.ShouldThrow<DomainRuleViolationException>().Message.ShouldContain("no such value");
	}

	[Fact]
	public void GivenNoReviewer_WhenApproving_ThenRefused()
	{
		// Given
		var question = TypeAhead();
		var value = question.AddChoiceFromReporter("Mount 7", Locale.EnCa, Now);

		// When
		var approving = () => question.ApproveValue(value.Id, " ", Now);

		// Then
		approving.ShouldThrow<DomainRuleViolationException>();
		value.NeedsReview.ShouldBeTrue();
	}

	[Fact]
	public void GivenAPickerOption_WhenReviewed_ThenRefused()
	{
		// Given — a picker option is an Administrator's to fix or replace (ADR-0128)
		var question = Question.Create(
			"wing", QuestionType.SingleSelect, "Wing", "Aile", Now, isActive: true,
			options: [new QuestionOptionInput("paraglider", "Paraglider", "Parapente")]);

		// When
		var removing = () => question.RemoveValue(question.Choices[0].Id, Reviewer, Now);

		// Then
		removing.ShouldThrow<DomainRuleViolationException>().Message.ShouldContain("Only a type-ahead value is reviewed");
	}

	[Fact]
	public void GivenAFlaggedValue_WhenTheQuestionForks_ThenTheCopyStaysFlaggedWithItsDates()
	{
		// Given
		var question = TypeAhead();
		var value = question.AddChoiceFromReporter("Mount 7", Locale.EnCa, Now);
		new Features.Reporting.Report(Locale.EnCa, Now).Answer(question, "Mount 7", Now);

		// When
		var current = question.CurrentRevision;
		var live = question.ApplyEdit(
			true, current.Type, "Where were you flying?", current.LabelFr, current.IsPrivate, current.IsActive,
			current.DisplayOrder, Now.AddDays(1));

		// Then
		var copy = live.AllChoices.Single();
		copy.Id.ShouldNotBe(value.Id);
		copy.NeedsReview.ShouldBeTrue();
		copy.CreatedAt.ShouldBe(Now);
	}

	[Fact]
	public void GivenAFlaggedValue_WhenTheWorkerSuppliesItsOtherLanguage_ThenItStaysFlagged()
	{
		// Given
		var question = TypeAhead();
		var value = question.AddChoiceFromReporter("Élévation Sainte-Anne", Locale.FrCa, Now);

		// When — translation is mechanical; review is a person's (ADR-0129)
		value.SupplyAutoTranslation("Sainte-Anne Rise").ShouldBeTrue();

		// Then
		value.LabelEnSource.ShouldBe(LabelSource.Auto);
		value.NeedsReview.ShouldBeTrue();
		value.ReviewedAt.ShouldBeNull();
		question.ReporterChoicesAwaitingReview.ShouldBe(1);
	}

	[Theory]
	[InlineData("itself")]
	[InlineData("a removed value")]
	[InlineData("from a merged value")]
	public void GivenAnImpossibleMerge_WhenAttempted_ThenRefused(string merge)
	{
		// Given
		var question = TypeAhead();
		var a = question.AddChoiceFromReporter("A", Locale.EnCa, Now);
		var b = question.AddChoiceFromReporter("B", Locale.EnCa, Now);
		var c = question.AddChoiceFromReporter("C", Locale.EnCa, Now);
		question.RemoveValue(c.Id, Reviewer, Now);
		question.MergeValue(b.Id, a.Id, Reviewer, Now);

		// When
		Action merging = merge switch
		{
			"itself" => () => question.MergeValue(a.Id, a.Id, Reviewer, Now),
			"a removed value" => () => question.MergeValue(a.Id, c.Id, Reviewer, Now),
			_ => () => question.MergeValue(b.Id, a.Id, Reviewer, Now),
		};

		// Then
		merging.ShouldThrow<DomainRuleViolationException>();
	}

	[Fact]
	public void GivenAMergedValue_WhenAReporterTypesAWordingReducingToItsCode_ThenTheAnswerNamesTheTarget()
	{
		// Given — "Coopers!" reads as nothing, but reduces to the merged value's code
		var question = TypeAhead();
		var coopers = question.AddChoiceFromReporter("Coopers", Locale.EnCa, Now);
		var target = question.AddChoiceFromReporter("Cooper's Hill", Locale.EnCa, Now);
		question.MergeValue(coopers.Id, target.Id, Reviewer, Now);

		// When
		var named = question.AddChoiceFromReporter("Coopers!", Locale.EnCa, Now);

		// Then
		named.ShouldBeSameAs(target);
		coopers.NeedsReview.ShouldBeFalse();
	}

	[Fact]
	public void GivenAMergedValue_WhenTheQuestionForks_ThenTheCopyIsMergedIntoTheCopyOfItsTarget()
	{
		// Given
		var question = TypeAhead();
		var coopers = question.AddChoiceFromReporter("Coopers", Locale.EnCa, Now);
		var target = question.AddChoiceFromReporter("Cooper's", Locale.EnCa, Now);
		question.MergeValue(coopers.Id, target.Id, Reviewer, Now);
		new Features.Reporting.Report(Locale.EnCa, Now).Answer(question, "Cooper's", Now);

		// When
		var current = question.CurrentRevision;
		var live = question.ApplyEdit(
			true, current.Type, "Where were you flying?", current.LabelFr, current.IsPrivate, current.IsActive,
			current.DisplayOrder, Now.AddDays(1));

		// Then
		var copiedTarget = live.AllChoices.Single(choice => choice.Code == target.Code);
		var copiedSource = live.AllChoices.Single(choice => choice.Code == coopers.Code);
		copiedSource.MergedIntoChoiceId.ShouldBe(copiedTarget.Id);
		copiedSource.Resolved.ShouldBeSameAs(copiedTarget);
		copiedSource.Deleted.ShouldBe(coopers.Deleted);
	}

	[Fact]
	public void GivenAMergedValueWithoutItsTarget_WhenResolved_ThenItRefusesToShowTheOldWording()
	{
		// Given — what a reader that forgot to load the target would hold
		var loaded = TypeAhead();
		var coopers = loaded.AddChoiceFromReporter("Coopers", Locale.EnCa, Now);
		loaded.MergeValue(coopers.Id, loaded.AddChoiceFromReporter("Cooper's", Locale.EnCa, Now).Id, Reviewer, Now);
		typeof(QuestionChoice).GetProperty(nameof(QuestionChoice.MergedInto))!.SetValue(coopers, null);

		// When / Then
		Should.Throw<InvalidOperationException>(() => coopers.Resolved);
	}

	private static Question TypeAhead()
	{
		return Question.Create("site", QuestionType.Autocomplete, "Where?", "Où ?", Now, isActive: true);
	}
}
