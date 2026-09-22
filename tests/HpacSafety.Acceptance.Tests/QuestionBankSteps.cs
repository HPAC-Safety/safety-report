using HpacSafety.Core;
using HpacSafety.Core.Features.QuestionBank;
using Reqnroll;
using Shouldly;

namespace HpacSafety.Acceptance.Tests;

/// <summary>
///     The non-<c>@ui</c> scenarios in
///     <c>features/question-bank-and-form/question-bank-and-form.feature</c> that
///     describe authoring behaviour — required state, shared choice lists,
///     conditional questions, and reordering.
/// </summary>
/// <remarks>
///     These run against the domain directly rather than through the API, because
///     the rules they describe live in <see cref="Question" />,
///     <see cref="OptionSet" />, and <see cref="QuestionDependencies" />. The API's
///     own handling of them is covered by <c>HpacSafety.Api.Tests</c>. Every
///     question here is synthetic.
/// </remarks>
[Binding]
public sealed class QuestionBankSteps
{
#pragma warning disable CA1822 // Reqnroll step bindings must be instance methods to be discovered.

    private static readonly DateTimeOffset Noon = new(2026, 9, 20, 12, 0, 0, TimeSpan.Zero);

    private readonly List<Question> _questions = [];
    private OptionSet? _optionSet;
    private Question? _question;
    private QuestionRevision? _snapshotted;
    private IReadOnlyList<QuestionRevisionOption> _optionsAtSnapshot = [];
    private Dictionary<TinyId, int> _revisionNumbersBefore = [];
    private DomainRuleViolationException? _rejection;

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

    // -------------------------------------------------------- choice lists --

    [Given(@"a shared choice list offers several bilingual options")]
    public void GivenASharedChoiceList()
    {
        _optionSet = OptionSet.Create("aerodromes", "Aerodromes", "Aérodromes", Noon);
        _optionSet.Add("golden", "Golden", "Golden");
        _optionSet.Add("lumby", "Lumby", "Lumby");
        _optionSet.Add("pemberton", "Pemberton", "Pemberton");
    }

    [Given(@"a question revision was built from a shared choice list")]
    public void GivenARevisionBuiltFromAList()
    {
        GivenASharedChoiceList();
        WhenARevisionUsesThatList();
    }

    [Given(@"a question revision copied an option from a shared choice list")]
    public void GivenARevisionCopiedAnOption()
    {
        GivenARevisionBuiltFromAList();
    }

    [When(@"an Administrator saves a question revision that uses that list")]
    public void WhenARevisionUsesThatList()
    {
        _question = Question.Create(
            "launch_site",
            QuestionType.Autocomplete,
            "Where did you launch from?",
            "D'où avez-vous décollé ?",
            Noon,
            isActive: true,
            optionSetId: _optionSet!.Id,
            options: _optionSet.AsRevisionOptions());

        _snapshotted = _question.CurrentRevision;
        _optionsAtSnapshot = [.. _snapshotted.Options.OrderBy(option => option.DisplayOrder)];
    }

    [Then(@"the revision holds its own complete copy of those options")]
    public void ThenTheRevisionHoldsItsOwnCopy()
    {
        _snapshotted!.Options.Count.ShouldBe(3);
        _snapshotted.Options.Select(option => option.Code)
            .ShouldBe(["golden", "lumby", "pemberton"], true);
    }

    [Then(@"each copy records the shared item it came from")]
    public void ThenEachCopyRecordsItsSource()
    {
        var itemIds = _optionSet!.Items.Select(item => item.Id).ToList();

        _snapshotted!.Options.ShouldAllBe(option => option.SourceItemId != null);
        _snapshotted.Options.Select(option => option.SourceItemId!.Value).ShouldBeSubsetOf(itemIds);
    }

    [When(@"an Administrator relabels an option, adds one, and removes another from that list")]
    public void WhenTheListIsEdited()
    {
        _optionSet!.Relabel("golden", "Golden (BC)", "Golden (C.-B.)");
        _optionSet.Add("cochrane", "Cochrane", "Cochrane");
        _optionSet.Remove("lumby", Noon.AddHours(1));
    }

    [When(@"an Administrator removes that option from the list")]
    public void WhenAnOptionIsRemoved()
    {
        _optionSet!.Remove("lumby", Noon.AddHours(1));
    }

    [Then(@"the existing revision still offers exactly the options it was saved with")]
    public void ThenTheExistingRevisionIsUnchanged()
    {
        _snapshotted!.Options.Count.ShouldBe(_optionsAtSnapshot.Count);
        _snapshotted.Option("golden")!.LabelEn.ShouldBe("Golden");
        _snapshotted.Option("lumby").ShouldNotBeNull();
        _snapshotted.Option("cochrane").ShouldBeNull();
    }

    [Then(@"a revision saved afterwards offers the edited list instead")]
    public void ThenALaterRevisionOffersTheEditedList()
    {
        var revision = _question!.Revise(
            QuestionType.Autocomplete,
            _snapshotted!.LabelEn,
            _snapshotted.LabelFr,
            _snapshotted.IsPrivate,
            true,
            _snapshotted.DisplayOrder,
            Noon.AddHours(2),
            optionSetId: _optionSet!.Id,
            options: _optionSet.AsRevisionOptions());

        revision.Option("golden")!.LabelEn.ShouldBe("Golden (BC)");
        revision.Option("cochrane").ShouldNotBeNull();
        revision.Option("lumby").ShouldBeNull();
    }

    [Then(@"the option is retired from the list rather than erased")]
    public void ThenTheOptionIsRetired()
    {
        _optionSet!.Items.ShouldNotContain(item => item.Code == "lumby");
        Should.Throw<DomainRuleViolationException>(() => _optionSet.Relabel("lumby", "Lumby", "Lumby"));
    }

    [Then(@"the revision's copy of it is unchanged")]
    public void ThenTheCopyIsUnchanged()
    {
        _snapshotted!.Option("lumby")!.LabelEn.ShouldBe("Lumby");
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

    [When(@"an Administrator tries to make another question depend on an option the parent does not offer")]
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
            if (arranged[position].DisplayOrder != position)
                arranged[position].Reorder(position, Noon.AddHours(1));

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

        if (settled.DisplayOrder != 0) settled.Reorder(0, Noon.AddHours(2));

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

    [When(@"they supply bilingual options with it")]
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
                    new QuestionOptionInput("second", "Second", "Deuxième")
                ]));
    }

    [Then(@"the revision stores those options")]
    public void ThenTheRevisionStoresThoseOptions()
    {
        _rejection.ShouldBeNull();
        _question!.CurrentRevision.Options.Count.ShouldBe(2);
    }

    [Then(@"the revision is rejected")]
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

    [Given(@"an active question has been asked")]
    public void GivenAnActiveQuestion()
    {
        _question = Ordinary("occurrence_notes", QuestionType.LongText);
    }

    [When(@"an Administrator deletes it")]
    public void WhenItIsDeleted()
    {
        _question!.Delete(Noon.AddHours(1));
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
        _rejection = Record(() => _question!.Delete(Noon.AddHours(1)));
    }

    [Then(@"trying to stop asking it is rejected the same way")]
    public void ThenDeactivatingConsentIsRejected()
    {
        Should.Throw<DomainRuleViolationException>(() => _question!.Deactivate(Noon.AddHours(1)));
    }

    // ------------------------------------------------- choice-list lifecycle --

    [When(@"an Administrator retires the whole list")]
    public void WhenTheListIsRetired()
    {
        _optionSet!.Delete(Noon.AddHours(1));
    }

    [Then(@"its options are retired with it")]
    public void ThenItsOptionsAreRetired()
    {
        _optionSet!.Deleted.ShouldBe(Noon.AddHours(1));
        _optionSet.Items.ShouldBeEmpty();
    }

    [Then(@"adding, renaming, or rearranging it is rejected")]
    public void ThenARetiredListRefusesEdits()
    {
        Should.Throw<DomainRuleViolationException>(() => _optionSet!.Add("cochrane", "Cochrane", "Cochrane"));
        Should.Throw<DomainRuleViolationException>(() => _optionSet!.Rename("Sites", "Sites"));
        Should.Throw<DomainRuleViolationException>(() => _optionSet!.Arrange(["golden"]));
    }

    [When(@"an Administrator arranges every option into a new order")]
    public void WhenTheListIsArranged()
    {
        _optionSet!.Arrange(["pemberton", "golden", "lumby"]);
    }

    [Then(@"the list takes that order")]
    public void ThenTheListTakesThatOrder()
    {
        _optionSet!.Items.Select(item => item.Code).ShouldBe(["pemberton", "golden", "lumby"]);
    }

    [Then(@"an arrangement that omits or repeats an option is rejected")]
    public void ThenAPartialArrangementIsRejected()
    {
        Should.Throw<DomainRuleViolationException>(() => _optionSet!.Arrange(["golden"]));
        Should.Throw<DomainRuleViolationException>(() => _optionSet!.Arrange(["golden", "golden", "lumby"]));
    }

    // ------------------------------------------------------------- helpers --

    private QuestionType _pendingType = QuestionType.ShortText;

    private static Question Ordinary(string key, QuestionType type, int displayOrder = 0)
    {
        return Question.Create(key, type, $"Question {key}", $"Question {key} (fr)", Noon, isActive: true, displayOrder: displayOrder);
    }

    private static Question PilotType()
    {
        return Question.Create(
            "pilot_type", QuestionType.SingleSelect, "Are you a hang gliding pilot or a paragliding pilot?",
            "Êtes-vous un pilote de deltaplane ou de parapente ?", Noon, isActive: true,
            options:
            [
                new QuestionOptionInput("hang_glider", "Hang glider", "Deltaplane"),
                new QuestionOptionInput("paraglider", "Paraglider", "Parapente")
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
