using HpacSafety.Core.Features.QuestionBank;
using Shouldly;

namespace HpacSafety.Core.Tests;

/// <summary>
///     A question's own choices: edited in place, copied by a fork, and grown by a
///     reporter only on a type-ahead — ADR-0063, ADR-0095.
/// </summary>
public class QuestionChoicesTests
{
	private static readonly DateTimeOffset At = new(2026, 9, 21, 12, 0, 0, TimeSpan.Zero);

	private static readonly QuestionOptionInput[] SiteOptions =
	[
		new("coopers", "Cooper's", "Cooper's"),
		new("woodside", "Woodside", "Woodside"),
	];

	private static Question Sites(QuestionType type = QuestionType.Autocomplete)
	{
		return Question.Create(
			"where_did_this_happen", type, "Where did this happen?", "Où cela s'est-il produit ?", At,
			isActive: true, options: SiteOptions);
	}

	private static Question Edit(Question question,
								 bool answered,
								 string? labelEn = null,
								 IReadOnlyList<QuestionOptionInput>? options = null)
	{
		var current = question.CurrentRevision;
		return question.ApplyEdit(
			answered, current.Type, labelEn ?? current.LabelEn, current.LabelFr, current.IsPrivate, current.IsActive,
			current.DisplayOrder, At.AddDays(1), options: options);
	}

	private static string[] Codes(Question question)
	{
		return [.. question.Choices.Select(choice => choice.Code)];
	}

	[Fact]
	public void GivenNewQuestion_WhenCreatedWithChoices_ThenOffersThem()
	{
		Codes(Sites()).ShouldBe(["coopers", "woodside"], ignoreOrder: true);
	}

	[Theory]
	[InlineData(QuestionType.SingleSelect)]
	[InlineData(QuestionType.MultiSelect)]
	[InlineData(QuestionType.Autocomplete)]
	public void GivenAnsweredQuestion_WhenOnlyChoicesChange_ThenQuestionAndRevisionAreKept(QuestionType type)
	{
		// Given
		var question = Sites(type);
		var revision = question.CurrentRevision.Id;

		// When
		var live = Edit(question, answered: true,
			options: [new("woodside", "Woodside", "Woodside"), new("coopers", "Cooper's Hill", "Colline Cooper"), new("mara", "Mara", "Mara")]);

		// Then
		live.ShouldBeSameAs(question);
		question.Deleted.ShouldBeNull();
		question.CurrentRevision.Id.ShouldBe(revision);
		Codes(question).ShouldBe(["woodside", "coopers", "mara"], ignoreOrder: true);
		question.Choice("coopers")!.LabelEn.ShouldBe("Cooper's Hill");
	}

	[Fact]
	public void GivenAnsweredQuestion_WhenNothingChanges_ThenNoRevisionIsCreated()
	{
		var question = Sites();

		Edit(question, answered: true, options: SiteOptions).ShouldBeSameAs(question);

		question.Revisions.Count.ShouldBe(1);
	}

	[Fact]
	public void GivenAnsweredQuestion_WhenWordingChanges_ThenReplacementCarriesEveryChoice()
	{
		// Given — a reporter addition, and a choice an Administrator removed
		var question = Sites();
		question.AddChoiceFromReporter("Mount 7", Locale.EnCa);
		Edit(question, answered: true, options: [SiteOptions[0], new("mount_7", "Mount 7", null)]);

		// When
		var replacement = Edit(question, answered: true, labelEn: "Where were you flying?");

		// Then
		replacement.ShouldNotBeSameAs(question);
		question.Deleted.ShouldNotBeNull();
		Codes(replacement).ShouldBe(["coopers", "mount_7"], ignoreOrder: true);
		replacement.Choice("mount_7")!.AddedByReporter.ShouldBeTrue();
		replacement.AllChoices.Single(choice => choice.Code == "woodside").Deleted.ShouldNotBeNull();
		Codes(question).ShouldBe(["coopers", "mount_7"], ignoreOrder: true);
	}

	[Fact]
	public void GivenAnsweredQuestion_WhenWordingAndChoicesChange_ThenReplacementTakesTheNewChoices()
	{
		var question = Sites(QuestionType.SingleSelect);

		var replacement = Edit(question, answered: true, labelEn: "Which site?", options: [new("mara", "Mara", "Mara")]);

		Codes(replacement).ShouldBe(["mara"], ignoreOrder: true);
		Codes(question).ShouldBe(["coopers", "woodside"], ignoreOrder: true);
	}

	[Fact]
	public void GivenRemovedChoice_WhenListed_ThenHiddenButKept()
	{
		var question = Sites(QuestionType.SingleSelect);

		question.ReplaceChoices([SiteOptions[0]], At);

		Codes(question).ShouldBe(["coopers"], ignoreOrder: true);
		question.AllChoices.Count.ShouldBe(2);
		question.OfferedChoiceLabelled("Woodside", Locale.EnCa).ShouldBeNull();
	}

	[Fact]
	public void GivenRemovedChoice_WhenAdministratorWritesItAgain_ThenSameRowIsRevived()
	{
		var question = Sites(QuestionType.SingleSelect);
		question.ReplaceChoices([SiteOptions[0]], At);

		question.ReplaceChoices(SiteOptions, At);

		Codes(question).ShouldBe(["coopers", "woodside"], ignoreOrder: true);
		question.AllChoices.Count.ShouldBe(2);
	}

	[Fact]
	public void GivenPinnedChoices_WhenListed_ThenPinnedFirstThenUnpinnedThenPinnedLast()
	{
		// Given
		var question = Question.Create(
			"country", QuestionType.SingleSelect, "Country", "Pays", At, isActive: true,
			options:
			[
				new QuestionOptionInput("other", "Other", "Autre", Pin: ChoicePin.Last),
				new QuestionOptionInput("mexico", "Mexico", "Mexique"),
				new QuestionOptionInput("canada", "Canada", "Canada", Pin: ChoicePin.First),
			]);

		// When
		var listed = question.Choices;

		// Then
		listed.Select(choice => choice.Code).ShouldBe(["canada", "mexico", "other"]);
	}

	[Fact]
	public void GivenPinnedChoice_WhenAnsweredQuestionForks_ThenReplacementKeepsThePin()
	{
		// Given
		var question = Sites(QuestionType.SingleSelect);
		question.ReplaceChoices([SiteOptions[0], SiteOptions[1] with { Pin = ChoicePin.Last }], At);

		// When
		var replacement = Edit(question, answered: true, labelEn: "Where did it happen?");

		// Then
		replacement.ShouldNotBeSameAs(question);
		replacement.Choice("woodside")!.Pin.ShouldBe(ChoicePin.Last);
		replacement.Choice("coopers")!.Pin.ShouldBe(ChoicePin.None);
	}

	[Fact]
	public void GivenRemovedChoice_WhenWrittenAgainPinned_ThenRevivedWithThePin()
	{
		// Given
		var question = Sites(QuestionType.SingleSelect);
		question.ReplaceChoices([SiteOptions[0]], At);

		// When
		question.ReplaceChoices([SiteOptions[0], SiteOptions[1] with { Pin = ChoicePin.First }], At);

		// Then
		question.Choice("woodside")!.Pin.ShouldBe(ChoicePin.First);
	}

	[Fact]
	public void GivenPinnedOption_WhenReplaced_ThenReplacementTakesThePinSaved()
	{
		// Given
		var question = Sites(QuestionType.SingleSelect);

		// When
		question.ReplaceChoices(
			[SiteOptions[0], new QuestionOptionInput("woodside", "Woodside Ridge", "Crête Woodside", Replace: true, Pin: ChoicePin.Last)],
			At);

		// Then
		question.Choices.Single(choice => choice.LabelEn == "Woodside Ridge").Pin.ShouldBe(ChoicePin.Last);
	}

	[Fact]
	public void GivenTwoChoicesWithOneCode_WhenSaved_ThenRefused()
	{
		var question = Sites(QuestionType.SingleSelect);

		Should.Throw<DomainRuleViolationException>(() =>
			question.ReplaceChoices([new("mara", "Mara", "Mara"), new("mara", "Mara again", "Mara encore")], At));
	}

	[Theory]
	[InlineData(QuestionType.ShortText)]
	[InlineData(QuestionType.YesNo)]
	public void GivenTypeWithoutChoices_WhenGivenChoices_ThenRefused(QuestionType type)
	{
		Should.Throw<DomainRuleViolationException>(() => Question.Create(
			"q", type, "Q", "Q", At, options: SiteOptions));
	}

	[Fact]
	public void GivenQuestionWithChoices_WhenRetypedToText_ThenItsChoicesMustGo()
	{
		var question = Sites(QuestionType.SingleSelect);
		var current = question.CurrentRevision;

		Should.Throw<DomainRuleViolationException>(() => question.ApplyEdit(
			false, QuestionType.ShortText, current.LabelEn, current.LabelFr, current.IsPrivate, current.IsActive,
			current.DisplayOrder, At));

		question.ApplyEdit(
			false, QuestionType.ShortText, current.LabelEn, current.LabelFr, current.IsPrivate, current.IsActive,
			current.DisplayOrder, At, options: []);
		question.Choices.ShouldBeEmpty();
	}

	[Fact]
	public void GivenChoiceWrittenByAdministrator_WhenSavedInOneLanguage_ThenRefused()
	{
		var question = Sites(QuestionType.SingleSelect);

		Should.Throw<DomainRuleViolationException>(() =>
			question.ReplaceChoices([.. SiteOptions, new("mara", "Mara", null)], At));
	}

	[Fact]
	public void GivenValueTypeAheadDoesNotOffer_WhenReporterSubmits_ThenAddedInTheirLanguageOnly()
	{
		// Given
		var question = Sites();

		// When
		var added = question.AddChoiceFromReporter("  Mount 7 ", Locale.EnCa);

		// Then
		added.Code.ShouldBe("mount_7");
		added.LabelEn.ShouldBe("Mount 7");
		added.LabelFr.ShouldBeNull();
		added.AddedByReporter.ShouldBeTrue();
		added.ReporterLocale.ShouldBe(Locale.EnCa);
		added.NeedsTranslation.ShouldBeTrue();
		question.ReporterChoicesAwaitingReview.ShouldBe(1);
		Codes(question).ShouldBe(["coopers", "woodside", "mount_7"], ignoreOrder: true);
	}

	[Fact]
	public void GivenFrenchReporter_WhenTheyAddAChoice_ThenRecordedInFrenchWithCodeFromTheFrench()
	{
		var added = Sites().AddChoiceFromReporter("Élévation Sainte-Anne", Locale.FrCa);

		added.Code.ShouldBe("elevation_sainte_anne");
		added.LabelFr.ShouldBe("Élévation Sainte-Anne");
		added.LabelEn.ShouldBeNull();
		added.ReporterLocale.ShouldBe(Locale.FrCa);
	}

	[Fact]
	public void GivenOneLanguageChoice_WhenReadInTheOther_ThenOfferedInTheLanguageItHas()
	{
		var question = Sites();
		var added = question.AddChoiceFromReporter("Mount 7", Locale.EnCa);

		added.Label(Locale.FrCa).ShouldBe("Mount 7");
		question.OfferedChoiceLabelled("Mount 7", Locale.FrCa).ShouldNotBeNull();
	}

	[Fact]
	public void GivenOneLanguageChoice_WhenAdministratorSuppliesTheOther_ThenItHasBothAndStaysFlaggedForReview()
	{
		// Given
		var question = Sites();
		question.AddChoiceFromReporter("Mount 7", Locale.EnCa);
		var revision = question.CurrentRevision.Id;

		// When
		var live = Edit(question, answered: true, options: [.. SiteOptions, new("mount_7", "Mount 7", "Mont 7")]);

		// Then
		live.ShouldBeSameAs(question);
		question.CurrentRevision.Id.ShouldBe(revision);
		question.Choice("mount_7")!.Label(Locale.FrCa).ShouldBe("Mont 7");
		question.Choice("mount_7")!.AddedByReporter.ShouldBeTrue();

		// Supplying a language is not a review: that is a Safety Officer's or an
		// Administrator's explicit approval, correction, or removal (ADR-0129).
		question.ReporterChoicesAwaitingReview.ShouldBe(1);
	}

	[Fact]
	public void GivenOneLanguageChoice_WhenOtherChoicesAreSaved_ThenItMayStayOneLanguage()
	{
		var question = Sites();
		question.AddChoiceFromReporter("Mount 7", Locale.EnCa);

		question.ReplaceChoices([new("mount_7", "Mount 7", null), .. SiteOptions], At);

		Codes(question).ShouldBe(["mount_7", "coopers", "woodside"], ignoreOrder: true);
		question.Choice("mount_7")!.NeedsTranslation.ShouldBeTrue();
	}

	[Fact]
	public void GivenChoiceWordedThatWayInEitherLanguage_WhenReporterTypesIt_ThenExistingChoiceIsReused()
	{
		var question = Sites();
		question.ReplaceChoices([.. SiteOptions, new("mount_7", "Mount 7", "Mont 7")], At);

		question.AddChoiceFromReporter("mont 7", Locale.EnCa).Code.ShouldBe("mount_7");

		question.Choices.Count.ShouldBe(3);
		question.Choice("mount_7")!.AddedByReporter.ShouldBeFalse();
	}

	[Fact]
	public void GivenTwoReportersNamingSameNewSite_WhenBothSubmit_ThenOneChoiceExists()
	{
		var question = Sites();

		var first = question.AddChoiceFromReporter("Mount 7", Locale.EnCa);
		var second = question.AddChoiceFromReporter("Mount 7", Locale.EnCa);

		second.ShouldBeSameAs(first);
		question.Choices.Count.ShouldBe(3);
	}

	[Fact]
	public void GivenChoiceAdministratorRemoved_WhenReporterRetypes_ThenNotRevived()
	{
		var question = Sites();
		question.ReplaceChoices([SiteOptions[0]], At);

		question.AddChoiceFromReporter("Woodside", Locale.EnCa);

		Codes(question).ShouldBe(["coopers"], ignoreOrder: true);
	}

	[Theory]
	[InlineData(QuestionType.SingleSelect)]
	[InlineData(QuestionType.MultiSelect)]
	public void GivenClosedListType_WhenReporterAddsChoice_ThenRefused(QuestionType type)
	{
		var question = Sites(type);

		Should.Throw<DomainRuleViolationException>(() => question.AddChoiceFromReporter("Mount 7", Locale.EnCa));
		question.Choices.Count.ShouldBe(2);
	}

	[Fact]
	public void GivenBlankValue_WhenReporterAddsChoice_ThenRefused()
	{
		Should.Throw<DomainRuleViolationException>(() => Sites().AddChoiceFromReporter("   ", Locale.EnCa));
	}

	[Fact]
	public void GivenChoiceLiveQuestionDependsOn_WhenParentSavedWithoutIt_ThenRefusedNamingDependent()
	{
		// Given
		var parent = Question.Create(
			"aircraft", QuestionType.SingleSelect, "Aircraft", "Aéronef", At, isActive: true,
			options: [new("hang_glider", "Hang glider", "Deltaplane"), new("paraglider", "Paraglider", "Parapente")]);
		var child = Question.Create(
			"wing_rating", QuestionType.ShortText, "Wing rating", "Homologation", At, isActive: true,
			dependsOnQuestionId: parent.Id, dependsOnChoiceId: parent.Choice("paraglider")!.Id);

		// When
		var refusal = Should.Throw<DomainRuleViolationException>(() =>
			QuestionDependencies.EnsureChoicesRemovable([parent, child], parent, ["hang_glider"]));

		// Then
		refusal.Message.ShouldContain("Wing rating");
		QuestionDependencies.EnsureChoicesRemovable([parent, child], parent, ["hang_glider", "paraglider"]);
	}

	[Fact]
	public void GivenReporterAddedChoice_WhenBothLanguagesAreBlanked_ThenRefused()
	{
		// Given
		var question = Sites();
		question.AddChoiceFromReporter("Mount 7", Locale.EnCa);

		// When / Then — a reporter choice may lack one language, never both
		Should.Throw<DomainRuleViolationException>(() =>
			question.ReplaceChoices([.. SiteOptions, new("mount_7", " ", null)], At));
		question.Choice("mount_7")!.LabelEn.ShouldBe("Mount 7");
	}

	[Fact]
	public void GivenFrenchOnlyChoice_WhenReadInEnglish_ThenOfferedInFrench()
	{
		var added = Sites().AddChoiceFromReporter("Élévation", Locale.FrCa);

		added.Label(Locale.EnCa).ShouldBe("Élévation");
	}

	[Fact]
	public void GivenDependencyOnAChoiceAlreadyRemoved_WhenParentIsSaved_ThenNotRefused()
	{
		// Given — a dependency left naming a choice removed before this save
		var parent = Question.Create(
			"aircraft", QuestionType.SingleSelect, "Aircraft", "Aéronef", At, isActive: true,
			options: [new QuestionOptionInput("hang_glider", "Hang glider", "Deltaplane"), new QuestionOptionInput("paraglider", "Paraglider", "Parapente")]);
		var child = Question.Create(
			"wing_rating", QuestionType.ShortText, "Wing rating", "Homologation", At, isActive: true,
			dependsOnQuestionId: parent.Id, dependsOnChoiceId: parent.Choice("paraglider")!.Id);
		parent.ReplaceChoices([new QuestionOptionInput("hang_glider", "Hang glider", "Deltaplane")], At);

		// When / Then — this save removes nothing the child still depends on
		Should.NotThrow(() => QuestionDependencies.EnsureChoicesRemovable([parent, child], parent, ["hang_glider"]));
	}

	[Fact]
	public void GivenConditionalQuestion_WhenSavedWithTheSameRequiredChoice_ThenNoRevisionIsCreated()
	{
		// Given — a single-select condition, as the editor sends it back
		var parent = Question.Create(
			"aircraft", QuestionType.SingleSelect, "Aircraft", "Aéronef", At, isActive: true,
			options: [new QuestionOptionInput("paraglider", "Paraglider", "Parapente")]);
		var paraglider = parent.Choice("paraglider")!.Id;
		var child = Question.Create(
			"wing_rating", QuestionType.ShortText, "Wing rating", "Homologation", At, isActive: true,
			dependsOnQuestionId: parent.Id, dependsOnChoiceId: paraglider);
		var current = child.CurrentRevision;

		// When
		var live = child.ApplyEdit(
			true, current.Type, current.LabelEn, current.LabelFr, current.IsPrivate, current.IsActive,
			current.DisplayOrder, At.AddDays(1), dependsOnQuestionId: parent.Id, dependsOnChoiceId: paraglider);

		// Then — nothing about the question changed, so nothing is revised or forked
		live.ShouldBeSameAs(child);
		child.Revisions.Count.ShouldBe(1);
	}
}
