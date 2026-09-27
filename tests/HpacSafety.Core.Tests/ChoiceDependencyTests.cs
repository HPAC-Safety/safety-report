using HpacSafety.Core.Features.QuestionBank;
using HpacSafety.Core.Features.Reporting;
using Shouldly;

namespace HpacSafety.Core.Tests;

/// <summary>
///     A question's choices depending on another question's answer (ADR-0146,
///     ADR-0151), each choice under one or more parent choices: the
///     rules on one question, and those <see cref="ChoiceDependencies" /> checks
///     across two. Every question and choice here is synthetic.
/// </summary>
public class ChoiceDependencyTests
{
	private static readonly DateTimeOffset At = new(2026, 9, 26, 12, 0, 0, TimeSpan.Zero);
	private const string Reviewer = "reviewer-subject";

	private static Question Make(QuestionType type = QuestionType.SingleSelect,
								 int displayOrder = 0)
	{
		return Question.Create(
			"make", type, "Make", "Marque", At, isActive: true, displayOrder: displayOrder,
			options: [new QuestionOptionInput("niviuk", "Niviuk", "Niviuk"), new QuestionOptionInput("ozone", "Ozone", "Ozone")]);
	}

	private static Question Model(Question make,
								  QuestionType type = QuestionType.Autocomplete,
								  int displayOrder = 1)
	{
		return Question.Create(
			"model", type, "Model", "Modèle", At, isActive: true, displayOrder: displayOrder,
			choicesDependOnQuestionId: make.Id,
			options:
			[
				new QuestionOptionInput("mentor_7", "Mentor 7", "Mentor 7", ParentChoiceIds: [make.Choice("niviuk")!.Id]),
				new QuestionOptionInput("rush_6", "Rush 6", "Rush 6", ParentChoiceIds: [make.Choice("ozone")!.Id]),
			]);
	}

	private static QuestionOptionInput[] Kept(Question question,
											  params (string Code, TinyId[] Parents)[] relinked)
	{
		return
		[
			.. question.Choices.Select(choice => new QuestionOptionInput(
				choice.Code, choice.LabelEn, choice.LabelFr,
				ParentChoiceIds: relinked.Any(pair => pair.Code == choice.Code)
					? relinked.First(pair => pair.Code == choice.Code).Parents
					: choice.ParentChoiceIds)),
		];
	}

	[Fact]
	public void GivenDependentQuestion_WhenChoiceSavedWithoutParentChoice_ThenRefusedNamingIt()
	{
		// Given
		var make = Make();
		var model = Model(make);

		// When
		var refusal = Should.Throw<DomainRuleViolationException>(() =>
			model.ReplaceChoices([.. Kept(model), new QuestionOptionInput("zeno_2", "Zeno 2", "Zeno 2")], At));

		// Then
		refusal.Message.ShouldContain("'Zeno 2'");
	}

	[Fact]
	public void GivenDependentQuestion_WhenChoiceSavedUnderTwoParentChoices_ThenOneChoiceIsOfferedUnderBoth()
	{
		// Given
		var make = Make();
		var model = Model(make);
		var (niviuk, ozone) = (make.Choice("niviuk")!.Id, make.Choice("ozone")!.Id);

		// When
		model.ReplaceChoices([.. Kept(model), new QuestionOptionInput("other", "Other", "Autre", ParentChoiceIds: [niviuk, ozone])], At);

		// Then
		var other = model.Choices.Single(choice => choice.LabelEn == "Other");
		other.ParentChoiceIds.ShouldBe(new[] { niviuk, ozone }.OrderBy(id => id.Value, StringComparer.Ordinal));
		other.IsOfferedUnder(niviuk).ShouldBeTrue();
		other.IsOfferedUnder(ozone).ShouldBeTrue();
	}

	[Theory]
	[InlineData("Other", "Autre")]
	[InlineData("  other ", "Something else")]
	[InlineData("Anything", "AUTRE")]
	public void GivenDependentQuestion_WhenSameWordingSavedTwice_ThenRefusedWhateverParents(string labelEn,
																							 string labelFr)
	{
		// Given
		var make = Make();
		var model = Model(make);

		// When / Then
		Should.Throw<DomainRuleViolationException>(() => model.ReplaceChoices(
			[
				.. Kept(model),
				new QuestionOptionInput("other", "Other", "Autre", ParentChoiceIds: [make.Choice("niviuk")!.Id]),
				new QuestionOptionInput("other_2", labelEn, labelFr, ParentChoiceIds: [make.Choice("ozone")!.Id]),
			], At)).Message.ShouldContain("offered twice");
	}

	[Fact]
	public void GivenDependentQuestion_WhenWordingDiffersOnlyInInnerWhitespace_ThenRefused()
	{
		// Given
		var make = Make();
		var model = Model(make);

		// When / Then
		Should.Throw<DomainRuleViolationException>(() => model.ReplaceChoices(
			[.. Kept(model), new QuestionOptionInput("mentor_7_x", "Mentor   7", "Mentor  7 bis", ParentChoiceIds: [make.Choice("ozone")!.Id])],
			At)).Message.ShouldContain("'Mentor");
	}

	[Fact]
	public void GivenQuestion_WhenChoicesDependOnItself_ThenRefused()
	{
		// Given
		var make = Make();

		// When / Then
		Should.Throw<DomainRuleViolationException>(() => make.DependChoicesOn(make.Id));
	}

	[Theory]
	[InlineData(QuestionType.MultiSelect, QuestionType.Autocomplete)]
	[InlineData(QuestionType.SingleSelect, QuestionType.MultiSelect)]
	[InlineData(QuestionType.YesNo, QuestionType.SingleSelect)]
	public void GivenTypeThatTakesNoPart_WhenDependencyChecked_ThenRefused(QuestionType parentType,
																			QuestionType childType)
	{
		// Given
		var parent = parentType == QuestionType.YesNo
			? Question.Create("make", parentType, "Make", "Marque", At, isActive: true)
			: Make(parentType);

		// When / Then
		Should.Throw<DomainRuleViolationException>(() =>
			ChoiceDependencies.EnsureDependencyAllowed([parent], null, "Model", childType, 5, parent.Id));
	}

	[Fact]
	public void GivenParentThatDependsOnAnother_WhenChosenAsParent_ThenRefusedAsTooDeep()
	{
		// Given
		var make = Make();
		var model = Model(make);

		// When / Then
		Should.Throw<DomainRuleViolationException>(() =>
				ChoiceDependencies.EnsureDependencyAllowed([make, model], null, "Size", QuestionType.Autocomplete, 5, model.Id))
			.Message.ShouldContain("one level deep");
	}

	[Fact]
	public void GivenQuestionOthersDependOn_WhenGivenParent_ThenRefusedAsTooDeep()
	{
		// Given
		var make = Make();
		var model = Model(make);
		var wing = Question.Create(
			"wing", QuestionType.SingleSelect, "Wing", "Aile", At, isActive: true, displayOrder: -1,
			options: [new QuestionOptionInput("paraglider", "Paraglider", "Parapente")]);

		// When / Then
		Should.Throw<DomainRuleViolationException>(() =>
				ChoiceDependencies.EnsureDependencyAllowed([make, model, wing], make.Id, "Make", QuestionType.SingleSelect, 0, wing.Id))
			.Message.ShouldContain("one level deep");
	}

	[Fact]
	public void GivenParentAfterChild_WhenDependencyChecked_ThenRefusedNamingBoth()
	{
		// Given
		var make = Make(displayOrder: 7);

		// When
		var refusal = Should.Throw<DomainRuleViolationException>(() =>
			ChoiceDependencies.EnsureDependencyAllowed([make], null, "Model", QuestionType.Autocomplete, 3, make.Id));

		// Then
		refusal.Message.ShouldContain("'Make'");
		refusal.Message.ShouldContain("'Model'");
	}

	[Fact]
	public void GivenRetiredParent_WhenDependencyChecked_ThenRefused()
	{
		// Given
		var make = Make();
		make.Delete(false, At);

		// When / Then
		Should.Throw<DomainRuleViolationException>(() =>
			ChoiceDependencies.EnsureDependencyAllowed([make], null, "Model", QuestionType.Autocomplete, 3, make.Id));
	}

	[Fact]
	public void GivenOrderPuttingChildFirst_WhenChecked_ThenRefusedNamingBoth()
	{
		// Given
		var make = Make();
		var model = Model(make);

		// When
		var refusal = Should.Throw<DomainRuleViolationException>(() => ChoiceDependencies.EnsureOrder([model, make]));

		// Then
		refusal.Message.ShouldContain("'Make' must come before 'Model'");
		Should.NotThrow(() => ChoiceDependencies.EnsureOrder([make, model]));
	}

	[Fact]
	public void GivenParentWithDependent_WhenRetypedToText_ThenRefused()
	{
		// Given
		var make = Make();
		var model = Model(make);

		// When / Then
		Should.Throw<DomainRuleViolationException>(() =>
			ChoiceDependencies.EnsureParentKeepsType([make, model], make, QuestionType.ShortText));
		Should.NotThrow(() => ChoiceDependencies.EnsureParentKeepsType([make, model], make, QuestionType.Autocomplete));
	}

	[Fact]
	public void GivenLinkToAnotherQuestionsChoice_WhenLinksChecked_ThenRefused()
	{
		// Given
		var make = Make();
		var elsewhere = Question.Create(
			"elsewhere", QuestionType.SingleSelect, "Elsewhere", "Ailleurs", At, isActive: true,
			options: [new QuestionOptionInput("gin", "Gin", "Gin")]);
		var model = Model(make);
		model.ReplaceChoices(Kept(model, ("mentor_7", [make.Choice("niviuk")!.Id, elsewhere.Choice("gin")!.Id])), At);

		// When / Then
		Should.Throw<DomainRuleViolationException>(() => ChoiceDependencies.EnsureLinksAllowed([make, elsewhere, model], model))
			.Message.ShouldContain("'Mentor 7'");
		Should.NotThrow(() => ChoiceDependencies.EnsureLinksAllowed([make, elsewhere, model], make));
		Should.Throw<DomainRuleViolationException>(() => ChoiceDependencies.EnsureOfferable([make, elsewhere, model], make.Id, [elsewhere.Choice("gin")!.Id]));
		Should.NotThrow(() => ChoiceDependencies.EnsureOfferable([make, elsewhere, model], make.Id, [make.Choice("ozone")!.Id]));
	}

	[Fact]
	public void GivenLinkToRemovedParentChoiceBesideLiveOne_WhenLinksChecked_ThenAllowed()
	{
		// Given — "Niviuk" was removed while "Mentor 7" is also offered under "Ozone"
		var make = Make();
		var model = Model(make);
		var (niviuk, ozone) = (make.Choice("niviuk")!.Id, make.Choice("ozone")!.Id);
		model.ReplaceChoices(Kept(model, ("mentor_7", [niviuk, ozone])), At);
		make.ReplaceChoices([new QuestionOptionInput("ozone", "Ozone", "Ozone")], At);

		// When / Then — the inert link stays, and filters nothing
		Should.NotThrow(() => ChoiceDependencies.EnsureLinksAllowed([make, model], model));
		model.Choice("mentor_7")!.ParentChoiceIds.ShouldContain(niviuk);
		Should.Throw<DomainRuleViolationException>(() => ChoiceDependencies.EnsureOfferable([make, model], make.Id, [niviuk]));
	}

	[Fact]
	public void GivenParentChoiceSomeChildIsOfferedUnderAlone_WhenRemoved_ThenRefusedNamingThatChild()
	{
		// Given — "Other" is under both makes, "Mentor 7" under Niviuk only
		var make = Make();
		var model = Model(make);
		var (niviuk, ozone) = (make.Choice("niviuk")!.Id, make.Choice("ozone")!.Id);
		model.ReplaceChoices([.. Kept(model), new QuestionOptionInput("other", "Other", "Autre", ParentChoiceIds: [niviuk, ozone])], At);

		// When
		var refusal = Should.Throw<DomainRuleViolationException>(() =>
			ChoiceDependencies.EnsureParentChoicesRemovable([make, model], make, ["ozone"]));

		// Then
		refusal.Message.ShouldContain("'Mentor 7'");
		refusal.Message.ShouldNotContain("'Other'");
		refusal.Message.ShouldContain("'Model'");
		Should.Throw<DomainRuleViolationException>(() =>
			ChoiceDependencies.EnsureValueRemovable([make, model], make, niviuk));
		Should.NotThrow(() => ChoiceDependencies.EnsureParentChoicesRemovable([make, model], make, ["niviuk", "ozone"]));
	}

	[Fact]
	public void GivenEveryChildUnderParentChoiceHasAnotherParent_WhenRemoved_ThenAllowedAndLinksStay()
	{
		// Given
		var make = Make();
		var model = Model(make);
		var (niviuk, ozone) = (make.Choice("niviuk")!.Id, make.Choice("ozone")!.Id);
		model.ReplaceChoices(Kept(model, ("mentor_7", [niviuk, ozone])), At);

		// When
		ChoiceDependencies.EnsureParentChoicesRemovable([make, model], make, ["ozone"]);
		ChoiceDependencies.EnsureValueRemovable([make, model], make, niviuk);
		make.ReplaceChoices([new QuestionOptionInput("ozone", "Ozone", "Ozone")], At);

		// Then
		make.Choice("niviuk").ShouldBeNull();
		model.Choice("mentor_7")!.ParentChoiceIds.ShouldContain(niviuk);
	}

	[Fact]
	public void GivenReplacedParentChoice_WhenFollowed_ThenLinkNamesReplacement()
	{
		// Given
		var make = Make();
		var model = Model(make);
		make.ReplaceChoices(
			[new QuestionOptionInput("niviuk", "Niviuk Gliders", "Niviuk Gliders", Replace: true), new QuestionOptionInput("ozone", "Ozone", "Ozone")],
			At);

		// When
		ChoiceDependencies.Follow([make, model], make, At);

		// Then
		model.Choice("mentor_7")!.ParentChoiceIds.ShouldBe([make.Choice("niviuk_gliders")!.Id]);
		model.Choice("rush_6")!.ParentChoiceIds.ShouldBe([make.Choice("ozone")!.Id]);
	}

	[Fact]
	public void GivenMergedParentValue_WhenFollowed_ThenLinkNamesMergeTarget()
	{
		// Given
		var make = Make(QuestionType.Autocomplete);
		var model = Model(make);
		make.MergeValue(make.Choice("niviuk")!.Id, make.Choice("ozone")!.Id, Reviewer, At);

		// When
		ChoiceDependencies.Follow([make, model], make, At);

		// Then
		model.Choice("mentor_7")!.ParentChoiceIds.ShouldBe([make.Choice("ozone")!.Id]);
	}

	[Fact]
	public void GivenForkedParent_WhenFollowed_ThenDependencyAndLinksNameReplacement()
	{
		// Given
		var make = Make();
		var model = Model(make);
		var revision = model.CurrentRevision.Id;
		var replacement = make.ApplyEdit(true, QuestionType.SingleSelect, "Wing make", "Marque", true, true, 0, At);

		// When
		ChoiceDependencies.Follow([make, model, replacement], replacement, At);

		// Then
		replacement.ShouldNotBeSameAs(make);
		model.ChoicesDependOnQuestionId.ShouldBe(replacement.Id);
		model.Choice("mentor_7")!.ParentChoiceIds.ShouldBe([replacement.Choice("niviuk")!.Id]);
		model.CurrentRevision.Id.ShouldBe(revision);
	}

	[Fact]
	public void GivenForkedChild_WhenApplied_ThenCopyKeepsDependencyAndLinks()
	{
		// Given — "Mentor 7" is under both makes, and its "Ozone" link was once unticked
		var make = Make();
		var model = Model(make);
		var (niviuk, ozone) = (make.Choice("niviuk")!.Id, make.Choice("ozone")!.Id);
		model.ReplaceChoices(Kept(model, ("rush_6", [niviuk, ozone])), At);
		model.ReplaceChoices(Kept(model, ("rush_6", [niviuk])), At);

		// When
		var replacement = model.ApplyEdit(true, QuestionType.Autocomplete, "Wing model", "Modèle", true, true, 1, At);

		// Then — every link crosses as a row of the copy, the stamped one included
		replacement.ChoicesDependOnQuestionId.ShouldBe(make.Id);
		replacement.Choice("mentor_7")!.ParentChoiceIds.ShouldBe([niviuk]);
		var rush = replacement.Choice("rush_6")!;
		rush.ParentChoiceIds.ShouldBe([niviuk]);
		rush.ParentLinks.Count.ShouldBe(2);
		rush.ParentLinks.ShouldAllBe(link => link.ChoiceId == rush.Id);
	}

	[Fact]
	public void GivenDependentTypeAhead_WhenReporterTypesWordingUnderAnotherParentChoice_ThenThatValueGainsLinkAndIsFlagged()
	{
		// Given
		var make = Make();
		var model = Model(make);
		var (niviuk, ozone) = (make.Choice("niviuk")!.Id, make.Choice("ozone")!.Id);
		var mentor = model.Choice("mentor_7")!;

		// When
		var typed = model.AddChoiceFromReporter(" mentor  7 ", Locale.EnCa, At, ozone);

		// Then — one wording, one value, now under both makes, for a reviewer to see
		typed.ShouldBeSameAs(mentor);
		mentor.ParentChoiceIds.ShouldBe(new[] { niviuk, ozone }.OrderBy(id => id.Value, StringComparer.Ordinal));
		mentor.NeedsReview.ShouldBeTrue();
		model.Choices.Count.ShouldBe(2);
	}

	[Fact]
	public void GivenDependentTypeAhead_WhenReporterTypesValueAlreadyUnderAnswer_ThenNamedWithoutFlag()
	{
		// Given
		var make = Make();
		var model = Model(make);

		// When
		var typed = model.AddChoiceFromReporter("RUSH 6", Locale.EnCa, At, make.Choice("ozone")!.Id);

		// Then
		typed.ShouldBeSameAs(model.Choice("rush_6"));
		typed.NeedsReview.ShouldBeFalse();
	}

	[Fact]
	public void GivenDependentTypeAhead_WhenReporterTypesMergedValue_ThenTargetGainsLinkAndIsFlagged()
	{
		// Given
		var make = Make();
		var model = Model(make);
		var (niviuk, ozone) = (make.Choice("niviuk")!.Id, make.Choice("ozone")!.Id);
		var typo = model.AddChoiceFromReporter("Mentr 7", Locale.EnCa, At, niviuk);
		model.MergeValue(typo.Id, model.Choice("mentor_7")!.Id, Reviewer, At);

		// When
		var typed = model.AddChoiceFromReporter("mentr 7", Locale.EnCa, At, ozone);

		// Then
		typed.ShouldBeSameAs(model.Choice("mentor_7"));
		typed.IsOfferedUnder(ozone).ShouldBeTrue();
		typed.NeedsReview.ShouldBeTrue();
	}

	[Fact]
	public void GivenDependentTypeAhead_WhenReporterTypesRemovedValue_ThenFlaggedNotRevived()
	{
		// Given
		var make = Make();
		var model = Model(make);
		var ozone = make.Choice("ozone")!.Id;
		var zeno = model.AddChoiceFromReporter("Zeno 1", Locale.EnCa, At, make.Choice("niviuk")!.Id);
		model.RemoveValue(zeno.Id, Reviewer, At);

		// When
		var typed = model.AddChoiceFromReporter("zeno 1", Locale.EnCa, At, ozone);

		// Then
		typed.ShouldBeSameAs(zeno);
		typed.Deleted.ShouldNotBeNull();
		typed.NeedsReview.ShouldBeTrue();
		typed.IsOfferedUnder(ozone).ShouldBeFalse();
	}

	[Fact]
	public void GivenDependentTypeAhead_WhenMergingValuesUnderDifferentParentChoices_ThenSurvivorIsOfferedUnderBoth()
	{
		// Given
		var make = Make();
		var model = Model(make);
		var (niviuk, ozone) = (make.Choice("niviuk")!.Id, make.Choice("ozone")!.Id);

		// When
		model.MergeValue(model.Choice("mentor_7")!.Id, model.Choice("rush_6")!.Id, Reviewer, At);

		// Then
		model.Choice("rush_6")!.ParentChoiceIds.ShouldBe(new[] { niviuk, ozone }.OrderBy(id => id.Value, StringComparer.Ordinal));
	}

	[Fact]
	public void GivenDependentTypeAhead_WhenReviewerSetsValueParents_ThenUntickedLinkIsStampedAndValueReviewed()
	{
		// Given
		var make = Make();
		var model = Model(make);
		var value = model.Choice("rush_6")!;
		var (niviuk, ozone) = (make.Choice("niviuk")!.Id, make.Choice("ozone")!.Id);
		var ozoneLink = value.ParentLinks.Single(link => link.ParentChoiceId == ozone);

		// When
		model.OfferValueUnder(value.Id, [niviuk], Reviewer, At);

		// Then
		value.ParentChoiceIds.ShouldBe([niviuk]);
		ozoneLink.Deleted.ShouldBe(At);
		value.ReviewedBy.ShouldBe(Reviewer);

		// And ticking it again restores the same row
		model.OfferValueUnder(value.Id, [niviuk, ozone], Reviewer, At);
		value.ParentLinks.Count.ShouldBe(2);
		ozoneLink.Deleted.ShouldBeNull();
	}

	[Fact]
	public void GivenDependentTypeAhead_WhenReviewerLeavesValueUnderNothing_ThenRefused()
	{
		// Given
		var make = Make();
		var model = Model(make);

		// When / Then
		Should.Throw<DomainRuleViolationException>(() => model.OfferValueUnder(model.Choice("rush_6")!.Id, [], Reviewer, At))
			.Message.ShouldContain("at least one");
	}

	[Fact]
	public void GivenIndependentTypeAhead_WhenReviewerRelinksValue_ThenRefused()
	{
		// Given
		var make = Make(QuestionType.Autocomplete);

		// When / Then
		Should.Throw<DomainRuleViolationException>(() =>
			make.OfferValueUnder(make.Choice("niviuk")!.Id, [make.Choice("ozone")!.Id], Reviewer, At));
	}

	[Fact]
	public void GivenTakenCode_WhenUnusedCodeAsked_ThenNextSuffixIsReturned()
	{
		// Given
		var make = Make();
		var model = Model(make);

		// When / Then
		model.UnusedCode("Mentor 7").ShouldBe("mentor_7_2");
		model.UnusedCode("Mentor 7", ["mentor_7_2"]).ShouldBe("mentor_7_3");
		model.UnusedCode("Zeno 2").ShouldBe("zeno_2");
	}

	[Fact]
	public void GivenClearedDependency_WhenChoiceAddedWithoutParent_ThenLinksAreKept()
	{
		// Given
		var make = Make();
		var model = Model(make);
		var niviuk = make.Choice("niviuk")!.Id;

		// When
		model.DependChoicesOn(null);
		model.ReplaceChoices(
			[
				new QuestionOptionInput("mentor_7", "Mentor 7", "Mentor 7"),
				new QuestionOptionInput("rush_6", "Rush 6", "Rush 6"),
				new QuestionOptionInput("zeno_2", "Zeno 2", "Zeno 2"),
			], At);

		// Then
		model.Choice("mentor_7")!.ParentChoiceIds.ShouldBe([niviuk]);
		model.Choice("zeno_2")!.ParentChoiceIds.ShouldBeEmpty();
	}

	[Fact]
	public void GivenChoice_WhenLinkedToItself_ThenRefused()
	{
		// Given
		var make = Make();
		var model = Model(make);
		var mentor = model.Choice("mentor_7")!;

		// When / Then
		Should.Throw<DomainRuleViolationException>(() => model.OfferValueUnder(mentor.Id, [mentor.Id], Reviewer, At));
	}

	[Fact]
	public void GivenQuestion_WhenDependencyOnItselfChecked_ThenRefused()
	{
		// Given
		var make = Make();

		// When / Then
		Should.Throw<DomainRuleViolationException>(() =>
			ChoiceDependencies.EnsureDependencyAllowed([make], make.Id, "Make", QuestionType.SingleSelect, 5, make.Id));
	}

	[Fact]
	public void GivenDependentPicker_WhenOptionReplacedWithoutNamingParent_ThenReplacementKeepsLink()
	{
		// Given
		var make = Make();
		var model = Model(make, QuestionType.SingleSelect);
		var niviuk = make.Choice("niviuk")!.Id;

		// When
		model.ReplaceChoices(
			[
				new QuestionOptionInput("mentor_7", "Mentor 7 Light", "Mentor 7 Light", Replace: true),
				new QuestionOptionInput("rush_6", "Rush 6", "Rush 6"),
			], At);

		// Then
		model.Choice("mentor_7_light")!.ParentChoiceIds.ShouldBe([niviuk]);
	}

	[Fact]
	public void GivenLinkNamingNoChoiceOfForkedParent_WhenFollowed_ThenLinkIsLeftAsItIs()
	{
		// Given
		var make = Make();
		var elsewhere = Question.Create(
			"elsewhere", QuestionType.SingleSelect, "Elsewhere", "Ailleurs", At, isActive: true,
			options: [new QuestionOptionInput("gin", "Gin", "Gin")]);
		var model = Model(make);
		var gin = elsewhere.Choice("gin")!.Id;
		model.ReplaceChoices(Kept(model, ("mentor_7", [gin])), At);
		var replacement = make.ApplyEdit(true, QuestionType.SingleSelect, "Wing make", "Marque", true, true, 0, At);

		// When
		ChoiceDependencies.Follow([make, model, replacement], replacement, At);

		// Then
		model.Choice("mentor_7")!.ParentChoiceIds.ShouldBe([gin]);
		model.Choice("rush_6")!.ParentChoiceIds.ShouldBe([replacement.Choice("ozone")!.Id]);
	}

	[Fact]
	public void GivenQuestionNothingDependsOn_WhenRetypedToText_ThenAllowed()
	{
		// Given
		var make = Make();

		// When / Then
		Should.NotThrow(() => ChoiceDependencies.EnsureParentKeepsType([make], make, QuestionType.ShortText));
	}

	[Fact]
	public void GivenRetiredParent_WhenLinksChecked_ThenRefused()
	{
		// Given
		var make = Make();
		var model = Model(make);
		make.Delete(false, At);

		// When / Then
		Should.Throw<DomainRuleViolationException>(() => ChoiceDependencies.EnsureLinksAllowed([make, model], model));
	}

	[Fact]
	public void GivenValueTypedWhileParentOffForm_WhenLinksChecked_ThenRefusedNamingIt()
	{
		// Given — a value a reporter typed with no parent answer to be offered under
		var make = Make();
		var model = Model(make);
		model.AddChoiceFromReporter("Zeno 2", Locale.EnCa, At);

		// When / Then
		Should.Throw<DomainRuleViolationException>(() => ChoiceDependencies.EnsureLinksAllowed([make, model], model))
			.Message.ShouldContain("'Zeno 2'");
	}

	[Fact]
	public void GivenParentNotLoaded_WhenFollowed_ThenDependentIsLeftAlone()
	{
		// Given
		var make = Make();
		var model = Model(make);
		var other = Make();

		// When
		ChoiceDependencies.Follow([model, other], other, At);

		// Then
		model.ChoicesDependOnQuestionId.ShouldBe(make.Id);
		model.Choice("mentor_7")!.ParentChoiceIds.ShouldBe([make.Choice("niviuk")!.Id]);
	}

	[Fact]
	public void GivenLinkNamingAnotherQuestionsChoice_WhenFollowedUnforked_ThenLinkIsLeftAsItIs()
	{
		// Given
		var make = Make();
		var elsewhere = Question.Create(
			"elsewhere", QuestionType.SingleSelect, "Elsewhere", "Ailleurs", At, isActive: true,
			options: [new QuestionOptionInput("gin", "Gin", "Gin")]);
		var model = Model(make);
		var gin = elsewhere.Choice("gin")!.Id;
		model.ReplaceChoices(Kept(model, ("mentor_7", [gin])), At);

		// When
		ChoiceDependencies.Follow([make, model], make, At);

		// Then
		model.Choice("mentor_7")!.ParentChoiceIds.ShouldBe([gin]);
	}

	[Fact]
	public void GivenDependentTypeAhead_WhenMergingValuesUnderOneParentChoice_ThenMerged()
	{
		// Given
		var make = Make();
		var model = Model(make);
		var ozone = make.Choice("ozone")!.Id;
		var typo = model.AddChoiceFromReporter("Rush6", Locale.EnCa, At, ozone);

		// When
		model.MergeValue(typo.Id, model.Choice("rush_6")!.Id, Reviewer, At);

		// Then
		typo.MergedIntoChoiceId.ShouldBe(model.Choice("rush_6")!.Id);
	}

	private static Question Group(string key,
								  int displayOrder)
	{
		return Question.Create(key, QuestionType.Group, key, key, At, isActive: true, isPrivate: false, displayOrder: displayOrder);
	}

	[Fact]
	public void GivenChildGroupedUnderEarlierGroup_WhenDependencyChecked_ThenRefusedAsAskedFirst()
	{
		// Given — the group's page comes before the make, whatever the child's own order
		var section = Group("section", 0);
		var make = Make(displayOrder: 1);

		// When / Then
		Should.Throw<DomainRuleViolationException>(() =>
				ChoiceDependencies.EnsureDependencyAllowed([section, make], null, "Model", QuestionType.Autocomplete, 9, make.Id, section.Id))
			.Message.ShouldContain("'Make' must come before 'Model'");
	}

	[Fact]
	public void GivenChildGroupedUnderLaterGroup_WhenDependencyChecked_ThenAllowed()
	{
		// Given
		var make = Make(displayOrder: 1);
		var section = Group("section", 2);

		// When / Then
		Should.NotThrow(() =>
			ChoiceDependencies.EnsureDependencyAllowed([section, make], null, "Model", QuestionType.Autocomplete, 0, make.Id, section.Id));
	}

	[Fact]
	public void GivenParentGroupedAfterItsChild_WhenSaved_ThenRefusedNamingBoth()
	{
		// Given
		var make = Make(displayOrder: 0);
		var model = Model(make, displayOrder: 1);
		var later = Group("later", 2);

		// When / Then
		Should.Throw<DomainRuleViolationException>(() => ChoiceDependencies.EnsureDependentsFollow([make, model, later], make, later.Id))
			.Message.ShouldContain("'Make' must come before 'Model'");
		Should.NotThrow(() => ChoiceDependencies.EnsureDependentsFollow([make, model, later], make, null));
	}

	[Fact]
	public void GivenOrderPuttingParentsGroupAfterChild_WhenChecked_ThenRefused()
	{
		// Given — the make renders on a group's page that now comes after the model
		var later = Group("later", 5);
		var make = Question.Create(
			"make", QuestionType.SingleSelect, "Make", "Marque", At, isActive: true, displayOrder: 0, groupedUnderQuestionId: later.Id,
			options: [new QuestionOptionInput("niviuk", "Niviuk", "Niviuk"), new QuestionOptionInput("ozone", "Ozone", "Ozone")]);
		var model = Model(make);

		// When / Then
		Should.Throw<DomainRuleViolationException>(() => ChoiceDependencies.EnsureOrder([make, model, later]));
		Should.NotThrow(() => ChoiceDependencies.EnsureOrder([later, make, model]));
	}

	[Fact]
	public void GivenChildUnderBothParentValues_WhenParentMergeFollowed_ThenLinksCollapseIntoOne()
	{
		// Given
		var make = Make(QuestionType.Autocomplete);
		var model = Model(make);
		var (niviuk, ozone) = (make.Choice("niviuk")!.Id, make.Choice("ozone")!.Id);
		model.ReplaceChoices([.. Kept(model), new QuestionOptionInput("other", "Other", "Autre", ParentChoiceIds: [niviuk, ozone])], At);
		make.MergeValue(niviuk, ozone, Reviewer, At);

		// When
		ChoiceDependencies.Follow([make, model], make, At);

		// Then — one live link to the target, and the old one stamped, not erased
		var other = model.Choice("other")!;
		other.ParentChoiceIds.ShouldBe([ozone]);
		other.ParentLinks.Single(link => link.ParentChoiceId == niviuk).Deleted.ShouldBe(At);
		model.Choice("mentor_7")!.ParentChoiceIds.ShouldBe([ozone]);
	}

	[Fact]
	public void GivenMergedValue_WhenRelinked_ThenRefused()
	{
		// Given
		var make = Make();
		var model = Model(make);
		var niviuk = make.Choice("niviuk")!.Id;
		var typo = model.AddChoiceFromReporter("Mentor7", Locale.EnCa, At, niviuk);
		model.MergeValue(typo.Id, model.Choice("mentor_7")!.Id, Reviewer, At);

		// When / Then
		Should.Throw<DomainRuleViolationException>(() => model.OfferValueUnder(typo.Id, [make.Choice("ozone")!.Id], Reviewer, At))
			.Message.ShouldContain("merged");
	}

	[Fact]
	public void GivenOtherValueUnlinked_WhenOneValuesParentsChecked_ThenOnlyTheyAreJudged()
	{
		// Given — a value typed while the make was off the form has no link yet
		var make = Make();
		var model = Model(make);
		model.AddChoiceFromReporter("Zeno 2", Locale.EnCa, At);

		// When / Then
		var rush = model.Choice("rush_6")!.Id;
		ChoiceDependencies.ValueParents([make, model], model, rush, [make.Choice("niviuk")!.Id]).ShouldBe([make.Choice("niviuk")!.Id]);
	}

	[Fact]
	public void GivenAnotherQuestionsChoice_WhenValueParentsChecked_ThenRefused()
	{
		// Given
		var make = Make();
		var elsewhere = Question.Create(
			"elsewhere", QuestionType.SingleSelect, "Elsewhere", "Ailleurs", At, isActive: true,
			options: [new QuestionOptionInput("gin", "Gin", "Gin")]);
		var model = Model(make);
		var gin = elsewhere.Choice("gin")!.Id;

		// When / Then
		var rush = model.Choice("rush_6")!.Id;
		Should.Throw<DomainRuleViolationException>(() => ChoiceDependencies.ValueParents([make, elsewhere, model], model, rush, [gin]));
		Should.Throw<DomainRuleViolationException>(() => ChoiceDependencies.ValueParents([make, elsewhere], elsewhere, elsewhere.Choice("gin")!.Id, [gin]));
		Should.Throw<DomainRuleViolationException>(() => ChoiceDependencies.ValueParents([make, model], model, rush, []))
			.Message.ShouldContain("at least one");
		make.Delete(false, At);
		Should.Throw<DomainRuleViolationException>(() => ChoiceDependencies.ValueParents([make, model], model, rush, [make.Choice("niviuk")!.Id]));
	}

	[Fact]
	public void GivenLinkToRemovedParentChoice_WhenSavedWithOnlyLiveTicks_ThenLinkIsKeptAndNotJudged()
	{
		// Given — "Mentor 7" under both makes, then "Niviuk" removed
		var make = Make();
		var model = Model(make);
		var (niviuk, ozone) = (make.Choice("niviuk")!.Id, make.Choice("ozone")!.Id);
		model.ReplaceChoices(Kept(model, ("mentor_7", [niviuk, ozone])), At);
		make.ReplaceChoices([new QuestionOptionInput("ozone", "Ozone", "Ozone")], At);
		var mentor = model.Choice("mentor_7")!;

		// When — the editor and the review page send only the live parents, or echo the removed one
		var fromEditor = ChoiceDependencies.WithStandingLinks([make, model], make.Id, mentor, [ozone]);
		var echoed = ChoiceDependencies.WithStandingLinks([make, model], make.Id, mentor, [niviuk, ozone]);

		// Then — the removed parent's link stays, but a fresh tick of it is refused
		fromEditor.ShouldBe([ozone, niviuk], ignoreOrder: true);
		echoed.ShouldBe([niviuk, ozone], ignoreOrder: true);
		Should.Throw<DomainRuleViolationException>(() =>
			ChoiceDependencies.WithStandingLinks([make, model], make.Id, model.Choice("rush_6"), [niviuk, ozone]));
		Should.Throw<DomainRuleViolationException>(() => ChoiceDependencies.ValueParents([make, model], model, mentor.Id, [niviuk]))
			.Message.ShouldContain("at least one");
	}
}
