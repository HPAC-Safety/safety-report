using System.Text.Json;
using HpacSafety.Core;
using HpacSafety.Core.Features.QuestionBank;
using HpacSafety.Core.Features.QuestionBank.Typeform;
using Reqnroll;
using Shouldly;

namespace HpacSafety.Acceptance.Tests;

/// <summary>
///     The non-<c>@ui</c>, non-API scenarios in
///     <c>features/typeform-question-import-export/typeform-question-import-export.feature</c>
///     that describe mapping behavior — everything <see cref="TypeformQuestionMapper" />
///     decides on its own, without a database or an HTTP endpoint. See
///     ADR-0077, ADR-0078.
/// </summary>
[Binding]
public sealed class TypeformImportSteps
{
#pragma warning disable CA1822 // Reqnroll step bindings must be instance methods to be discovered.

	private readonly List<TypeformField> _englishFields = [];
	private readonly List<TypeformField> _frenchFields = [];
	private readonly List<TypeformLogicRule> _logic = [];
	private string _focusRef = "";
	private TypeformImportResult? _result;

	[Given(@"an Administrator has an English Typeform export and a matching French one")]
	public void GivenAnAdministratorHasAMatchedPair()
	{
		// Contextual. Every scenario below builds the specific field(s) it needs.
	}

	[Given(@"a field's ref appears in the English file but not the French one")]
	public void GivenAFieldRefIsMissingFromFrench()
	{
		_focusRef = "only-in-english";
		_englishFields.Add(Field(_focusRef, "Only in English", "short_text"));
	}

	[Given(@"a multiple_choice field's choice ref appears in the English file but not the French one")]
	public void GivenAChoiceRefIsMissingFromFrench()
	{
		_focusRef = "choice-mismatch-field";
		var choices = new List<TypeformChoice> { new("a", "a", "A"), new("b", "b", "B") };
		_englishFields.Add(ChoiceField(_focusRef, "Pick one", "multiple_choice", choices, false, false));
		_frenchFields.Add(
			ChoiceField(_focusRef, "Choisir", "multiple_choice", [new TypeformChoice("a", "a", "A (fr)")], false, false));
	}

	[Given(@"a Typeform field of type (.*)")]
	public void GivenATypeformFieldOfType(string typeformType)
	{
		_focusRef = "typed-field";

		var (englishField, frenchField) = typeformType switch
		{
			"dropdown" => (
				ChoiceField(_focusRef, "Pick", "dropdown", [new TypeformChoice("a", "a", "A")], null, null),
				ChoiceField(_focusRef, "Choisir", "dropdown", [new TypeformChoice("a", "a", "A (fr)")], null, null)),
			_ => (Field(_focusRef, "Field", typeformType), Field(_focusRef, "Champ", typeformType))
		};

		_englishFields.Add(englishField);
		_frenchFields.Add(frenchField);
	}

	[Given(@"a Typeform multiple_choice field that does not allow multiple selection")]
	public void GivenASingleSelectMultipleChoiceField()
	{
		_focusRef = "single-choice-field";
		var choices = new List<TypeformChoice> { new("a", "a", "A") };
		_englishFields.Add(ChoiceField(_focusRef, "Pick one", "multiple_choice", choices, false, false));
		_frenchFields.Add(ChoiceField(_focusRef, "Choisir", "multiple_choice", choices, false, false));
	}

	[Given(@"a Typeform multiple_choice field that allows multiple selection$")]
	public void GivenAMultiSelectMultipleChoiceField()
	{
		_focusRef = "multi-choice-field";
		var choices = new List<TypeformChoice> { new("a", "a", "A"), new("b", "b", "B") };
		_englishFields.Add(ChoiceField(_focusRef, "Pick many", "multiple_choice", choices, true, false));
		_frenchFields.Add(ChoiceField(_focusRef, "Choisir plusieurs", "multiple_choice", choices, true, false));
	}

	[Given(@"a Typeform multiple_choice field that allows multiple selection and an other choice")]
	public void GivenAMultiSelectFieldWithOtherChoice()
	{
		_focusRef = "multi-choice-other-field";
		var choices = new List<TypeformChoice> { new("a", "a", "A") };
		_englishFields.Add(ChoiceField(_focusRef, "Ratings", "multiple_choice", choices, true, true));
		_frenchFields.Add(ChoiceField(_focusRef, "Qualifications", "multiple_choice", choices, true, true));
	}

	[Given(@"a Typeform group field containing several nested fields")]
	public void GivenAGroupFieldWithNestedFields()
	{
		_focusRef = "group-field";
		_englishFields.Add(GroupField(_focusRef, "Aircraft", "group", "make-child", "model-child", "Make", "Model"));
		_frenchFields.Add(GroupField(_focusRef, "Aéronef", "group", "make-child", "model-child", "Fabricant", "Modèle"));
	}

	[Given(@"a Typeform contact_info field containing name, phone, and email subfields")]
	public void GivenAContactInfoField()
	{
		_focusRef = "contact-field";
		_englishFields.Add(
			GroupField(_focusRef, "From", "contact_info", "name-child", "email-child", "Name", "Email"));
		_frenchFields.Add(
			GroupField(_focusRef, "De", "contact_info", "name-child", "email-child", "Nom", "Courriel"));
	}

	[Given(@"a Typeform statement field whose description only interpolates other fields")]
	public void GivenAGeneratedRecapStatement()
	{
		_focusRef = "recap-field";
		var field = new TypeformField(
			"1", _focusRef, "Summary", "statement",
			null, new TypeformFieldProperties("Name: {{field:some-ref}}", null, null, null, null));
		_englishFields.Add(field);
		_frenchFields.Add(field with { Title = "Résumé" });
	}

	[Given(@"a Typeform field of a type this system does not support")]
	public void GivenAnUnsupportedTypeformField()
	{
		_focusRef = "unsupported-field";
		_englishFields.Add(Field(_focusRef, "Rank these", "ranking"));
		_frenchFields.Add(Field(_focusRef, "Classer", "ranking"));
	}

	[Given(@"a Typeform field whose jump logic has only its unconditional fallback action")]
	public void GivenAFieldWithOnlyLinearFlow()
	{
		_focusRef = "linear-field";
		_englishFields.Add(Field(_focusRef, "Field", "short_text"));
		_frenchFields.Add(Field(_focusRef, "Champ", "short_text"));
		_logic.Add(new TypeformLogicRule("field", _focusRef, [AlwaysAction("next-ref")]));
	}

	[Given(@"a Typeform field whose jump logic includes a real condition")]
	public void GivenAFieldWithARealCondition()
	{
		_focusRef = "conditional-field";
		_englishFields.Add(Field(_focusRef, "Field", "short_text"));
		_frenchFields.Add(Field(_focusRef, "Champ", "short_text"));
		_logic.Add(
			new TypeformLogicRule("field", _focusRef, [IsAction("other-ref", "some-choice"), AlwaysAction("next-ref")]));
	}

	[Given(@"a Typeform field with a given ref")]
	public void GivenATypeformFieldWithAGivenRef()
	{
		_focusRef = "6f9c2f55-a0df-4afa-9744-9681b60933dd";
		_englishFields.Add(Field(_focusRef, "Field", "short_text"));
		_frenchFields.Add(Field(_focusRef, "Champ", "short_text"));
	}

	[When(@"the pair is mapped")]
	public void WhenThePairIsMapped()
	{
		_result = TypeformQuestionMapper.Map(
			new TypeformDocument(_englishFields, _logic), new TypeformDocument(_frenchFields, []));
	}

	[Then(@"a draft is still produced for that field")]
	public void ThenADraftIsStillProduced()
	{
		_result!.Drafts.ShouldContain(draft => draft.Key == QuestionKey.Normalize(_focusRef));
	}

	[Then(@"its French text defaults to the English text")]
	public void ThenFrenchDefaultsToEnglish()
	{
		var draft = Draft();
		draft.LabelFr.ShouldBe(draft.LabelEn);
	}

	[Then(@"the draft is flagged that French still needs review")]
	public void ThenTheDraftIsFlagged()
	{
		Draft().FrenchDefaultedToEnglish.ShouldBeTrue();
	}

	[Then(@"a draft is still produced with that choice")]
	public void ThenADraftIsProducedWithTheChoice()
	{
		Draft().Options.ShouldContain(option => option.Code == "b");
	}

	[Then(@"the choice's French label defaults to its English label")]
	public void ThenTheChoiceFrenchLabelDefaultsToEnglish()
	{
		var option = Draft().Options.Single(o => o.Code == "b");
		option.LabelFr.ShouldBe(option.LabelEn);
	}

	[Then(@"the choice is flagged that French still needs review")]
	public void ThenTheChoiceIsFlagged()
	{
		Draft().Options.Single(o => o.Code == "b").FrenchDefaultedToEnglish.ShouldBeTrue();
	}

	[Then(@"it produces a draft of type (.*)")]
	public void ThenItProducesADraftOfType(string questionType)
	{
		EnumCode.TryParse<QuestionType>(questionType, out var expected).ShouldBeTrue();
		Draft().Type.ShouldBe(expected);
	}

	[Then(@"it produces a single-select draft seeded from its choices")]
	public void ThenItProducesASingleSelectDraft()
	{
		Draft().Type.ShouldBe(QuestionType.SingleSelect);
		Draft().Options.ShouldNotBeEmpty();
	}

	[Then(@"it produces a multi-select draft seeded from its choices")]
	public void ThenItProducesAMultiSelectDraft()
	{
		Draft().Type.ShouldBe(QuestionType.MultiSelect);
		Draft().Options.ShouldNotBeEmpty();
	}

	[Then(@"reporter additions are not enabled")]
	public void ThenReporterAdditionsAreNotEnabled()
	{
		Draft().AllowsReporterAdditions.ShouldBeFalse();
	}

	[Then(@"it produces a multi-select draft with reporter additions enabled")]
	public void ThenItProducesAMultiSelectDraftWithReporterAdditions()
	{
		Draft().Type.ShouldBe(QuestionType.MultiSelect);
		Draft().AllowsReporterAdditions.ShouldBeTrue();
	}

	[Then(@"it produces one group draft from the field's title")]
	public void ThenItProducesOneGroupDraft()
	{
		Draft().Type.ShouldBe(QuestionType.Group);
	}

	[Then(@"one draft per nested field, each grouped under it")]
	public void ThenOneDraftPerNestedFieldGroupedUnderIt()
	{
		var groupKey = QuestionKey.Normalize(_focusRef);
		var children = _result!.Drafts.Where(draft => draft.GroupedUnderKey == groupKey).ToList();
		children.Count.ShouldBe(2);
	}

	[Then(@"one draft per subfield, each grouped under it")]
	public void ThenOneDraftPerSubfieldGroupedUnderIt()
	{
		ThenOneDraftPerNestedFieldGroupedUnderIt();
	}

	[Then(@"no draft is produced for it")]
	public void ThenNoDraftIsProducedForIt()
	{
		_result!.Drafts.ShouldNotContain(draft => draft.Key == QuestionKey.Normalize(_focusRef));
	}

	[Then(@"the import report lists it as not imported")]
	public void ThenTheImportReportListsItAsNotImported()
	{
		_result!.Rejected.ShouldContain(field => field.Ref == _focusRef);
	}

	[Then(@"no pending logic note is recorded for that field")]
	public void ThenNoPendingLogicNoteIsRecorded()
	{
		_result!.PendingLogic.ShouldNotContain(entry => entry.FieldRef == _focusRef);
	}

	[Then(@"the produced draft is unconditional")]
	public void ThenTheProducedDraftIsUnconditional()
	{
		// Contextual — this mapper never sets a dependency at all (ADR-0078);
		// asserted structurally by ImportedQuestionDraft having no such field.
	}

	[Then(@"a pending logic note is recorded naming that field and its original logic")]
	public void ThenAPendingLogicNoteIsRecorded()
	{
		var note = _result!.PendingLogic.Single(entry => entry.FieldRef == _focusRef);
		note.RawLogicJson.ShouldNotBeNullOrWhiteSpace();
	}

	[Then(@"the produced draft's key is that ref, normalized")]
	public void ThenTheDraftsKeyIsTheNormalizedRef()
	{
		Draft().Key.ShouldBe(QuestionKey.Normalize(_focusRef));
	}

	private ImportedQuestionDraft Draft()
	{
		return _result!.Drafts.Single(draft => draft.Key == QuestionKey.Normalize(_focusRef));
	}

	private static TypeformField Field(string @ref, string title, string type)
	{
		return new TypeformField("id", @ref, title, type, null, new TypeformFieldProperties(null, null, null, null, null));
	}

	private static TypeformField ChoiceField(
		string @ref, string title, string type, IReadOnlyList<TypeformChoice> choices, bool? multi, bool? other)
	{
		return new TypeformField("id", @ref, title, type, null, new TypeformFieldProperties(null, multi, other, choices, null));
	}

	private static TypeformField GroupField(
		string @ref, string title, string type, string childRef1, string childRef2, string childTitle1, string childTitle2)
	{
		var children = new List<TypeformField>
		{
			Field(childRef1, childTitle1, "short_text"), Field(childRef2, childTitle2, "short_text")
		};

		return new TypeformField("id", @ref, title, type, null, new TypeformFieldProperties(null, null, null, null, children));
	}

	private static JsonElement AlwaysAction(string toRef)
	{
		return JsonSerializer.SerializeToElement(
			new { action = "jump", details = new { to = new { type = "field", value = toRef } }, condition = new { op = "always", vars = Array.Empty<object>() } });
	}

	private static JsonElement IsAction(string fieldRef, string choiceRef)
	{
		return JsonSerializer.SerializeToElement(
			new
			{
				action = "jump",
				details = new { to = new { type = "field", value = "some-target" } },
				condition = new
				{
					op = "is",
					vars = new object[]
					{
						new { type = "field", value = fieldRef }, new { type = "choice", value = choiceRef }
					}
				}
			});
	}
}
