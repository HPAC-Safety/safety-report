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
	private QuestionChoice? _named;

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
		_answer = new Report(Locale.EnCa, Noon).Answer(_question, true, Noon);
	}

	[Given(@"^an? (single_select|multi_select|autocomplete|single-select) question has been answered with one of its choices$")]
	public void GivenAQuestionAnsweredWithAChoice(string type)
	{
		ArgumentNullException.ThrowIfNull(type);
		GivenAnAnsweredQuestion(type.Replace('-', '_'));
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
			dependsOnQuestionId: _question.Id, dependsOnChoiceId: _question.Choice(code)!.Id);
	}

	// --- REQ-QB-123..125: a picker option is fixed in place or replaced (ADR-0128) ---

	[Given(@"a single-select question has been answered with its option ""(.*)""")]
	public void GivenASingleSelectAnsweredWithItsOption(string label)
	{
		_question = Picker(label, "Mara");
		_answer = new Report(Locale.EnCa, Noon).Answer(_question, label, Noon);
		_revisionId = _question.CurrentRevision.Id;
		_named = _question.Choices[0];
	}

	[Given(@"a single-select question offering ""(.*)"", ""(.*)"", and ""(.*)"" has been answered with ""(.*)""")]
	public void GivenAPickerOfThreeAnswered(string first,
											string second,
											string third,
											string answered)
	{
		_question = Picker(first, second, third);
		_answer = new Report(Locale.EnCa, Noon).Answer(_question, answered, Noon);
		_revisionId = _question.CurrentRevision.Id;
		_named = _question.OfferedChoiceLabelled(answered, Locale.EnCa);
	}

	[When(@"an Administrator fixes that option's wording in place to ""(.*)""")]
	public void WhenAnAdministratorFixesTheOption(string wording)
	{
		Save([.. _question.Choices.Select(choice => choice == _named
			? new QuestionOptionInput(choice.Code, wording, $"{wording} (fr)")
			: new QuestionOptionInput(choice.Code, choice.LabelEn, choice.LabelFr))]);
	}

	[When(@"an Administrator replaces ""(.*)"" with ""(.*)""")]
	public void WhenAnAdministratorReplacesTheOption(string old,
													 string wording)
	{
		var code = QuestionKey.Normalize(old);
		_named ??= _question.Choice(code);

		Save([.. _question.Choices.Select(choice => choice.Code == code
			? new QuestionOptionInput(choice.Code, wording, $"{wording} (fr)", Replace: true)
			: new QuestionOptionInput(choice.Code, choice.LabelEn, choice.LabelFr))]);
	}

	[Then(@"the option keeps its identifier")]
	public void ThenTheOptionKeepsItsIdentifier()
	{
		_question.Choices.ShouldContain(choice => choice.Id == _named!.Id);
	}

	[Then(@"the earlier answer now reads ""(.*)""")]
	public void ThenTheEarlierAnswerNowReads(string wording)
	{
		_answer!.ChoiceId.ShouldBe(_named!.Id);
		_answer.Text.ShouldBe(wording);
	}

	[Then(@"the form offers ""(.*)"", ""(.*)"", and ""(.*)""")]
	public void ThenTheFormOffers(string first,
								  string second,
								  string third)
	{
		_question.Choices.Select(choice => choice.LabelEn).ShouldBe([first, second, third]);
	}

	[Then(@"""(.*)"" is retired, not erased, and records that ""(.*)"" replaced it")]
	public void ThenTheOptionIsRetiredAndLinked(string old,
												string replacement)
	{
		var retired = _question.AllChoices.Single(choice => choice.Id == _named!.Id);
		retired.LabelEn.ShouldBe(old);
		retired.Deleted.ShouldNotBeNull();
		retired.ReplacedByChoiceId.ShouldBe(_question.OfferedChoiceLabelled(replacement, Locale.EnCa)!.Id);
	}

	[Then(@"""(.*)"" has an identifier of its own")]
	public void ThenTheReplacementHasItsOwnIdentifier(string replacement)
	{
		_question.OfferedChoiceLabelled(replacement, Locale.EnCa)!.Id.ShouldNotBe(_named!.Id);
	}

	[Then(@"the earlier answer still names ""(.*)"" and reads ""(.*)""")]
	public void ThenTheEarlierAnswerStillNamesTheOldOption(string old,
														   string reads)
	{
		_answer!.ChoiceId.ShouldBe(_named!.Id);
		_named.LabelEn.ShouldBe(old);
		_answer.Text.ShouldBe(reads);
	}

	[Then(@"the dependent question is enabled by an answer naming ""(.*)""")]
	public void ThenTheDependentFollowsTheReplacement(string replacement)
	{
		var current = _question.OfferedChoiceLabelled(replacement, Locale.EnCa)!;

		_dependent!.CurrentRevision.IsEnabledGiven(_question, current.Id).ShouldBeTrue();
		_dependent.DependsOnChoiceId.ShouldBe(_named!.Id);
	}

	[Then(@"the dependent question keeps its current revision")]
	public void ThenTheDependentKeepsItsRevision()
	{
		_dependent!.Revisions.Count.ShouldBe(1);
	}

	[When(@"a reporter submits an answer naming a site the question does not offer")]
	public void WhenAReporterNamesANewSite()
	{
		_added = _question.AddChoiceFromReporter("Mount 7", Locale.EnCa);
	}

	[When(@"^a reporter answering in (English|French) submits ""(.*)"", which the question does not offer$")]
	public void WhenAReporterNamesANewSite(string language,
										  string typed)
	{
		var locale = LocaleOf(language);
		_answer = new Report(locale, Noon).Answer(_question, typed, Noon);
		_added = _question.AllChoices.Single(choice => choice.Id == _answer.ChoiceId);
	}

	// --- REQ-QB-128..130, REQ-QB-135: a reviewer's hand on type-ahead values (ADR-0129) ---

	private const string Reviewer = "synthetic-safety-officer";
	private readonly List<ReportAnswer> _answers = [];
	private TinyId _valueId;

	[Then(@"^the question gains a reporter-added value whose (English|French) wording is ""(.*)""$")]
	public void ThenTheQuestionGainsAValue(string language,
										   string typed)
	{
		_added!.AddedByReporter.ShouldBeTrue();
		_added.Label(LocaleOf(language)).ShouldBe(typed);
		(LocaleOf(language) == Locale.FrCa ? _added.LabelFr : _added.LabelEn).ShouldBe(typed);
		_question.Choices.ShouldContain(_added);
	}

	[Then(@"the value is flagged for review")]
	public void ThenTheValueIsFlagged()
	{
		_added!.NeedsReview.ShouldBeTrue();
		_added.CreatedAt.ShouldBe(Noon);
	}

	[Then(@"the reporter's answer names that value")]
	public void ThenTheAnswerNamesTheValue()
	{
		_answer!.ChoiceId.ShouldBe(_added!.Id);
	}

	[Then(@"^the next reporter is offered it, in (English|French) until its other language is supplied$")]
	public void ThenOfferedInItsLanguage(string language)
	{
		var typedIn = LocaleOf(language);
		var typed = _added!.Label(typedIn);

		_question.OfferedChoiceLabelled(typed, typedIn).ShouldBe(_added);
		_question.OfferedChoiceLabelled(typed, typedIn.Counterpart).ShouldBe(_added);
		_added.OtherLabel(typedIn).ShouldBeNull();
	}

	[Given(@"two reports answered a type-ahead question with the value ""(.*)""")]
	public void GivenTwoReportsAnsweredWith(string typed)
	{
		// No written values: the reporters' spelling is the value itself.
		_question = Question.Create(
			"where_did_this_happen", QuestionType.Autocomplete, "Where did this happen?", "Où cela s'est-il produit ?", Noon,
			isActive: true);
		_answers.Add(new Report(Locale.EnCa, Noon).Answer(_question, typed, Noon));
		_answers.Add(new Report(Locale.EnCa, Noon).Answer(_question, typed, Noon));
		_answers.Select(answer => answer.ChoiceId).Distinct().ShouldHaveSingleItem();
		_valueId = _answers[0].ChoiceId!.Value;
	}

	[When(@"a Safety Officer corrects that value's wording to ""(.*)""")]
	public void WhenASafetyOfficerCorrects(string corrected)
	{
		_question.CorrectValue(_valueId, corrected, null, Reviewer, Noon.AddHours(1));
	}

	[Then(@"the value keeps its identifier")]
	public void ThenTheValueKeepsItsIdentifier()
	{
		_question.AllChoices.ShouldContain(choice => choice.Id == _valueId);
		_answers.ShouldAllBe(answer => answer.ChoiceId == _valueId);
	}

	[Then(@"both answers now read ""(.*)""")]
	public void ThenBothAnswersRead(string corrected)
	{
		_answers.Select(answer => answer.Text).ShouldBe([corrected, corrected]);
	}

	[Then(@"the next reporter is offered ""(.*)""")]
	public void ThenTheNextReporterIsOffered(string corrected)
	{
		_question.OfferedChoiceLabelled(corrected, Locale.EnCa)!.Id.ShouldBe(_valueId);
	}

	[Given(@"a Safety Officer removed the type-ahead value ""(.*)""")]
	public void GivenASafetyOfficerRemoved(string typed)
	{
		_question = QuestionOfType(QuestionType.Autocomplete);
		_added = _question.AddChoiceFromReporter(typed, Locale.EnCa, Noon);
		_question.RemoveValue(_added.Id, Reviewer, Noon.AddHours(1));
		_added.NeedsReview.ShouldBeFalse();
	}

	[When(@"^a reporter (?:later )?submits ""(.*)"" for that question$")]
	public void WhenAReporterSubmitsForThatQuestion(string typed)
	{
		_answer = new Report(Locale.EnCa, Noon).Answer(_question, typed, Noon.AddHours(2));
	}

	[Then(@"the reporter's answer names the removed value")]
	public void ThenTheAnswerNamesTheRemovedValue()
	{
		_answer!.ChoiceId.ShouldBe(_added!.Id);
		_question.AllChoices.Count(choice => choice.AddedByReporter).ShouldBe(1);
	}

	[Then(@"the value stays removed and is not offered")]
	public void ThenTheValueStaysRemoved()
	{
		_added!.Deleted.ShouldNotBeNull();
		_question.Choices.ShouldNotContain(_added);
	}

	[Then(@"the value is flagged for review again")]
	public void ThenTheValueIsFlaggedAgain()
	{
		_added!.NeedsReview.ShouldBeTrue();
		_question.ReporterChoicesAwaitingReview.ShouldBe(1);
	}

	[Given(@"a type-ahead question has a reporter-added value flagged for review")]
	public void GivenAFlaggedValue()
	{
		_question = QuestionOfType(QuestionType.Autocomplete);
		_added = _question.AddChoiceFromReporter("Mount 7", Locale.EnCa, Noon);
		_added.NeedsReview.ShouldBeTrue();
	}

	[When(@"a Safety Officer approves it")]
	public void WhenASafetyOfficerApproves()
	{
		_question.ApproveValue(_added!.Id, Reviewer, Noon.AddHours(1));
	}

	[Then(@"the value is no longer flagged for review")]
	public void ThenNoLongerFlagged()
	{
		_added!.NeedsReview.ShouldBeFalse();
		_question.ReporterChoicesAwaitingReview.ShouldBe(0);
	}

	[Then(@"the review records the Safety Officer's token subject and the time")]
	public void ThenTheReviewIsRecorded()
	{
		_added!.ReviewedBy.ShouldBe(Reviewer);
		_added.ReviewedAt.ShouldBe(Noon.AddHours(1));
	}

	// --- REQ-QB-131..133: merging type-ahead values (ADR-0129) ---

	private readonly Dictionary<string, ReportAnswer> _answersByValue = [];
	private Exception? _attemptRefusal;

	private QuestionChoice ValueReading(string words)
	{
		return _question.AllChoices.Single(choice => choice.Label(Locale.EnCa) == words);
	}

	[Given(@"reports answered a type-ahead question with ""(.*)"" and with ""(.*)"", two separate values")]
	public void GivenTwoSeparateValues(string first,
									   string second)
	{
		_question = Question.Create(
			"where_did_this_happen", QuestionType.Autocomplete, "Where did this happen?", "Où cela s'est-il produit ?", Noon,
			isActive: true);
		_answersByValue[first] = new Report(Locale.EnCa, Noon).Answer(_question, first, Noon);
		_answersByValue[second] = new Report(Locale.EnCa, Noon).Answer(_question, second, Noon);
		_answersByValue[first].ChoiceId.ShouldNotBe(_answersByValue[second].ChoiceId);
	}

	[When(@"a Safety Officer merges ""(.*)"" into ""(.*)""")]
	public void WhenASafetyOfficerMerges(string source,
										 string target)
	{
		_question.MergeValue(ValueReading(source).Id, ValueReading(target).Id, Reviewer, Noon.AddHours(1));
	}

	[Then(@"""(.*)"" is removed and records that it was merged into ""(.*)""")]
	public void ThenTheSourceIsMerged(string source,
									  string target)
	{
		var merged = _question.AllChoices.Single(choice => choice.Id == _answersByValue[source].ChoiceId);
		merged.Deleted.ShouldNotBeNull();
		merged.MergedIntoChoiceId.ShouldBe(ValueReading(target).Id);
	}

	[Then(@"the answers that named ""(.*)"" still name it, and read ""(.*)""")]
	public void ThenTheAnswersStillNameIt(string source,
										  string target)
	{
		var answer = _answersByValue[source];
		answer.ChoiceId.ShouldBe(_question.AllChoices.Single(choice => choice.MergedIntoChoiceId is not null).Id);
		answer.Text.ShouldBe(target);
	}

	[Then(@"the form offers ""(.*)"" only")]
	public void ThenTheFormOffersOnly(string target)
	{
		_question.Choices.Select(choice => choice.Label(Locale.EnCa)).ShouldBe([target]);
	}

	[Then(@"the new answer names ""(.*)""")]
	public void ThenTheNewAnswerNames(string target)
	{
		_answer!.ChoiceId.ShouldBe(ValueReading(target).Id);
		_answer.Text.ShouldBe(target);
	}

	[Given(@"the type-ahead value ""(.*)"" was merged into ""(.*)""")]
	public void GivenAValueWasMerged(string source,
									 string target)
	{
		_question = Question.Create(
			"where_did_this_happen", QuestionType.Autocomplete, "Where did this happen?", "Où cela s'est-il produit ?", Noon,
			isActive: true);
		_answersByValue[source] = new Report(Locale.EnCa, Noon).Answer(_question, source, Noon);
		_answersByValue[target] = new Report(Locale.EnCa, Noon).Answer(_question, target, Noon);
		_question.AddChoiceFromReporter("C", Locale.EnCa, Noon);
		_question.MergeValue(ValueReading(source).Id, ValueReading(target).Id, Reviewer, Noon);
	}

	[Then(@"an answer naming ""(.*)"" reads ""(.*)""")]
	public void ThenAnAnswerNamingReads(string source,
										string target)
	{
		_answersByValue[source].Text.ShouldBe(target);
		_question.AllChoices.Single(choice => choice.Id == _answersByValue[source].ChoiceId).MergedIntoChoiceId
			.ShouldBe(ValueReading(target).Id);
	}

	[Then(@"merging ""(.*)"" into ""(.*)"" is refused")]
	public void ThenMergingIsRefused(string source,
									 string target)
	{
		Should.Throw<DomainRuleViolationException>(() =>
			_question.MergeValue(ValueReading(source).Id, ValueReading(target).Id, Reviewer, Noon.AddHours(2)));
	}

	[Given(@"^an? (autocomplete|single_select|multi_select) question has two choices$")]
	public void GivenAQuestionWithTwoChoices(string type)
	{
		EnumCode.TryParse(type, out QuestionType parsed).ShouldBeTrue();
		_question = QuestionOfType(parsed);
	}

	[When(@"^a Safety Officer tries to (merge it into the other|correct its wording) one of them$")]
	public void WhenASafetyOfficerTries(string action)
	{
		var (first, second) = (_question.Choices[0], _question.Choices[1]);
		_attemptRefusal = Record.Exception(() =>
		{
			if (action.StartsWith("merge", StringComparison.Ordinal))
			{
				_question.MergeValue(first.Id, second.Id, Reviewer, Noon);
			}
			else
			{
				_question.CorrectValue(first.Id, "Cooper's Hill", "Colline Cooper", Reviewer, Noon);
			}
		});
	}

	[Then(@"^the attempt is (accepted|refused)$")]
	public void ThenTheAttemptIs(string outcome)
	{
		if (outcome == "accepted")
		{
			_attemptRefusal.ShouldBeNull();
		}
		else
		{
			_attemptRefusal.ShouldBeOfType<DomainRuleViolationException>();
		}
	}

	private static Locale LocaleOf(string language)
	{
		return language == "French" ? Locale.FrCa : Locale.EnCa;
	}

	[When(@"another reporter submits the same site name")]
	public void WhenAnotherReporterNamesTheSameSite()
	{
		_added = _question.AddChoiceFromReporter("mount 7", Locale.EnCa);
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

	[When(@"an Administrator saves the single-select question without that choice")]
	public void WhenTheParentIsSavedWithoutTheChoice()
	{
		_refusal = Should.Throw<DomainRuleViolationException>(() =>
			QuestionDependencies.EnsureChoicesRemovable([_question, _dependent!], _question, ["hang_glider"]));
	}

	[Then(@"the question gains the site as a reporter-added choice")]
	public void ThenTheQuestionGainsTheSite()
	{
		_added!.AddedByReporter.ShouldBeTrue();
		_question.Choices.ShouldContain(_added);
	}

	[Then(@"the next reporter is offered it")]
	public void ThenTheNextReporterIsOfferedIt()
	{
		_question.OfferedChoiceLabelled("Mount 7", Locale.EnCa).ShouldNotBeNull();
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

	[Then(@"the report is accepted and the question gains the value")]
	public void ThenAcceptedAndGained()
	{
		_refusal.ShouldBeNull();
		_answer!.Text.ShouldBe("Mount 7");
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

	[When(@"that choice is removed")]
	public void WhenThatChoiceIsRemoved()
	{
		Save([Written[1]]);
	}

	[When(@"an Administrator changes the question's wording")]
	public void WhenAnAdministratorChangesTheQuestionsWording()
	{
		WhenAnAdministratorRewordsIt();
	}

	[Then(@"the earlier answer still names it and reads its wording")]
	public void ThenTheEarlierAnswerStillNamesIt()
	{
		var removed = _question.AllChoices.Single(choice => choice.Code == "coopers");
		_answer!.ChoiceId.ShouldBe(removed.Id);
		_answer.Text.ShouldBe("Cooper's");
	}

	[Then(@"the replacement question offers a copy of every choice, each with its own identifier")]
	public void ThenTheReplacementOffersACopyOfEveryChoice()
	{
		_live.ShouldNotBeSameAs(_question);
		_live.AllChoices.Select(choice => choice.Code).ShouldBe(_question.AllChoices.Select(choice => choice.Code), ignoreOrder: true);
		_live.AllChoices.Select(choice => choice.Id).Intersect(_question.AllChoices.Select(choice => choice.Id)).ShouldBeEmpty();
	}

	[Then(@"the earlier answer still names the retired question's choice")]
	public void ThenTheEarlierAnswerNamesTheRetiredQuestionsChoice()
	{
		_answer!.ChoiceId.ShouldBe(_question.AllChoices.Single(choice => choice.Code == "coopers").Id);
		_answer.Text.ShouldBe("Cooper's");
	}

	[Then(@"the form stops offering it")]
	public void ThenTheFormStopsOfferingIt()
	{
		_question.OfferedChoiceLabelled("Cooper's", Locale.EnCa).ShouldBeNull();
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
		_question.OfferedChoiceLabelled("Mount 7", Locale.FrCa).ShouldNotBeNull();
	}

	/// <summary>An Administrator's save of the whole question, choices included, as the editor sends it.</summary>
	private void Save(IReadOnlyList<QuestionOptionInput> options)
	{
		var current = _question.CurrentRevision;
		_live = _question.ApplyEdit(
			true, current.Type, current.LabelEn, current.LabelFr, current.IsPrivate, current.IsActive,
			current.DisplayOrder, Noon.AddHours(2), options: options);
	}

	private static Question Picker(params string[] labels)
	{
		return Question.Create(
			"wing_type", QuestionType.SingleSelect, "Wing type", "Type d'aile", Noon, isActive: true,
			options: [.. labels.Select(label => new QuestionOptionInput(QuestionKey.Normalize(label), label, $"{label} (fr)"))]);
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
