using HpacSafety.Core.Features.QuestionBank;
using Shouldly;

namespace HpacSafety.Core.Tests;

/// <summary>
///     Which list a question renders, and what a reporter typing a missing value
///     does to a shared set — ADR-0063.
/// </summary>
public class QuestionChoicesTests
{
	private static readonly DateTimeOffset At = new(2026, 9, 21, 12, 0, 0, TimeSpan.Zero);

	private static OptionSet Sites()
	{
		var set = OptionSet.Create("sites", "Flying sites", "Sites de vol", At);
		set.Add("coopers", "Cooper's", "Cooper's");
		set.Add("woodside", "Woodside", "Woodside");
		return set;
	}

	private static Question BackedBy(
		OptionSet set, QuestionType type = QuestionType.Autocomplete, bool allowsReporterAdditions = false)
	{
		return Question.Create(
			"where_did_this_happen", type, "Where did this happen?", "Où cela s'est-il produit ?", At,
			isActive: true, optionSetId: set.Id, allowsReporterAdditions: allowsReporterAdditions,
			options: set.AsRevisionOptions());
	}

	[Fact]
	public void GivenTypeAhead_WhenListGrows_ThenOffersNewChoice()
	{
		// Given
		var set = Sites();
		var question = BackedBy(set);

		// When
		set.Add("mount_7", "Mount 7", "Mont 7");

		// Then — the next pilot sees it without anyone republishing the question
		QuestionChoices.For(question.CurrentRevision, set)
			.Select(option => option.Code)
			.ShouldBe(["coopers", "woodside", "mount_7"]);
	}

	[Fact]
	public void GivenTypeAhead_WhenListGrows_ThenRevisionStillRecordsWhatWasShown()
	{
		// Given
		var set = Sites();
		var question = BackedBy(set);

		// When
		set.Add("mount_7", "Mount 7", "Mont 7");

		// Then — the record of what that reporter was offered is untouched
		QuestionChoices.Snapshot(question.CurrentRevision)
			.Select(option => option.Code)
			.ShouldBe(["coopers", "woodside"]);
	}

	[Theory]
	[InlineData(QuestionType.SingleSelect)]
	[InlineData(QuestionType.MultiSelect)]
	public void GivenClosedListType_WhenSharedListGrows_ThenStillRendersSnapshot(QuestionType type)
	{
		// Given — a curated, closed set; showing an unmentioned choice would
		// make the revision's record misleading
		var set = Sites();
		var question = BackedBy(set, type);

		// When
		set.Add("mount_7", "Mount 7", "Mont 7");

		// Then
		QuestionChoices.RendersLiveSet(question.CurrentRevision, set).ShouldBeFalse();
		QuestionChoices.For(question.CurrentRevision, set)
			.Select(option => option.Code)
			.ShouldBe(["coopers", "woodside"]);
	}

	// ------------------------------------------------ AllowsReporterAdditions (ADR-0077) --

	[Fact]
	public void GivenAutocomplete_WhenCreatedWithFlagFalse_ThenAlwaysAllowsAdditionsAnyway()
	{
		// Given / When — the caller's value is irrelevant; autocomplete is
		// always on, matching its pre-flag behaviour exactly.
		var question = Question.Create(
			"launch_site", QuestionType.Autocomplete, "Where from?", "D'où ?", At, isActive: true,
			allowsReporterAdditions: false);

		// Then
		question.AllowsReporterAdditions.ShouldBeTrue();
	}

	[Fact]
	public void GivenMultiSelect_WhenCreatedWithNoFlag_ThenDefaultsToClosed()
	{
		// Given / When
		var question = Question.Create(
			"ratings", QuestionType.MultiSelect, "Ratings", "Qualifications", At, isActive: true,
			options: [new QuestionOptionInput("p3", "P3", "P3")]);

		// Then
		question.AllowsReporterAdditions.ShouldBeFalse();
	}

	[Fact]
	public void GivenMultiSelect_WhenAuthoredWithFlagTrue_ThenAllowsAdditions()
	{
		// Given / When
		var question = Question.Create(
			"ratings", QuestionType.MultiSelect, "Ratings", "Qualifications", At, isActive: true,
			allowsReporterAdditions: true, options: [new QuestionOptionInput("p3", "P3", "P3")]);

		// Then
		question.AllowsReporterAdditions.ShouldBeTrue();
	}

	[Theory]
	[InlineData(QuestionType.SingleSelect)]
	[InlineData(QuestionType.YesNo)]
	[InlineData(QuestionType.ShortText)]
	public void GivenTypeOtherThanAutocompleteOrMultiSelect_WhenFlagIsTrue_ThenRejected(QuestionType type)
	{
		// Given / When / Then
		Should.Throw<DomainRuleViolationException>(() => Question.Create(
			"authored", type, "A question", "Une question", At, isActive: true, allowsReporterAdditions: true));
	}

	[Fact]
	public void GivenFlaggedMultiSelect_WhenSharedListGrows_ThenOffersNewChoice()
	{
		// Given
		var set = Sites();
		var question = BackedBy(set, QuestionType.MultiSelect, allowsReporterAdditions: true);

		// When
		set.Add("mount_7", "Mount 7", "Mont 7");

		// Then — exactly like an autocomplete, once the flag is on
		QuestionChoices.RendersLiveSet(question.CurrentRevision, set).ShouldBeTrue();
		QuestionChoices.For(question.CurrentRevision, set)
			.Select(option => option.Code)
			.ShouldBe(["coopers", "woodside", "mount_7"]);
	}

	[Fact]
	public void GivenUnflaggedMultiSelect_WhenSharedListGrows_ThenStillRendersSnapshot()
	{
		// Given — the default: an ordinary multi-select stays closed
		var set = Sites();
		var question = BackedBy(set, QuestionType.MultiSelect);

		// When
		set.Add("mount_7", "Mount 7", "Mont 7");

		// Then
		QuestionChoices.RendersLiveSet(question.CurrentRevision, set).ShouldBeFalse();
	}

	[Fact]
	public void GivenMultiSelect_WhenReporterAdditionsAreTurnedOn_ThenNewRevisionRecords()
	{
		// Given
		var question = Question.Create(
			"ratings", QuestionType.MultiSelect, "Ratings", "Qualifications", At, isActive: true,
			options: [new QuestionOptionInput("p3", "P3", "P3")]);

		// When
		question.AllowReporterAdditions(true, At.AddHours(1));

		// Then
		question.AllowsReporterAdditions.ShouldBeTrue();
		question.CurrentRevision.RevisionNumber.ShouldBe(2);
	}

	[Fact]
	public void GivenTypeAhead_WhenSharedListIsRetired_ThenFallsBackToSnapshot()
	{
		// Given
		var set = Sites();
		var question = BackedBy(set);

		// When
		set.Delete(At.AddHours(1));

		// Then — a retired set leaves the question showing what it recorded,
		// rather than showing nothing
		QuestionChoices.RendersLiveSet(question.CurrentRevision, set).ShouldBeFalse();
		QuestionChoices.For(question.CurrentRevision, set)
			.Select(option => option.Code)
			.ShouldBe(["coopers", "woodside"]);
	}

	[Fact]
	public void GivenNoSharedListIsLoaded_WhenChoicesAreResolved_ThenSnapshotIsUsed()
	{
		// Given
		var set = Sites();
		var question = BackedBy(set);

		// When / Then
		QuestionChoices.For(question.CurrentRevision, null)
			.Select(option => option.Code)
			.ShouldBe(["coopers", "woodside"]);
	}

	[Fact]
	public void GivenDifferentList_WhenChoicesAreResolved_ThenSnapshotIsUsed()
	{
		// Given — a revision never renders a set it does not name
		var set = Sites();
		var question = BackedBy(set);
		var unrelated = OptionSet.Create("provinces", "Provinces", "Provinces", At);
		unrelated.Add("alberta", "Alberta", "Alberta");

		// When / Then
		QuestionChoices.RendersLiveSet(question.CurrentRevision, unrelated).ShouldBeFalse();
		QuestionChoices.For(question.CurrentRevision, unrelated)
			.Select(option => option.Code)
			.ShouldBe(["coopers", "woodside"]);
	}

	[Fact]
	public void GivenHandTypedQuestion_WhenChoicesAreResolved_ThenSnapshotIsUsed()
	{
		// Given — an autocomplete whose options were typed for it alone
		var question = Question.Create(
			"launch_site", QuestionType.Autocomplete, "Where from?", "D'où ?", At, isActive: true,
			options: [new QuestionOptionInput("golden", "Golden", "Golden")]);

		// When / Then
		QuestionChoices.For(question.CurrentRevision, null)
			.Select(option => option.Code)
			.ShouldBe(["golden"]);
	}

	[Fact]
	public void GivenFrenchReporter_WhenTheyAddAChoice_ThenRecordedInFrenchWithCodeFromTheFrench()
	{
		// Given
		var set = Sites();

		// When
		var added = set.AddFromReporter("Élévation Sainte-Anne", Locale.FrCa);

		// Then
		added.Code.ShouldBe("elevation_sainte_anne");
		added.LabelFr.ShouldBe("Élévation Sainte-Anne");
		added.ReporterLocale.ShouldBe(Locale.FrCa);
		added.NeedsTranslation.ShouldBeTrue();
	}

	[Fact]
	public void GivenChoiceWordedThatWayInReportersLanguage_WhenTheyTypeIt_ThenExistingChoiceIsReused()
	{
		// Given
		var set = Sites();
		set.Add("mount_7", "Mount 7", "Mont 7");

		// When
		var reused = set.AddFromReporter("mont 7", Locale.FrCa);

		// Then
		reused.Code.ShouldBe("mount_7");
		reused.AddedByReporter.ShouldBeFalse();
		set.Items.Count.ShouldBe(3);
	}

	[Fact]
	public void GivenValueListDoesNotOffer_WhenReporterSubmits_ThenAddedAndMarked()
	{
		// Given
		var set = Sites();

		// When
		var added = set.AddFromReporter("Mount 7", Locale.EnCa);

		// Then
		added.Code.ShouldBe("mount_7");
		added.AddedByReporter.ShouldBeTrue();
		added.LabelEn.ShouldBe("Mount 7");

		// Nothing on the submission path translates, so the reporter's own
		// words stand in for the other language and say so (ADR-0072).
		added.Label(Locale.FrCa).ShouldBe("Mount 7");
		added.NeedsTranslation.ShouldBeTrue();
		set.Items.Select(candidate => candidate.Code).ShouldBe(["coopers", "woodside", "mount_7"]);
	}

	[Fact]
	public void GivenValueAlreadyOffered_WhenReporterSubmits_ThenNothingIsAddedOrRelabelled()
	{
		// Given — a reporter's spelling never overwrites an administrator's
		var set = Sites();

		// When
		var added = set.AddFromReporter("coopers", Locale.EnCa);

		// Then
		added.LabelEn.ShouldBe("Cooper's");
		added.AddedByReporter.ShouldBeFalse();
		set.Items.Count.ShouldBe(2);
	}

	[Fact]
	public void GivenTwoReportersNamingSameNewSite_WhenBothSubmit_ThenOneChoiceExists()
	{
		// Given
		var set = Sites();

		// When — the second types it differently; both normalize to one code
		var first = set.AddFromReporter("Mount 7", Locale.EnCa);
		var second = set.AddFromReporter("mount  7", Locale.EnCa);

		// Then
		second.Id.ShouldBe(first.Id);
		second.LabelEn.ShouldBe("Mount 7");
		set.Items.Count(item => item.Code == "mount_7").ShouldBe(1);
	}

	[Fact]
	public void GivenChoiceAdministratorRemoved_WhenReporterRetypes_ThenNotRevived()
	{
		// Given — removal is the only curation tool there is
		var set = Sites();
		set.Remove("woodside", At.AddHours(1));

		// When
		var added = set.AddFromReporter("Woodside", Locale.EnCa);

		// Then — the answer points at a real row, but the list still does not
		// offer it
		added.Code.ShouldBe("woodside");
		added.Deleted.ShouldNotBeNull();
		set.Items.Select(item => item.Code).ShouldBe(["coopers"]);
	}

	[Fact]
	public void GivenRetiredList_WhenReporterAddsTo_ThenRefused()
	{
		// Given
		var set = Sites();
		set.Delete(At.AddHours(1));

		// When / Then
		Should.Throw<DomainRuleViolationException>(() => set.AddFromReporter("Mount 7", Locale.EnCa));
	}

	[Fact]
	public void GivenBlankLabel_WhenReporterAddsChoice_ThenRefused()
	{
		// Given — a choice nobody can read is not a choice
		var set = Sites();

		// When / Then
		Should.Throw<DomainRuleViolationException>(() => set.AddFromReporter("", Locale.EnCa));
		Should.Throw<DomainRuleViolationException>(() => set.AddFromReporter("   ", Locale.EnCa));
	}

	[Fact]
	public void GivenAdministratorAddsChoice_WhenRead_ThenNotMarkedReporterAdded()
	{
		// Given / When
		var set = Sites();

		// Then
		set.Items.ShouldAllBe(item => !item.AddedByReporter);
	}

	[Fact]
	public void GivenReporterAddedChoice_WhenAdministratorRelabels_ThenMarkerStays()
	{
		// Given — the flag records where a choice came from, not whether
		// anyone has touched it since
		var set = Sites();
		set.AddFromReporter("Mount 7", Locale.EnCa);

		// When
		set.Relabel("mount_7", "Mount 7", "Mont 7");

		// Then
		var item = set.Items.Single(candidate => candidate.Code == "mount_7");
		item.LabelEn.ShouldBe("Mount 7");
		item.AddedByReporter.ShouldBeTrue();
	}
}
