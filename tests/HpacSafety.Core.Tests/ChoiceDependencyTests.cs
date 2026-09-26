using HpacSafety.Core.Features.QuestionBank;
using HpacSafety.Core.Features.Reporting;
using Shouldly;

namespace HpacSafety.Core.Tests;

/// <summary>
///     A question's choices depending on another question's answer (ADR-0146): the
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
				new QuestionOptionInput("mentor_7", "Mentor 7", "Mentor 7", ParentChoiceId: make.Choice("niviuk")!.Id),
				new QuestionOptionInput("rush_6", "Rush 6", "Rush 6", ParentChoiceId: make.Choice("ozone")!.Id),
			]);
	}

	private static QuestionOptionInput[] Kept(Question question,
											  params (string Code, TinyId Parent)[] relinked)
	{
		return
		[
			.. question.Choices.Select(choice => new QuestionOptionInput(
				choice.Code, choice.LabelEn, choice.LabelFr,
				ParentChoiceId: relinked.Any(pair => pair.Code == choice.Code)
					? relinked.First(pair => pair.Code == choice.Code).Parent
					: choice.ParentChoiceId)),
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
	public void GivenDependentQuestion_WhenSameWordingSavedUnderTwoParentChoices_ThenBothAreOffered()
	{
		// Given
		var make = Make();
		var model = Model(make);

		// When
		model.ReplaceChoices(
			[
				.. Kept(model),
				new QuestionOptionInput("other", "Other", "Autre", ParentChoiceId: make.Choice("niviuk")!.Id),
				new QuestionOptionInput("other_2", "Other", "Autre", ParentChoiceId: make.Choice("ozone")!.Id),
			], At);

		// Then
		model.Choices.Count(choice => choice.LabelEn == "Other").ShouldBe(2);
	}

	[Fact]
	public void GivenDependentQuestion_WhenSameWordingSavedTwiceUnderOneParentChoice_ThenRefused()
	{
		// Given
		var make = Make();
		var model = Model(make);
		var niviuk = make.Choice("niviuk")!.Id;

		// When / Then
		Should.Throw<DomainRuleViolationException>(() => model.ReplaceChoices(
			[
				.. Kept(model),
				new QuestionOptionInput("other", "Other", "Autre", ParentChoiceId: niviuk),
				new QuestionOptionInput("other_2", "other", "autre", ParentChoiceId: niviuk),
			], At)).Message.ShouldContain("same parent choice");
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
		model.ReplaceChoices(Kept(model, ("mentor_7", elsewhere.Choice("gin")!.Id)), At);

		// When / Then
		Should.Throw<DomainRuleViolationException>(() => ChoiceDependencies.EnsureLinksAllowed([make, elsewhere, model], model))
			.Message.ShouldContain("'Mentor 7'");
		Should.NotThrow(() => ChoiceDependencies.EnsureLinksAllowed([make, elsewhere, model], make));
	}

	[Fact]
	public void GivenLinkedParentChoice_WhenRemoved_ThenRefusedNamingDependent()
	{
		// Given
		var make = Make();
		var model = Model(make);

		// When / Then
		Should.Throw<DomainRuleViolationException>(() =>
				ChoiceDependencies.EnsureParentChoicesRemovable([make, model], make, ["ozone"]))
			.Message.ShouldContain("'Model'");
		Should.Throw<DomainRuleViolationException>(() =>
			ChoiceDependencies.EnsureValueRemovable([make, model], make, make.Choice("niviuk")!.Id));
		Should.NotThrow(() => ChoiceDependencies.EnsureParentChoicesRemovable([make, model], make, ["niviuk", "ozone"]));
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
		ChoiceDependencies.Follow([make, model], make);

		// Then
		model.Choice("mentor_7")!.ParentChoiceId.ShouldBe(make.Choice("niviuk_gliders")!.Id);
		model.Choice("rush_6")!.ParentChoiceId.ShouldBe(make.Choice("ozone")!.Id);
	}

	[Fact]
	public void GivenMergedParentValue_WhenFollowed_ThenLinkNamesMergeTarget()
	{
		// Given
		var make = Make(QuestionType.Autocomplete);
		var model = Model(make);
		make.MergeValue(make.Choice("niviuk")!.Id, make.Choice("ozone")!.Id, Reviewer, At);

		// When
		ChoiceDependencies.Follow([make, model], make);

		// Then
		model.Choice("mentor_7")!.ParentChoiceId.ShouldBe(make.Choice("ozone")!.Id);
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
		ChoiceDependencies.Follow([make, model, replacement], replacement);

		// Then
		replacement.ShouldNotBeSameAs(make);
		model.ChoicesDependOnQuestionId.ShouldBe(replacement.Id);
		model.Choice("mentor_7")!.ParentChoiceId.ShouldBe(replacement.Choice("niviuk")!.Id);
		model.CurrentRevision.Id.ShouldBe(revision);
	}

	[Fact]
	public void GivenForkedChild_WhenApplied_ThenCopyKeepsDependencyAndLinks()
	{
		// Given
		var make = Make();
		var model = Model(make);

		// When
		var replacement = model.ApplyEdit(true, QuestionType.Autocomplete, "Wing model", "Modèle", true, true, 1, At);

		// Then
		replacement.ChoicesDependOnQuestionId.ShouldBe(make.Id);
		replacement.Choice("mentor_7")!.ParentChoiceId.ShouldBe(make.Choice("niviuk")!.Id);
	}

	[Fact]
	public void GivenDependentTypeAhead_WhenReporterTypesWordingUnderAnotherParentChoice_ThenNewValueUnderTheirs()
	{
		// Given
		var make = Make();
		var model = Model(make);
		var ozone = make.Choice("ozone")!.Id;

		// When
		var typed = model.AddChoiceFromReporter("mentor 7", Locale.EnCa, At, ozone);

		// Then — Mentor 7 is a Niviuk model; under Ozone it is a new value
		typed.Id.ShouldNotBe(model.Choice("mentor_7")!.Id);
		typed.ParentChoiceId.ShouldBe(ozone);
		typed.Code.ShouldBe("mentor_7_2");
		model.AddChoiceFromReporter("RUSH 6", Locale.EnCa, At, ozone).ShouldBe(model.Choice("rush_6"));
	}

	[Fact]
	public void GivenDependentTypeAhead_WhenMergingValuesUnderDifferentParentChoices_ThenRefused()
	{
		// Given
		var make = Make();
		var model = Model(make);

		// When / Then
		Should.Throw<DomainRuleViolationException>(() =>
			model.MergeValue(model.Choice("mentor_7")!.Id, model.Choice("rush_6")!.Id, Reviewer, At));
	}

	[Fact]
	public void GivenDependentTypeAhead_WhenReviewerRelinksValue_ThenLinkChangesAndValueIsReviewed()
	{
		// Given
		var make = Make();
		var model = Model(make);
		var value = model.Choice("rush_6")!;

		// When
		model.RelinkValue(value.Id, make.Choice("niviuk")!.Id, Reviewer, At);

		// Then
		value.ParentChoiceId.ShouldBe(make.Choice("niviuk")!.Id);
		value.ReviewedBy.ShouldBe(Reviewer);
	}

	[Fact]
	public void GivenValueWordedLikeOneUnderTarget_WhenRelinked_ThenRefused()
	{
		// Given
		var make = Make();
		var model = Model(make);
		var niviuk = make.Choice("niviuk")!.Id;
		var twin = model.AddChoiceFromReporter("Mentor 7", Locale.EnCa, At, make.Choice("ozone")!.Id);

		// When / Then
		Should.Throw<DomainRuleViolationException>(() => model.RelinkValue(twin.Id, niviuk, Reviewer, At))
			.Message.ShouldContain("Merge the two");
	}

	[Fact]
	public void GivenIndependentTypeAhead_WhenReviewerRelinksValue_ThenRefused()
	{
		// Given
		var make = Make(QuestionType.Autocomplete);

		// When / Then
		Should.Throw<DomainRuleViolationException>(() =>
			make.RelinkValue(make.Choice("niviuk")!.Id, make.Choice("ozone")!.Id, Reviewer, At));
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
		model.Choice("mentor_7")!.ParentChoiceId.ShouldBe(niviuk);
		model.Choice("zeno_2")!.ParentChoiceId.ShouldBeNull();
	}

	[Fact]
	public void GivenChoice_WhenLinkedToItself_ThenRefused()
	{
		// Given
		var make = Make();
		var model = Model(make);
		var mentor = model.Choice("mentor_7")!;

		// When / Then
		Should.Throw<DomainRuleViolationException>(() => model.RelinkValue(mentor.Id, mentor.Id, Reviewer, At));
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
		model.Choice("mentor_7_light")!.ParentChoiceId.ShouldBe(niviuk);
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
		model.ReplaceChoices(Kept(model, ("mentor_7", gin)), At);
		var replacement = make.ApplyEdit(true, QuestionType.SingleSelect, "Wing make", "Marque", true, true, 0, At);

		// When
		ChoiceDependencies.Follow([make, model, replacement], replacement);

		// Then
		model.Choice("mentor_7")!.ParentChoiceId.ShouldBe(gin);
		model.Choice("rush_6")!.ParentChoiceId.ShouldBe(replacement.Choice("ozone")!.Id);
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
		ChoiceDependencies.Follow([model, other], other);

		// Then
		model.ChoicesDependOnQuestionId.ShouldBe(make.Id);
		model.Choice("mentor_7")!.ParentChoiceId.ShouldBe(make.Choice("niviuk")!.Id);
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
		model.ReplaceChoices(Kept(model, ("mentor_7", gin)), At);

		// When
		ChoiceDependencies.Follow([make, model], make);

		// Then
		model.Choice("mentor_7")!.ParentChoiceId.ShouldBe(gin);
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
}
