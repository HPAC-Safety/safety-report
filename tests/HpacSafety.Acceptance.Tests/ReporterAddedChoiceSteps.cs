using HpacSafety.Core;
using HpacSafety.Core.Features.QuestionBank;
using HpacSafety.Core.Features.Reporting;
using Reqnroll;
using Shouldly;

namespace HpacSafety.Acceptance.Tests;

/// <summary>
///     The non-<c>@ui</c> scenarios for a reporter adding a missing type-ahead
///     choice, and for which list a question renders — ADR-0063.
/// </summary>
/// <remarks>
///     These run against the domain. The submission path that will call
///     <see cref="OptionSet.AddFromReporter" /> does not exist yet; what exists is
///     the rule it will call, and that is what these pin down. Every site name
///     here is synthetic.
/// </remarks>
[Binding]
public sealed class ReporterAddedChoiceSteps
{
#pragma warning disable CA1822 // Reqnroll step bindings must be instance methods to be discovered.

	private static readonly DateTimeOffset Noon = new(2026, 9, 21, 12, 0, 0, TimeSpan.Zero);

	private OptionSet _set = null!;
	private Question _question = null!;
	private QuestionRevision _revision = null!;
	private OptionSetItem? _added;
	private QuestionType _pendingType = QuestionType.Autocomplete;

	[Given(@"a type-ahead question is backed by a shared choice list")]
	public void GivenATypeAheadBackedByAList()
	{
		_set = Sites();
		_question = QuestionOfType(QuestionType.Autocomplete);
		_revision = _question.CurrentRevision;
	}

	[Given(@"a type-ahead revision was saved when the shared list was shorter")]
	public void GivenARevisionSavedEarlier()
	{
		GivenATypeAheadBackedByAList();
	}

	[Given(@"a type-ahead revision was built from a shared choice list")]
	public void GivenARevisionBuiltFromAList()
	{
		GivenATypeAheadBackedByAList();
	}

	[Given(@"a (.*) revision is backed by a shared choice list")]
	public void GivenARevisionOfType(string type)
	{
		EnumCode.TryParse(type, out _pendingType).ShouldBeTrue();

		_set = Sites();
		_question = QuestionOfType(_pendingType);
		_revision = _question.CurrentRevision;
	}

	[Given(@"a reporter has already added a site to a shared choice list")]
	public void GivenAReporterAlreadyAddedASite()
	{
		GivenATypeAheadBackedByAList();
		_added = _set.AddFromReporter("Mount 7", Locale.EnCa);
	}

	[Given(@"an Administrator removed a choice from a shared list")]
	public void GivenAnAdministratorRemovedAChoice()
	{
		GivenATypeAheadBackedByAList();
		_set.Remove("woodside", Noon.AddHours(1));
	}

	[When(@"a reporter submits an answer naming a site the list does not offer")]
	public void WhenAReporterNamesANewSite()
	{
		_added = _set.AddFromReporter("Mount 7", Locale.EnCa);
	}

	[When(@"a reporter answering in French submits ""(.*)"", which the list does not offer")]
	public void WhenAFrenchReporterNamesANewSite(string typed)
	{
		_added = _set.AddFromReporter(typed, Locale.FrCa);
	}

	[Then(@"the list gains a reporter-added choice whose French wording is ""(.*)""")]
	public void ThenTheListGainsAFrenchChoice(string typed)
	{
		_added!.AddedByReporter.ShouldBeTrue();
		_added.LabelFr.ShouldBe(typed);
		_set.Items.ShouldContain(_added);
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

	[When(@"another reporter submits the same site name")]
	public void WhenAnotherReporterNamesTheSameSite()
	{
		_added = _set.AddFromReporter("mount 7", Locale.EnCa);
	}

	[When(@"a reporter submits that same value again")]
	public void WhenAReporterRetypesARemovedValue()
	{
		_added = _set.AddFromReporter("Woodside", Locale.EnCa);
	}

	[When(@"a choice is added to that list afterwards")]
	public void WhenAChoiceIsAddedAfterwards()
	{
		_set.Add("mount_7", "Mount 7", "Mont 7");
	}

	[When(@"that shared list is retired entirely")]
	public void WhenTheListIsRetired()
	{
		_set.Delete(Noon.AddHours(1));
	}

	[Then(@"the site is added to the shared list as a reporter-added choice")]
	public void ThenItIsAddedAsReporterAdded()
	{
		_added!.AddedByReporter.ShouldBeTrue();
		_set.Items.Select(item => item.Code).ShouldContain("mount_7");
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
		// Nothing on the submission path translates, so the typed words stand
		// in for both languages and the flag says so (ADR-0072).
		_added!.NeedsTranslation.ShouldBeTrue();
		_added.Label(Locale.FrCa).ShouldBe("Mount 7");
	}

	[Then(@"the next reporter is offered it")]
	public void ThenTheNextReporterIsOfferedIt()
	{
		_set.Items.Where(item => item.Deleted is null).Select(item => item.Code).ShouldContain("mount_7");
	}

	[Then(@"the reporter's answer still records the value they typed")]
	public void ThenTheAnswerRecordsTheTypedValue()
	{
		// An answer holds its own words and points at no row at all (ADR-0072),
		// so a removed choice cannot reach back and change what it says.
		var report = new Report(Locale.EnCa, Noon);
		var answer = report.Answer(_question, "Woodside", Noon);

		answer.Value.ShouldBe("Woodside");
	}

	[Then(@"the existing choice is reused rather than duplicated")]
	public void ThenTheExistingChoiceIsReused()
	{
		_set.Items.Count(item => item.Code == "mount_7").ShouldBe(1);
	}

	[Then(@"an administrator's wording is never replaced by a reporter's")]
	public void ThenTheWordingIsNotReplaced()
	{
		_added!.LabelEn.ShouldBe("Mount 7");
	}

	[Then(@"the choice stays removed from the list")]
	public void ThenItStaysRemoved()
	{
		_set.Items.Select(item => item.Code).ShouldNotContain("woodside");
	}

	[Then(@"the reporter's answer still refers to the existing row")]
	public void ThenTheAnswerStillRefersToARow()
	{
		_added.ShouldNotBeNull();
		_added.Code.ShouldBe("woodside");
		_added.Deleted.ShouldNotBeNull();
	}

	[Then(@"the question now offers the longer list")]
	public void ThenItOffersTheLongerList()
	{
		QuestionChoices.For(_revision, _set).Select(option => option.Code).ShouldContain("mount_7");
	}

	[Then(@"the revision still records the shorter one")]
	public void ThenTheRevisionRecordsTheShorterOne()
	{
		QuestionChoices.Snapshot(_revision).Select(option => option.Code).ShouldNotContain("mount_7");
	}

	[Then(@"the question offers the live list")]
	public void ThenItOffersTheLiveList()
	{
		QuestionChoices.RendersLiveSet(_revision, _set).ShouldBeTrue();
		QuestionChoices.For(_revision, _set).Select(option => option.Code).ShouldContain("mount_7");
	}

	[Then(@"the question offers its own snapshot")]
	public void ThenItOffersItsSnapshot()
	{
		QuestionChoices.RendersLiveSet(_revision, _set).ShouldBeFalse();
		QuestionChoices.For(_revision, _set).Select(option => option.Code).ShouldNotContain("mount_7");
	}

	[Then(@"the question still offers the choices its revision recorded")]
	public void ThenARetiredListFallsBackToTheSnapshot()
	{
		QuestionChoices.RendersLiveSet(_revision, _set).ShouldBeFalse();

		QuestionChoices.For(_revision, _set)
			.Select(option => option.Code)
			.ShouldBe(QuestionChoices.Snapshot(_revision).Select(option => option.Code));
	}

	private static OptionSet Sites()
	{
		var set = OptionSet.Create("sites", "Flying sites", "Sites de vol", Noon);
		set.Add("coopers", "Cooper's", "Cooper's");
		set.Add("woodside", "Woodside", "Woodside");
		return set;
	}

	private Question QuestionOfType(QuestionType type)
	{
		return Question.Create(
			"where_did_this_happen",
			type,
			"Where did this happen?",
			"Où cela s'est-il produit ?",
			Noon,
			isActive: true,
			optionSetId: _set.Id,
			options: _set.AsRevisionOptions());
	}
}
