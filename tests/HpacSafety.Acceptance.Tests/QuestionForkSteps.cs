using HpacSafety.Core.Features.QuestionBank;
using Reqnroll;
using Shouldly;

namespace HpacSafety.Acceptance.Tests;

/// <summary>
///     The revise-or-fork scenarios in
///     <c>features/question-bank-and-form/question-bank-and-form.feature</c> —
///     REQ-QB-001, REQ-QB-002, REQ-QB-003, and REQ-QB-006 (ADR-0071).
/// </summary>
/// <remarks>
///     The edit itself is made elsewhere — against the domain by
///     <see cref="ReporterAddedChoiceSteps" />, which already binds "an Administrator
///     changes its wording", or through the booted API by
///     <see cref="QuestionForkEndpointSteps" /> for an answer on a deleted report.
///     This class judges what either left behind, through
///     <see cref="QuestionEditOutcome" />. Every question here is synthetic.
/// </remarks>
[Binding]
public sealed class QuestionForkSteps(QuestionEditOutcome outcome)
{
#pragma warning disable CA1822 // Reqnroll step bindings must be instance methods to be discovered.

	private static readonly DateTimeOffset Noon = new(2026, 9, 22, 12, 0, 0, TimeSpan.Zero);

	private readonly List<(QuestionRevision Before, RevisionFields Snapshot, QuestionRevision After, RevisionFields Asked)> _edits = [];

	// --- REQ-QB-001: an unanswered question revises ---

	[Given(@"an active question revision exists for a stable key")]
	public void GivenAnActiveQuestionRevision()
	{
		outcome.Original = Question.Create(
			"occurrence_site", QuestionType.ShortText, "Where were you flying?", "Où voliez-vous ?", Noon,
			helpTextEn: "The site name.", helpTextFr: "Le nom du site.", isPrivate: true, isActive: true);
	}

	[Given(@"no answer references that question")]
	public void GivenNoAnswerReferencesIt()
	{
		outcome.Answer = null;
	}

	[When(@"an Administrator changes its wording, help text, translations, type, order, privacy, active state, or required state")]
	public void WhenAnAdministratorChangesEachField()
	{
		var question = outcome.Original!;
		var fields = RevisionFields.Of(question.CurrentRevision);

		// One edit per field the scenario names, each carrying the ones before it,
		// so every revision is judged against the one it replaced.
		RevisionFields[] asked =
		[
			fields = fields with { LabelEn = "At which site were you flying?" },
			fields = fields with { HelpTextEn = "The site's usual name." },
			fields = fields with { LabelFr = "À quel site voliez-vous ?" },
			fields = fields with { Type = QuestionType.LongText },
			fields = fields with { DisplayOrder = 3 },
			fields = fields with { IsPrivate = false },
			fields = fields with { IsActive = false },
			fields = fields with { IsRequired = true },
		];

		foreach (var next in asked)
		{
			var before = question.CurrentRevision;
			var snapshot = RevisionFields.Of(before);

			outcome.Live = question.ApplyEdit(
				false, next.Type, next.LabelEn, next.LabelFr, next.IsPrivate, next.IsActive, next.DisplayOrder,
				Noon.AddHours(_edits.Count + 1), next.HelpTextEn, next.HelpTextFr, isRequired: next.IsRequired);

			outcome.Live.ShouldBeSameAs(question);
			_edits.Add((before, snapshot, question.CurrentRevision, next));
		}
	}

	[Then(@"a new complete revision is created with the next revision number")]
	public void ThenEachEditIsANewCompleteRevision()
	{
		_edits.Count.ShouldBe(8);

		foreach (var (before, _, after, asked) in _edits)
		{
			after.Id.ShouldNotBe(before.Id);
			after.RevisionNumber.ShouldBe(before.RevisionNumber + 1);
			RevisionFields.Of(after).ShouldBe(asked);
		}
	}

	[Then(@"the previous revision is left unchanged")]
	public void ThenThePreviousRevisionIsUnchanged()
	{
		foreach (var (before, snapshot, _, _) in _edits)
		{
			RevisionFields.Of(before).ShouldBe(snapshot);
		}

		outcome.Original!.Revisions.Count.ShouldBe(_edits.Count + 1);
	}

	[Then(@"the question keeps its identifier")]
	public void ThenTheQuestionKeepsItsIdentifier()
	{
		outcome.Live!.Id.ShouldBe(outcome.Original!.Id);
		outcome.Original.Deleted.ShouldBeNull();
	}

	// --- REQ-QB-002 and REQ-QB-003: an answered question forks ---

	[Then(@"the original question is stamped as deleted")]
	public void ThenTheOriginalIsStampedDeleted()
	{
		outcome.Original!.Deleted.ShouldNotBeNull();
		outcome.Original.IsActive.ShouldBeFalse();
	}

	[Then(@"a new question is created with a new identifier")]
	public void ThenANewQuestionIsCreated()
	{
		outcome.Live!.Id.ShouldNotBe(outcome.Original!.Id);
		outcome.Live.Deleted.ShouldBeNull();
	}

	[Then(@"the new question carries the same stable key")]
	public void ThenTheNewQuestionKeepsTheKey()
	{
		outcome.Live!.Key.ShouldBe(outcome.Original!.Key);
	}

	[Then(@"the new question starts its own revision numbering")]
	public void ThenTheNewQuestionStartsAtRevisionOne()
	{
		var revision = outcome.Live!.Revisions.ShouldHaveSingleItem();
		revision.RevisionNumber.ShouldBe(1);
		revision.QuestionId.ShouldBe(outcome.Live.Id);
	}

	[Then(@"the answers already given still refer to the retired question and its original wording")]
	public void ThenOldAnswersKeepTheirWording()
	{
		var answer = outcome.Answer!;
		answer.QuestionId.ShouldBe(outcome.Original!.Id);

		var answered = outcome.Original.Revisions.Single(revision => revision.Id == answer.QuestionRevisionId);
		answered.LabelEn.ShouldBe(outcome.OriginalLabelEn);
		outcome.Live!.CurrentRevision.LabelEn.ShouldNotBe(outcome.OriginalLabelEn);
	}

	// --- REQ-QB-006: publication consent revises in place ---

	[Then(@"a new revision is created for it")]
	public void ThenANewRevisionIsCreatedForIt()
	{
		var live = outcome.Live!;
		live.ShouldBeSameAs(outcome.Original);
		live.CurrentRevision.RevisionNumber.ShouldBe(2);
		live.CurrentRevision.LabelEn.ShouldNotBe(outcome.OriginalLabelEn);
	}

	[Then(@"it is never stamped as deleted")]
	public void ThenItIsNeverDeleted()
	{
		outcome.Original!.Deleted.ShouldBeNull();
		outcome.Original.IsActive.ShouldBeTrue();
	}

	/// <summary>Every field the scenario names, as one comparable value.</summary>
	private sealed record RevisionFields(
		QuestionType Type,
		string LabelEn,
		string LabelFr,
		string? HelpTextEn,
		string? HelpTextFr,
		int DisplayOrder,
		bool IsPrivate,
		bool IsActive,
		bool IsRequired)
	{
		public static RevisionFields Of(QuestionRevision revision)
		{
			return new RevisionFields(
				revision.Type, revision.LabelEn, revision.LabelFr, revision.HelpTextEn, revision.HelpTextFr,
				revision.DisplayOrder, revision.IsPrivate, revision.IsActive, revision.IsRequired);
		}
	}
}
