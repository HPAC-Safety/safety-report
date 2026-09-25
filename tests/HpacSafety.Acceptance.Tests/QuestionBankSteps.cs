using HpacSafety.Core;
using HpacSafety.Core.Features.QuestionBank;
using Reqnroll;
using Shouldly;

namespace HpacSafety.Acceptance.Tests;

/// <summary>
///     The non-<c>@ui</c> scenarios in
///     <c>features/question-bank-and-form/question-bank-and-form.feature</c> that
///     describe authoring behaviour — required state, conditional questions,
///     and reordering.
/// </summary>
/// <remarks>
///     These run against the domain directly rather than through the API, because
///     the rules they describe live in <see cref="Question" /> and
///     <see cref="QuestionDependencies" />. The API's
///     own handling of them is covered by <c>HpacSafety.Api.Tests</c>. Every
///     question here is synthetic.
/// </remarks>
[Binding]
public sealed class QuestionBankSteps
{
#pragma warning disable CA1822 // Reqnroll step bindings must be instance methods to be discovered.

	private static readonly DateTimeOffset Noon = new(2026, 9, 20, 12, 0, 0, TimeSpan.Zero);

	private readonly List<Question> _questions = [];
	private Question? _question;
	private Dictionary<TinyId, int> _revisionNumbersBefore = [];
	private DomainRuleViolationException? _rejection;
	private bool _hasBeenAnswered;

	[Given(@"the question bank stores each question as a stable, non-localized key")]
	public void GivenQuestionsHaveStableKeys()
	{
		// Contextual. Asserted by every step below that reads Question.Key.
	}

	[Given(@"each revision has a monotonically increasing revision number for its key")]
	public void GivenRevisionNumbersIncrease()
	{
		// Contextual, as above.
	}

	[Given(@"at most one live question exists for a stable key")]
	public void GivenOneLiveQuestionPerKey()
	{
		// Contextual. Enforced by the partial unique index (ADR-0071) and
		// asserted by the fork scenarios.
	}

	// ------------------------------------------------------------ required --

	[Given(@"an Administrator authors an ordinary question")]
	public void GivenAnOrdinaryQuestion()
	{
		_question = Ordinary("occurrence_notes", QuestionType.LongText);
	}

	[When(@"they mark it as one reporters must answer")]
	public void WhenTheyMarkItRequired()
	{
		Revise(true);
	}

	[Then(@"the new revision records that it is required")]
	public void ThenTheRevisionIsRequired()
	{
		_question!.CurrentRevision.IsRequired.ShouldBeTrue();
		_question.CurrentRevision.RevisionNumber.ShouldBe(2);
	}

	[Then(@"marking it optional again records that on a further new revision")]
	public void ThenMarkingItOptionalAgain()
	{
		Revise(false);

		_question!.CurrentRevision.IsRequired.ShouldBeFalse();
		_question.CurrentRevision.RevisionNumber.ShouldBe(3);
	}

	// ------------------------------------------------- conditional questions --

	[Given(@"an active question asks for something other than yes\/no or single-select")]
	public void GivenANonBooleanQuestion()
	{
		_questions.Add(Ordinary("wing_make", QuestionType.ShortText));
	}

	[Given(@"a question is already conditional on a yes\/no question")]
	public void GivenAConditionalQuestion()
	{
		var parent = Ordinary("were_you_injured", QuestionType.YesNo);
		_questions.Add(parent);

		var child = Question.Create(
			"injury_detail", QuestionType.LongText, "What was the injury?", "Quelle était la blessure ?",
			Noon, isActive: true, dependsOnQuestionId: parent.Id);

		_questions.Add(child);
		_question = child;
	}

	[Given(@"the consent_publish question exists")]
	public void GivenConsentExists()
	{
		_question = Question.CreateConsentPublish(
			"May we publish a summary of this report?",
			"Pouvons-nous publier un résumé de ce rapport ?",
			Noon);

		_questions.Add(_question);
		_questions.Add(Ordinary("were_you_injured", QuestionType.YesNo));
	}

	[Given(@"the consent_media question exists")]
	public void GivenMediaConsentExists()
	{
		_question = Question.CreateConsentMedia(
			"May we show your photos and videos?",
			"Pouvons-nous montrer vos photos et vidéos ?",
			Noon);

		_questions.Add(_question);
		_questions.Add(Ordinary("were_you_injured", QuestionType.YesNo));
	}

	[Then(@"trying to make it conditional on another question is rejected the same way")]
	public void ThenMakingItConditionalIsRejected()
	{
		var other = _questions.Find(question => question.Key == "were_you_injured")!;

		Should.Throw<DomainRuleViolationException>(() => _question!.DependOn(other.Id, null, Noon.AddHours(1)));
	}

	[Then(@"trying to give it another role is rejected the same way")]
	public void ThenGivingItAnotherRoleIsRejected()
	{
		Should.Throw<DomainRuleViolationException>(() => _question!.AssignRole(QuestionRole.None));
		Should.Throw<DomainRuleViolationException>(() => _question!.AssignRole(QuestionRole.ConsentPublish));
		_question!.Role.ShouldBe(QuestionRole.ConsentMedia);
	}

	[Then(@"an Administrator may still change its wording in both languages")]
	public void ThenItsWordingCanChange()
	{
		var revision = _question!.Revise(
			_question.CurrentRevision.Type,
			"May HPAC show the photos and videos you attached?",
			"L'ACVL peut-elle montrer les photos et vidéos jointes ?",
			_question.CurrentRevision.IsPrivate,
			true,
			_question.CurrentRevision.DisplayOrder,
			Noon.AddHours(2));

		revision.LabelEn.ShouldBe("May HPAC show the photos and videos you attached?");
		revision.LabelFr.ShouldBe("L'ACVL peut-elle montrer les photos et vidéos jointes ?");
		revision.IsRequired.ShouldBeTrue();
		_question.Deleted.ShouldBeNull();
	}

	[When(@"an Administrator tries to make another question conditional on it")]
	public void WhenAnotherQuestionDependsOnIt()
	{
		_rejection = Record(() =>
			QuestionDependencies.EnsureDependencyAllowed(_questions, null, _questions[0].Id));
	}

	[When(@"an Administrator tries to make that yes\/no question conditional on it")]
	public void WhenTheParentDependsOnItsChild()
	{
		_rejection = Record(() =>
			QuestionDependencies.EnsureDependencyAllowed(_questions, _questions[0].Id, _question!.Id));
	}

	[When(@"an Administrator tries to make it conditional on another question")]
	public void WhenConsentIsMadeConditional()
	{
		var other = _questions.Find(question => question.Key == "were_you_injured")!;

		_rejection = Record(() => _question!.DependOn(other.Id, null, Noon.AddHours(1)));
	}

	[Then(@"the attempt is rejected")]
	public void ThenTheAttemptIsRejected()
	{
		_rejection.ShouldBeOfType<DomainRuleViolationException>();
	}

	[Then(@"a yes\/no question is accepted as the condition instead")]
	public void ThenAYesNoQuestionIsAccepted()
	{
		var parent = Ordinary("were_you_injured", QuestionType.YesNo);
		_questions.Add(parent);

		Should.NotThrow(() => QuestionDependencies.EnsureDependencyAllowed(_questions, null, parent.Id));
	}

	[Then(@"a question offered as its own condition is rejected the same way")]
	public void ThenSelfDependencyIsRejected()
	{
		Should.Throw<DomainRuleViolationException>(() =>
			QuestionDependencies.EnsureDependencyAllowed(_questions, _question!.Id, _question.Id));
	}

	[Then(@"a single-select question naming one of its live options is accepted as the condition instead")]
	public void ThenASingleSelectQuestionIsAccepted()
	{
		var parent = PilotType();
		_questions.Add(parent);

		Should.NotThrow(() =>
			QuestionDependencies.EnsureDependencyAllowed(_questions, null, parent.Id, "hang_glider"));
	}

	[Given(@"a single-select question asking whether the pilot flies hang gliders or paragliders")]
	public void GivenAPilotTypeQuestion()
	{
		_questions.Add(PilotType());
	}

	[Given(@"a single-select question offering hang glider and paraglider")]
	public void GivenAPilotTypeQuestionForRejection()
	{
		GivenAPilotTypeQuestion();
	}

	[When(@"an Administrator makes a rating question depend on the ""(.*)"" option")]
	[When(@"an Administrator makes a different rating question depend on the ""(.*)"" option")]
	public void WhenARatingQuestionDependsOnTheOption(string optionLabel)
	{
		var parent = _questions.Single(question => question.Key == "pilot_type");
		var code = optionLabel == "hang glider" ? "hang_glider" : "paraglider";

		QuestionDependencies.EnsureDependencyAllowed(_questions, null, parent.Id, code);

		var child = Question.Create(
			$"rating_{code}", QuestionType.SingleSelect, $"Rating ({optionLabel})", $"Qualification ({optionLabel})",
			Noon, isActive: true, dependsOnQuestionId: parent.Id, dependsOnOptionCode: code,
			options: [new QuestionOptionInput("h1", "H1", "H1")]);

		_questions.Add(child);
	}

	[Then(@"each rating question's saved dependency names its own required option")]
	public void ThenEachRatingQuestionNamesItsOwnOption()
	{
		_questions.Single(question => question.Key == "rating_hang_glider")
			.DependsOnOptionCode.ShouldBe("hang_glider");
		_questions.Single(question => question.Key == "rating_paraglider")
			.DependsOnOptionCode.ShouldBe("paraglider");
	}

	[When(@"an Administrator tries to make another question depend on a choice the parent does not offer")]
	public void WhenDependingOnAnUnofferedOption()
	{
		var parent = _questions.Single(question => question.Key == "pilot_type");

		_rejection = Record(() =>
			QuestionDependencies.EnsureDependencyAllowed(_questions, null, parent.Id, "trike"));
	}

	[Given(@"a yes\/no question")]
	public void GivenAYesNoQuestion()
	{
		_questions.Add(Ordinary("were_you_injured", QuestionType.YesNo));
	}

	[When(@"an Administrator makes another question depend on it")]
	public void WhenAnotherQuestionDependsOnTheYesNoQuestion()
	{
		var parent = _questions.Single(question => question.Type == QuestionType.YesNo);

		QuestionDependencies.EnsureDependencyAllowed(_questions, null, parent.Id);

		_question = Question.Create(
			"injury_detail", QuestionType.LongText, "What was the injury?", "Quelle était la blessure ?",
			Noon, isActive: true, dependsOnQuestionId: parent.Id);
		_questions.Add(_question);
	}

	[Then(@"the dependency needs no required option, because the condition is always ""answered yes""")]
	public void ThenTheDependencyNeedsNoOption()
	{
		_question!.DependsOnOptionCode.ShouldBeNull();
	}

	// --------------------------------------------------- statement and group --

	[Then(@"it cannot be marked required or private")]
	public void ThenItCannotBeMarkedRequiredOrPrivate()
	{
		Should.Throw<DomainRuleViolationException>(() =>
			Question.Create("heading_required", _pendingType, "Heading", "Titre", Noon, isRequired: true));
		Should.Throw<DomainRuleViolationException>(() =>
			Question.Create("heading_private", _pendingType, "Heading", "Titre", Noon, isPrivate: true));
	}

	[Then(@"it cannot be made conditional on another question")]
	public void ThenItCannotBeMadeConditional()
	{
		var parent = Ordinary("were_you_injured", QuestionType.YesNo);

		Should.Throw<DomainRuleViolationException>(() =>
			Question.Create(
				"heading_conditional", _pendingType, "Heading", "Titre", Noon, isPrivate: false,
				dependsOnQuestionId: parent.Id));
	}

	[Then(@"it cannot be the condition for another question")]
	public void ThenItCannotBeTheCondition()
	{
		var heading = Question.Create("heading_parent", _pendingType, "Heading", "Titre", Noon, isPrivate: false);

		Should.Throw<DomainRuleViolationException>(() =>
			QuestionDependencies.EnsureDependencyAllowed([heading], null, heading.Id));
	}

	// ------------------------------------------------------------- grouping --

	[Given(@"a group question exists as a section heading")]
	public void GivenAGroupQuestionExistsAsASectionHeading()
	{
		_questions.Add(Group("aircraft"));
	}

	[When(@"an Administrator makes another question grouped under it")]
	public void WhenAnotherQuestionIsGroupedUnderIt()
	{
		var group = _questions.Single(question => question.Type == QuestionType.Group);

		_question = Question.Create(
			"manufacturer", QuestionType.ShortText, "Manufacturer", "Manufacturier", Noon,
			isActive: true, isPrivate: false, groupedUnderQuestionId: group.Id);
		_questions.Add(_question);
	}

	[Then(@"the question's saved revision names that group as its heading")]
	public void ThenTheRevisionNamesTheGroup()
	{
		var group = _questions.Single(question => question.Type == QuestionType.Group);
		_question!.GroupedUnderQuestionId.ShouldBe(group.Id);
	}

	[Given(@"a question that is not a group")]
	public void GivenAQuestionThatIsNotAGroup()
	{
		_questions.Add(Ordinary("manufacturer", QuestionType.ShortText));
	}

	[When(@"an Administrator tries to group another question under it")]
	public void WhenTryingToGroupAnotherQuestionUnderIt()
	{
		var notAGroup = _questions.Single(question => question.Key == "manufacturer");

		_rejection = Record(() => QuestionGrouping.EnsureGroupingAllowed(_questions, null, notAGroup.Id));
	}

	[Given(@"two group questions exist")]
	public void GivenTwoGroupQuestionsExist()
	{
		_questions.Add(Group("form"));
		_questions.Add(Group("aircraft"));
	}

	[When(@"an Administrator tries to group one under the other")]
	public void WhenTryingToGroupOneGroupUnderAnother()
	{
		var outer = _questions[0];
		var inner = _questions[1];

		_rejection = Record(() => QuestionGrouping.EnsureGroupingAllowed(_questions, inner.Id, outer.Id));
	}

	[Given(@"a group question exists$")]
	public void GivenAGroupQuestionExists()
	{
		_question = Group("aircraft");
		_questions.Add(_question);
	}

	[When(@"an Administrator tries to group it under itself")]
	public void WhenTryingToGroupItUnderItself()
	{
		_rejection = Record(() => _question!.GroupUnder(_question.Id, Noon.AddHours(1)));
	}

	[Given(@"a question is both conditional on a yes\/no question and grouped under a group question")]
	public void GivenAQuestionBothConditionalAndGrouped()
	{
		var parent = Ordinary("were_you_injured", QuestionType.YesNo);
		var group = Group("aircraft");
		_questions.Add(parent);
		_questions.Add(group);

		_question = Question.Create(
			"manufacturer", QuestionType.ShortText, "Manufacturer", "Manufacturier", Noon,
			isActive: true, isPrivate: false, dependsOnQuestionId: parent.Id, groupedUnderQuestionId: group.Id);
		_questions.Add(_question);
	}

	[When(@"an Administrator reads its saved revision")]
	public void WhenReadingItsSavedRevision()
	{
		// Contextual — the following Then steps read _question directly.
	}

	[Then(@"both facts are recorded independently")]
	public void ThenBothFactsAreRecordedIndependently()
	{
		var parent = _questions.Single(question => question.Type == QuestionType.YesNo);
		var group = _questions.Single(question => question.Type == QuestionType.Group);

		_question!.DependsOnQuestionId.ShouldBe(parent.Id);
		_question.GroupedUnderQuestionId.ShouldBe(group.Id);
	}

	[Then(@"clearing one leaves the other unchanged")]
	public void ThenClearingOneLeavesTheOtherUnchanged()
	{
		var group = _questions.Single(question => question.Type == QuestionType.Group);

		_question!.DependOn(null, null, Noon.AddHours(1));

		_question.DependsOnQuestionId.ShouldBeNull();
		_question.GroupedUnderQuestionId.ShouldBe(group.Id);
	}

	// ---------------------------------------------------------- reordering --

	[Given(@"several active questions sit in a known order")]
	public void GivenSeveralQuestionsInOrder()
	{
		_questions.Add(Ordinary("occurrence_date", QuestionType.Date, 0));
		_questions.Add(Ordinary("occurrence_time", QuestionType.Time, 1));
		_questions.Add(Ordinary("occurrence_notes", QuestionType.LongText, 2));

		_revisionNumbersBefore = _questions.ToDictionary(
			question => question.Id, question => question.CurrentRevision.RevisionNumber);
	}

	[When(@"an Administrator rearranges them")]
	public void WhenTheyAreRearranged()
	{
		// The last question moves to the front; the other two shift down one.
		// Only a question whose position actually changed is revised.
		var arranged = new List<Question> { _questions[2], _questions[0], _questions[1] };

		for (var position = 0; position < arranged.Count; position++)
		{
			if (arranged[position].DisplayOrder != position)
			{
				arranged[position].Reorder(position, Noon.AddHours(1));
			}
		}

		_questions.Clear();
		_questions.AddRange(arranged);
	}

	[Then(@"each question that moved has a new revision recording its new position")]
	public void ThenMovedQuestionsHaveNewRevisions()
	{
		for (var position = 0; position < _questions.Count; position++)
		{
			var question = _questions[position];
			question.DisplayOrder.ShouldBe(position);
			question.CurrentRevision.RevisionNumber.ShouldBe(_revisionNumbersBefore[question.Id] + 1);
		}
	}

	[Then(@"a question that did not move keeps its current revision")]
	public void ThenAnUnmovedQuestionKeepsItsRevision()
	{
		var settled = _questions[0];
		var before = settled.CurrentRevision.RevisionNumber;

		if (settled.DisplayOrder != 0)
		{
			settled.Reorder(0, Noon.AddHours(2));
		}

		settled.CurrentRevision.RevisionNumber.ShouldBe(before);
	}

	[Then(@"no two questions are left claiming the same position")]
	public void ThenPositionsAreUnique()
	{
		_questions.Select(question => question.DisplayOrder).Distinct().Count().ShouldBe(_questions.Count);
	}

	// --------------------------------------------------------------- types --

	[Given(@"an Administrator authors a (.*) question")]
	public void GivenAQuestionOfType(string type)
	{
		EnumCode.TryParse<QuestionType>(type, out var parsed).ShouldBeTrue();
		_pendingType = parsed;
	}

	[When(@"they supply bilingual choices with it")]
	public void WhenTheySupplyOptions()
	{
		_rejection = Record(() =>
			_question = Question.Create(
				"authored_question",
				_pendingType,
				"A question",
				"Une question",
				Noon,
				isActive: true,
				options:
				[
					new QuestionOptionInput("first", "First", "Premier"),
					new QuestionOptionInput("second", "Second", "Deuxième"),
				]));
	}

	[Then(@"the question stores those choices")]
	public void ThenTheQuestionStoresThoseChoices()
	{
		_rejection.ShouldBeNull();
		_question!.Choices.Count.ShouldBe(2);
	}

	[Then(@"the question is rejected")]
	public void ThenTheRevisionIsRejected()
	{
		_rejection.ShouldBeOfType<DomainRuleViolationException>();
	}

	// -------------------------------------------------- keys and retirement --

	[Given(@"an Administrator authors a question with a loosely typed key")]
	public void GivenALooselyTypedKey()
	{
		_question = Question.Create(
			"  Occurrence   Date! ", QuestionType.Date, "When?", "Quand ?", Noon, isActive: true);
	}

	[Then(@"the stored key is lowercase and underscore-separated")]
	public void ThenTheKeyIsNormalized()
	{
		_question!.Key.ShouldBe("occurrence_date");
	}

	[Then(@"a key that reduces to nothing at all is rejected")]
	public void ThenAnEmptyKeyIsRejected()
	{
		Should.Throw<DomainRuleViolationException>(() =>
			Question.Create("!!!", QuestionType.Date, "When?", "Quand ?", Noon));
	}

	[Given(@"an active question nobody has answered")]
	public void GivenAnActiveQuestion()
	{
		_question = Ordinary("occurrence_notes", QuestionType.LongText);
		_hasBeenAnswered = false;
	}

	[Given(@"a question revision has never been referenced by any answer, including answers on deleted reports")]
	public void GivenAnUnreferencedRevision()
	{
		_question = Ordinary("occurrence_notes", QuestionType.LongText);
		_hasBeenAnswered = false;
	}

	[Given(@"a question revision is referenced by at least one answer, including an answer on a deleted report")]
	public void GivenAReferencedRevision()
	{
		_question = Ordinary("occurrence_notes", QuestionType.LongText);
		_hasBeenAnswered = true;
	}

	[When(@"an Administrator deletes it")]
	public void WhenItIsDeleted()
	{
		_question!.Delete(_hasBeenAnswered, Noon.AddHours(1));
	}

	[When(@"an Administrator attempts to delete it")]
	public void WhenDeletionIsAttempted()
	{
		_rejection = Record(() => _question!.Delete(_hasBeenAnswered, Noon.AddHours(1)));
	}

	[Then(@"the deletion succeeds")]
	public void ThenDeletionSucceeds()
	{
		_question!.Deleted.ShouldBe(Noon.AddHours(1));
	}

	[Then(@"the deletion is rejected")]
	public void ThenDeletionIsRejected()
	{
		_rejection.ShouldNotBeNull();
	}

	[Then(@"the revision remains available as history indefinitely")]
	public void ThenTheRevisionRemains()
	{
		_question!.Deleted.ShouldBeNull();
		_question.Revisions.ShouldNotBeEmpty();
	}

	[Then(@"deactivating it through a new revision is the normal way to remove it from future forms")]
	public void ThenDeactivationIsTheWayOut()
	{
		var revisions = _question!.Revisions.Count;

		_question.Deactivate(Noon.AddHours(2));

		_question.IsActive.ShouldBeFalse();
		_question.Revisions.Count.ShouldBe(revisions + 1);
		_question.Deleted.ShouldBeNull();
	}

	[Then(@"the question is stamped as deleted rather than removed")]
	public void ThenItIsStampedDeleted()
	{
		_question!.Deleted.ShouldBe(Noon.AddHours(1));
		_question.IsActive.ShouldBeFalse();
		_question.Revisions.ShouldNotBeEmpty();
	}

	[Then(@"it refuses any further revision")]
	public void ThenItRefusesFurtherRevision()
	{
		Should.Throw<DomainRuleViolationException>(() => _question!.Reorder(5, Noon.AddHours(2)));
	}

	[When(@"an Administrator tries to delete it")]
	public void WhenConsentIsDeleted()
	{
		_rejection = Record(() => _question!.Delete(false, Noon.AddHours(1)));
	}

	[Then(@"trying to stop asking it is rejected the same way")]
	public void ThenDeactivatingConsentIsRejected()
	{
		Should.Throw<DomainRuleViolationException>(() => _question!.Deactivate(Noon.AddHours(1)));
	}

	[Then(@"an ordinary edit that clears its active flag is rejected the same way")]
	public void ThenAnOrdinaryEditCannotDeactivateConsent()
	{
		// The dedicated method refused this all along; the administrator's
		// ordinary edit reached the same state and did not.
		Should.Throw<DomainRuleViolationException>(
			() => _question!.Revise(
				_question.CurrentRevision.Type,
				_question.CurrentRevision.LabelEn,
				_question.CurrentRevision.LabelFr,
				_question.CurrentRevision.IsPrivate,
				false,
				_question.CurrentRevision.DisplayOrder,
				Noon.AddHours(1)));
	}

	// ------------------------------------------------------ no way back --

	[Given(@"a question has been stamped as deleted")]
	public void GivenARetiredQuestion()
	{
		_question = Ordinary("occurrence_notes", QuestionType.LongText);
		_question.Delete(false, Noon.AddHours(1));
	}

	[When(@"anything attempts to restore, revive, or revise it")]
	public void WhenAnythingTriesToBringItBack()
	{
		var question = _question!;
		var current = question.CurrentRevision;
		var at = Noon.AddHours(2);

		Action[] attempts =
		[
			() => question.Activate(at),
			() => question.Revise(current.Type, "Reworded", "Reformulée", current.IsPrivate, true, current.DisplayOrder, at),
			() => question.ApplyEdit(false, current.Type, "Reworded", "Reformulée", current.IsPrivate, true, current.DisplayOrder, at),
			() => question.ApplyEdit(true, current.Type, "Reworded", "Reformulée", current.IsPrivate, true, current.DisplayOrder, at),
			() => question.Reorder(4, at),
			() => question.DependOn(null, null, at),
			() => question.GroupUnder(null, at),
			() => question.AssignRole(QuestionRole.None),
		];

		var rejections = attempts.Select(Record).ToList();

		// One attempt that got through is enough to make the scenario false.
		_rejection = rejections.TrueForAll(rejection => rejection is not null) ? rejections[0] : null;
	}

	[Then(@"an Administrator who wants it back authors it again as a new question")]
	public void ThenItIsAuthoredAgain()
	{
		// There is no restore, undelete, or revive to call (ADR-0071).
		typeof(Question).GetMethods()
			.Select(method => method.Name)
			.ShouldNotContain(name => name.Contains("Restore", StringComparison.Ordinal)
									  || name.Contains("Undelete", StringComparison.Ordinal)
									  || name.Contains("Revive", StringComparison.Ordinal));

		var again = Ordinary(_question!.Key, QuestionType.LongText);

		again.Id.ShouldNotBe(_question.Id);
		again.IsActive.ShouldBeTrue();
		_question.Deleted.ShouldNotBeNull();
		_question.Revisions.Count.ShouldBe(1);
	}

	// ------------------------------------------------------------- helpers --

	private QuestionType _pendingType = QuestionType.ShortText;

	private static Question Ordinary(string key,
									 QuestionType type,
									 int displayOrder = 0)
	{
		return Question.Create(key, type, $"Question {key}", $"Question {key} (fr)", Noon, isActive: true, displayOrder: displayOrder);
	}

	private static Question Group(string key)
	{
		return Question.Create(
			key, QuestionType.Group, $"Question {key}", $"Question {key} (fr)", Noon, isActive: true, isPrivate: false);
	}

	private static Question PilotType()
	{
		return Question.Create(
			"pilot_type", QuestionType.SingleSelect, "Are you a hang gliding pilot or a paragliding pilot?",
			"Êtes-vous un pilote de deltaplane ou de parapente ?", Noon, isActive: true,
			options:
			[
				new QuestionOptionInput("hang_glider", "Hang glider", "Deltaplane"),
				new QuestionOptionInput("paraglider", "Paraglider", "Parapente"),
			]);
	}

	private void Revise(bool isRequired)
	{
		var current = _question!.CurrentRevision;

		_question.Revise(
			current.Type, current.LabelEn, current.LabelFr, current.IsPrivate, current.IsActive,
			current.DisplayOrder, Noon.AddHours(1), isRequired: isRequired);
	}

	private static DomainRuleViolationException? Record(Action act)
	{
		try
		{
			act();
			return null;
		}
		catch (DomainRuleViolationException cause)
		{
			return cause;
		}
	}
}
