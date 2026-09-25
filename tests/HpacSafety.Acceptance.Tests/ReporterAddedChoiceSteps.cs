using HpacSafety.Core;
using HpacSafety.Core.Features.QuestionBank;
using HpacSafety.Core.Features.Reporting;
using Reqnroll;
using Shouldly;

namespace HpacSafety.Acceptance.Tests;

/// <summary>
///     The non-<c>@ui</c> scenarios for a question's own choices: a reporter
///     adding one to a type-ahead, an Administrator editing them in place, and a
///     fork carrying them across — ADR-0063, ADR-0095. Its wording edit also
///     serves REQ-QB-002 and REQ-QB-006, judged by <see cref="QuestionForkSteps" />.
/// </summary>
/// <remarks>
///     These run against the domain; <c>REQ-QB-095</c> proves the same rule over
///     the submission endpoint. Every site name here is synthetic.
/// </remarks>
[Binding]
public sealed class ReporterAddedChoiceSteps(QuestionEditOutcome outcome)
{
#pragma warning disable CA1822 // Reqnroll step bindings must be instance methods to be discovered.

	private static readonly DateTimeOffset Noon = new(2026, 9, 21, 12, 0, 0, TimeSpan.Zero);

	private static readonly QuestionOptionInput[] Written =
	[
		new("coopers", "Cooper's", "Cooper's"),
		new("woodside", "Woodside", "Woodside"),
	];

	private Question _question = null!;
	private Question _live = null!;
	private TinyId _revisionId;
	private QuestionChoice? _added;
	private ReportAnswer? _answer;
	private Exception? _refusal;
	private IReadOnlyList<QuestionOptionInput> _edited = [];
	private Question? _dependent;

	[Given(@"a type-ahead question offers several choices")]
	public void GivenATypeAheadOffersChoices()
	{
		_question = QuestionOfType(QuestionType.Autocomplete);
	}

	[Given(@"a published (.*) question offers several choices")]
	public void GivenAPublishedQuestionOfType(string type)
	{
		EnumCode.TryParse(type, out QuestionType parsed).ShouldBeTrue();
		_question = QuestionOfType(parsed);
	}

	[Given(@"a reporter has already added a site to a type-ahead question")]
	public void GivenAReporterAlreadyAddedASite()
	{
		GivenATypeAheadOffersChoices();
		_added = _question.AddChoiceFromReporter("Mount 7", Locale.EnCa);
	}

	[Given(@"an Administrator removed a reporter-added choice from a type-ahead question")]
	public void GivenAnAdministratorRemovedAReporterChoice()
	{
		GivenAReporterAlreadyAddedASite();
		_question.ReplaceChoices(Written, Noon.AddHours(1));
	}

	[Given(@"a type-ahead question has a reporter-added choice typed only in English")]
	public void GivenAnEnglishOnlyReporterChoice()
	{
		GivenAReporterAlreadyAddedASite();
	}

	[Given(@"a (.*) question has been answered on at least one report")]
	public void GivenAnAnsweredQuestion(string type)
	{
		var parsed = QuestionType.Autocomplete;
		(type == "type-ahead" || EnumCode.TryParse(type, out parsed)).ShouldBeTrue();
		_question = QuestionOfType(parsed);
		_answer = new Report(Locale.EnCa, Noon).Answer(_question, "Cooper's", Noon);
		_revisionId = _question.CurrentRevision.Id;

		if (parsed == QuestionType.Autocomplete)
		{
			_added = _question.AddChoiceFromReporter("Mount 7", Locale.EnCa);
		}
	}

	[Given(@"a question has been answered on at least one report")]
	public void GivenAnAnsweredOrdinaryQuestion()
	{
		_question = Question.Create(
			"occurrence_site", QuestionType.ShortText, "Which site were you flying?", "Où voliez-vous ?", Noon, isActive: true);
		_answer = new Report(Locale.EnCa, Noon).Answer(_question, "Cooper's", Noon);
	}

	[Given(@"the consent_publish question has been answered on at least one report")]
	public void GivenAnAnsweredConsentQuestion()
	{
		_question = Question.CreateConsentPublish(
			"May we publish a summary of this report?", "Pouvons-nous publier un résumé de ce rapport ?", Noon);
		_answer = new Report(Locale.EnCa, Noon).Answer(_question, "yes", Noon);
	}

	[Given(@"a question has been answered with one of its choices")]
	public void GivenAQuestionAnsweredWithAChoice()
	{
		GivenAnAnsweredQuestion("single_select");
	}

	[Given(@"it offers choices an Administrator wrote and a reporter-added choice")]
	public void GivenWrittenAndReporterChoices()
	{
		_question.Choices.Select(choice => choice.AddedByReporter).ShouldBe([false, false, true]);
	}

	[Given(@"an Administrator removed one of its choices")]
	public void GivenAnAdministratorRemovedOne()
	{
		Save([Written[0], new("mount_7", "Mount 7", null)]);
	}

	[Given(@"a question depends on the ""(.*)"" choice of a single-select question")]
	public void GivenAQuestionDependsOnAChoice(string code)
	{
		_question = Question.Create(
			"aircraft", QuestionType.SingleSelect, "Aircraft", "Aéronef", Noon, isActive: true,
			options: [new("hang_glider", "Hang glider", "Deltaplane"), new("paraglider", "Paraglider", "Parapente")]);
		_dependent = Question.Create(
			"wing_rating", QuestionType.ShortText, "Wing rating", "Homologation", Noon, isActive: true,
			dependsOnQuestionId: _question.Id, dependsOnOptionCode: code);
	}

	[When(@"a reporter submits an answer naming a site the question does not offer")]
	public void WhenAReporterNamesANewSite()
	{
		_added = _question.AddChoiceFromReporter("Mount 7", Locale.EnCa);
	}

	[When(@"a reporter answering in French submits ""(.*)"", which the question does not offer")]
	public void WhenAFrenchReporterNamesANewSite(string typed)
	{
		_added = _question.AddChoiceFromReporter(typed, Locale.FrCa);
	}

	[When(@"another reporter submits the same site name")]
	public void WhenAnotherReporterNamesTheSameSite()
	{
		_added = _question.AddChoiceFromReporter("mount 7", Locale.EnCa);
	}

	[When(@"a reporter submits that same value again")]
	public void WhenAReporterRetypesARemovedValue()
	{
		_added = _question.AddChoiceFromReporter("Mount 7", Locale.EnCa);
	}

	[When(@"a reporter submits a value the question does not offer")]
	public void WhenAReporterSubmitsAnUnofferedValue()
	{
		// What the submission endpoint does: validate the answer, then record a
		// type-ahead's new value on the question (ADR-0063, ADR-0095).
		try
		{
			_answer = new Report(Locale.EnCa, Noon).Answer(_question, "Mount 7", Noon);

			if (_question.TakesReporterAdditions)
			{
				_added = _question.AddChoiceFromReporter("Mount 7", Locale.EnCa);
			}
		}
		catch (DomainRuleViolationException cause)
		{
			_refusal = cause;
		}
	}

	[When(@"an Administrator changes its wording")]
	public void WhenAnAdministratorRewordsIt()
	{
		var current = _question.CurrentRevision;
		outcome.OriginalLabelEn = current.LabelEn;

		_live = _question.ApplyEdit(
			true, current.Type, "Where were you flying?", current.LabelFr, current.IsPrivate, current.IsActive,
			current.DisplayOrder, Noon.AddDays(1));

		// QuestionForkSteps judges the edit with the sentences REQ-QB-002 and
		// REQ-QB-006 share with the API-driven REQ-QB-003.
		outcome.Original = _question;
		outcome.Live = _live;
		outcome.Answer = _answer;
	}

	[When(@"^an Administrator (adds a choice to|rewords one of|reorders|supplies the missing language of one of) its choices$")]
	public void WhenAnAdministratorEditsItsChoices(string edit)
	{
		var reporterChoice = _added is null ? [] : new QuestionOptionInput[] { new("mount_7", "Mount 7", null) };

		_edited = edit switch
		{
			"adds a choice to" => [.. Written, new("mara", "Mara", "Mara")],
			"rewords one of" => [new("coopers", "Cooper's Hill", "Colline Cooper"), Written[1]],
			"reorders" => [.. reporterChoice, Written[1], Written[0]],
			_ => [.. Written, new("mount_7", "Mount 7", "Mont 7")],
		};

		Save(_edited);
	}

	[When(@"an Administrator removes that choice")]
	public void WhenAnAdministratorRemovesTheChoice()
	{
		Save([Written[1]]);
	}

	[When(@"an Administrator saves the single-select question without that choice")]
	public void WhenTheParentIsSavedWithoutTheChoice()
	{
		_refusal = Should.Throw<DomainRuleViolationException>(() =>
			QuestionDependencies.EnsureChoicesRemovable([_question, _dependent!], _question, ["hang_glider"]));
	}

	[When(@"a reporter using French opens the form")]
	public void WhenAFrenchReporterOpensTheForm()
	{
		// The form reads the question's live choices in the reporter's locale.
	}

	[When(@"an Administrator supplies the choice's French wording")]
	public void WhenAnAdministratorSuppliesTheFrench()
	{
		Save([.. Written, new("mount_7", "Mount 7", "Mont 7")]);
	}

	[Then(@"the question gains the site as a reporter-added choice")]
	public void ThenTheQuestionGainsTheSite()
	{
		_added!.AddedByReporter.ShouldBeTrue();
		_question.Choices.ShouldContain(_added);
	}

	[Then(@"it carries the language the reporter typed it in")]
	public void ThenItCarriesTheTypedLanguage()
	{
		_added!.LabelEn.ShouldBe("Mount 7");
		_added.ReporterLocale.ShouldBe(Locale.EnCa);
	}

	[Then(@"it is marked for an Administrator to supply the other language")]
	public void ThenItIsMarkedForTranslation()
	{
		// Nothing on the submission path translates (ADR-0072): the other
		// language is simply missing until an Administrator supplies it.
		_added!.LabelFr.ShouldBeNull();
		_added.NeedsTranslation.ShouldBeTrue();
		_question.ReporterChoicesAwaitingReview.ShouldBe(1);
	}

	[Then(@"the next reporter is offered it")]
	public void ThenTheNextReporterIsOfferedIt()
	{
		_question.Offers("Mount 7", Locale.EnCa).ShouldBeTrue();
	}

	[Then(@"the question gains a reporter-added choice whose French wording is ""(.*)""")]
	public void ThenTheQuestionGainsAFrenchChoice(string typed)
	{
		_added!.AddedByReporter.ShouldBeTrue();
		_added.LabelFr.ShouldBe(typed);
		_question.Choices.ShouldContain(_added);
	}

	[Then(@"the choice has no English wording until an Administrator supplies it")]
	public void ThenTheChoiceHasNoEnglish()
	{
		_added!.LabelEn.ShouldBeNull();
	}

	[Then(@"the choice records that it was typed in French")]
	public void ThenItRecordsFrench()
	{
		_added!.ReporterLocale.ShouldBe(Locale.FrCa);
		_added.NeedsTranslation.ShouldBeTrue();
	}

	[Then(@"its code is ""(.*)"", derived from the French wording")]
	public void ThenItsCodeIsDerivedFromTheFrench(string code)
	{
		_added!.Code.ShouldBe(code);
	}

	[Then(@"the existing choice is reused rather than duplicated")]
	public void ThenTheExistingChoiceIsReused()
	{
		_question.Choices.Count(choice => choice.Code == "mount_7").ShouldBe(1);
	}

	[Then(@"an administrator's wording is never replaced by a reporter's")]
	public void ThenTheWordingIsNotReplaced()
	{
		_added!.LabelEn.ShouldBe("Mount 7");
	}

	[Then(@"the choice stays removed from the question")]
	public void ThenItStaysRemoved()
	{
		_question.Choices.Select(choice => choice.Code).ShouldNotContain("mount_7");
		_added!.Deleted.ShouldNotBeNull();
	}

	[Then(@"the reporter's answer still records the value they typed")]
	public void ThenTheAnswerRecordsTheTypedValue()
	{
		// A type-ahead takes the reporter's words as given, offered or not.
		new Report(Locale.EnCa, Noon).Answer(_question, "Mount 7", Noon).Value.ShouldBe("Mount 7");
	}

	[Then(@"the report is accepted and the question gains the value")]
	public void ThenAcceptedAndGained()
	{
		_refusal.ShouldBeNull();
		_answer!.Value.ShouldBe("Mount 7");
		_question.Choice("mount_7")!.AddedByReporter.ShouldBeTrue();
	}

	[Then(@"the submission is rejected and the question is unchanged")]
	public void ThenRejectedAndUnchanged()
	{
		_refusal.ShouldNotBeNull();
		_question.Choices.Select(choice => choice.Code).ShouldBe(["coopers", "woodside"]);
	}

	[Then(@"the replacement question offers every choice the retired one offered")]
	public void ThenTheReplacementOffersEveryChoice()
	{
		_live.ShouldNotBeSameAs(_question);
		_live.Choices.Select(choice => choice.Code).ShouldBe(_question.Choices.Select(choice => choice.Code));
	}

	[Then(@"the reporter-added choice is still marked as reporter-added")]
	public void ThenTheMarkIsKept()
	{
		_live.Choice("mount_7")!.AddedByReporter.ShouldBeTrue();
	}

	[Then(@"the removed choice is carried over and stays removed")]
	public void ThenTheRemovedChoiceStaysRemoved()
	{
		_live.AllChoices.Single(choice => choice.Code == "woodside").Deleted.ShouldNotBeNull();
	}

	[Then(@"the question offers the edited choices")]
	public void ThenItOffersTheEditedChoices()
	{
		_question.Choices.Select(choice => choice.Code).ShouldBe(_edited.Select(option => option.Code));
		_question.Choices.Select(choice => choice.LabelEn)
			.ShouldBe(_edited.Select(option => option.LabelEn));
	}

	[Then(@"the question keeps its identifier and its current revision")]
	public void ThenTheQuestionIsKept()
	{
		_live.ShouldBeSameAs(_question);
		_question.Deleted.ShouldBeNull();
		_question.CurrentRevision.Id.ShouldBe(_revisionId);
	}

	[Then(@"the answers already given still record the reporter's own words")]
	[Then(@"the answer that named it still records the reporter's own words")]
	public void ThenTheAnswersKeepTheirWords()
	{
		_answer!.Value.ShouldBe("Cooper's");
	}

	[Then(@"the form stops offering it")]
	public void ThenTheFormStopsOfferingIt()
	{
		_question.Offers("Cooper's", Locale.EnCa).ShouldBeFalse();
	}

	[Then(@"the choice is retired rather than erased")]
	public void ThenTheChoiceIsRetired()
	{
		_question.AllChoices.Single(choice => choice.Code == "coopers").Deleted.ShouldNotBeNull();
	}

	[Then(@"the save is refused naming the dependent question")]
	public void ThenTheSaveIsRefused()
	{
		_refusal!.Message.ShouldContain("Wing rating");
	}

	[Then(@"the choice is still offered")]
	public void ThenTheChoiceIsStillOffered()
	{
		_question.Choice("paraglider").ShouldNotBeNull();
	}

	[Then(@"the question offers that choice in its English wording")]
	public void ThenOfferedInEnglishToAFrenchReporter()
	{
		_question.Choice("mount_7")!.Label(Locale.FrCa).ShouldBe("Mount 7");
		_question.Offers("Mount 7", Locale.FrCa).ShouldBeTrue();
	}

	[Then(@"a reporter using French is offered the French wording")]
	public void ThenOfferedInFrench()
	{
		_question.Choice("mount_7")!.Label(Locale.FrCa).ShouldBe("Mont 7");
	}

	[Then(@"the choice is no longer waiting to be reviewed")]
	public void ThenNoLongerAwaitingReview()
	{
		_question.Choice("mount_7")!.NeedsTranslation.ShouldBeFalse();
		_question.ReporterChoicesAwaitingReview.ShouldBe(0);
	}

	/// <summary>An Administrator's save of the whole question, choices included, as the editor sends it.</summary>
	private void Save(IReadOnlyList<QuestionOptionInput> options)
	{
		var current = _question.CurrentRevision;
		_live = _question.ApplyEdit(
			true, current.Type, current.LabelEn, current.LabelFr, current.IsPrivate, current.IsActive,
			current.DisplayOrder, Noon.AddHours(2), options: options);
	}

	private static Question QuestionOfType(QuestionType type)
	{
		return Question.Create(
			"where_did_this_happen",
			type,
			"Where did this happen?",
			"Où cela s'est-il produit ?",
			Noon,
			isActive: true,
			options: Written);
	}
}
