using HpacSafety.Core;
using HpacSafety.Core.Features.QuestionBank;
using HpacSafety.Core.Features.QuestionBank.Typeform;
using Shouldly;

namespace HpacSafety.Core.Tests;

/// <summary>
///     Mapping a Typeform English/French export pair into review drafts —
///     ADR-0077.
/// </summary>
public class TypeformQuestionMapperTests
{
	private static readonly string FixturesDirectory = Path.Combine(AppContext.BaseDirectory, "Fixtures", "Typeform");

	private static TypeformDocument Load(string fileName)
	{
		var json = File.ReadAllText(Path.Combine(FixturesDirectory, fileName));
		return TypeformDocument.Parse(json);
	}

	private static TypeformImportResult MapSynthetic()
	{
		return TypeformQuestionMapper.Map(Load("synthetic-en.json"), Load("synthetic-fr.json"));
	}

	private static ImportedQuestionDraft DraftFor(TypeformImportResult result,
												  string ref_)
	{
		return result.Drafts.Single(draft => draft.Key == QuestionKey.Normalize(ref_));
	}

	[Theory]
	[InlineData("name-ref", QuestionType.ShortText)]
	[InlineData("notes-ref", QuestionType.LongText)]
	[InlineData("email-ref", QuestionType.Email)]
	[InlineData("phone-ref", QuestionType.Phone)]
	[InlineData("date-ref", QuestionType.Date)]
	[InlineData("number-ref", QuestionType.Number)]
	[InlineData("file-ref", QuestionType.FileUpload)]
	[InlineData("yesno-ref", QuestionType.YesNo)]
	[InlineData("dropdown-ref", QuestionType.SingleSelect)]
	[InlineData("single-ref", QuestionType.SingleSelect)]
	[InlineData("multi-ref", QuestionType.MultiSelect)]
	[InlineData("multiother-ref", QuestionType.MultiSelect)]
	[InlineData("intro-ref", QuestionType.Statement)]
	[InlineData("group-ref", QuestionType.Group)]
	[InlineData("contact-ref", QuestionType.Group)]
	public void GivenTypeformType_WhenMapped_ThenQuestionTypeMatches(string fieldRef,
																	 QuestionType expected)
	{
		// Given / When
		var result = MapSynthetic();

		// Then
		DraftFor(result, fieldRef).Type.ShouldBe(expected);
	}

	[Fact]
	public void GivenBilingualPair_WhenFieldMatchesByRef_ThenBothLanguagesCarryOver()
	{
		// Given / When
		var result = MapSynthetic();

		// Then
		var name = DraftFor(result, "name-ref");
		name.LabelEn.ShouldBe("Name:");
		name.LabelFr.ShouldBe("Nom :");
		name.FrenchDefaultedToEnglish.ShouldBeFalse();
		name.HelpTextEn.ShouldBe("Your name.");
		name.HelpTextFr.ShouldBe("Votre nom.");
	}

	[Fact]
	public void GivenFieldMissingFromFrenchExport_WhenMapped_ThenDefaultsToEnglishAndIsFlagged()
	{
		// Given — the English file drives which fields exist; a field with
		// no French counterpart by ref still imports rather than failing
		// the whole pair (ADR-0077).
		var result = MapSynthetic();

		// When
		var extra = DraftFor(result, "extra-ref");

		// Then
		extra.LabelEn.ShouldBe("Extra:");
		extra.LabelFr.ShouldBe("Extra:");
		extra.FrenchDefaultedToEnglish.ShouldBeTrue();
	}

	[Fact]
	public void GivenChoiceMissingFromFrenchExport_WhenMapped_ThenThatChoiceDefaultsToEnglish()
	{
		// Given — the French "single-ref" field only offers one of the two
		// English choices.
		var result = MapSynthetic();

		// When
		var single = DraftFor(result, "single-ref");

		// Then
		single.Options.Count.ShouldBe(2);
		var x = single.Options.Single(option => option.Code == "x");
		x.FrenchDefaultedToEnglish.ShouldBeFalse();
		x.LabelFr.ShouldBe("X (fr)");

		var y = single.Options.Single(option => option.Code == "y");
		y.FrenchDefaultedToEnglish.ShouldBeTrue();
		y.LabelFr.ShouldBe("Y");
	}

	[Fact]
	public void GivenMultiSelectWithOtherChoice_WhenMapped_ThenItIsAnOrdinaryMultiSelect()
	{
		// Given / When — allow_other_choice is ignored: only a type-ahead takes a
		// reporter's added choice (ADR-0095).
		var result = MapSynthetic();

		// Then
		DraftFor(result, "multiother-ref").Type.ShouldBe(QuestionType.MultiSelect);
	}

	[Fact]
	public void GivenGroupField_WhenMapped_ThenChildrenAreGroupedUnderIt()
	{
		// Given / When
		var result = MapSynthetic();
		var groupKey = QuestionKey.Normalize("group-ref");

		// Then
		DraftFor(result, "group-ref").GroupedUnderKey.ShouldBeNull();
		DraftFor(result, "make-ref").GroupedUnderKey.ShouldBe(groupKey);
		DraftFor(result, "model-ref").GroupedUnderKey.ShouldBe(groupKey);
	}

	[Fact]
	public void GivenContactInfoField_WhenMapped_ThenSubfieldsAreGroupedUnderIt()
	{
		// Given / When — contact_info flattens exactly like a native group.
		var result = MapSynthetic();
		var groupKey = QuestionKey.Normalize("contact-ref");

		// Then
		DraftFor(result, "contact-ref").Type.ShouldBe(QuestionType.Group);
		DraftFor(result, "first-ref").GroupedUnderKey.ShouldBe(groupKey);
		DraftFor(result, "first-ref").Type.ShouldBe(QuestionType.ShortText);
		DraftFor(result, "cemail-ref").GroupedUnderKey.ShouldBe(groupKey);
		DraftFor(result, "cemail-ref").Type.ShouldBe(QuestionType.Email);
	}

	[Fact]
	public void GivenGeneratedRecapStatement_WhenMapped_ThenNotImported()
	{
		// Given / When — merge-tag-only content, Typeform's own boilerplate.
		var result = MapSynthetic();

		// Then
		result.Drafts.ShouldNotContain(draft => draft.Key == QuestionKey.Normalize("recap-ref"));
	}

	[Fact]
	public void GivenUnsupportedTypeformType_WhenMapped_ThenRejectedNotDropped()
	{
		// Given / When
		var result = MapSynthetic();

		// Then
		result.Drafts.ShouldNotContain(draft => draft.Key == QuestionKey.Normalize("unsupported-ref"));
		var rejection = result.Rejected.Single(field => field.Ref == "unsupported-ref");
		rejection.TypeformType.ShouldBe("ranking");
		rejection.Title.ShouldBe("Rank these:");
	}

	[Fact]
	public void GivenFieldWithARealBranchingCondition_WhenMapped_ThenFlaggedAsPendingLogic()
	{
		// Given / When — "answered yes" jump logic still requires manual
		// wiring in this first pass; see the class remarks on the mapper.
		var result = MapSynthetic();

		// Then
		var pending = result.PendingLogic.Single(entry => entry.FieldRef == "date-ref");
		pending.FieldTitle.ShouldBe("Date:");
		pending.RawLogicJson.ShouldContain("yesno-ref");
	}

	[Fact]
	public void GivenFieldWithOnlyAnAlwaysAction_WhenMapped_ThenNotFlaggedAsPendingLogic()
	{
		// Given / When — a lone "always" jump is normal linear flow, not a
		// real condition.
		var result = MapSynthetic();

		// Then
		result.PendingLogic.ShouldNotContain(entry => entry.FieldRef == "name-ref");
	}

	[Fact]
	public void GivenAFieldRef_WhenMapped_ThenKeyIsTheNormalizedRef()
	{
		// Given / When
		var result = MapSynthetic();

		// Then
		DraftFor(result, "name-ref").Key.ShouldBe(QuestionKey.Normalize("name-ref"));
	}

	// ------------------------------------------------------- real export pair --

	[Fact]
	public void GivenTheOrganizationsRealExportPair_WhenMapped_ThenItImportsWithoutThrowing()
	{
		// Given / When
		var result = TypeformQuestionMapper.Map(Load("form-en.json"), Load("form-fr.json"));

		// Then
		result.Drafts.ShouldNotBeEmpty();
	}

	[Fact]
	public void GivenTheOrganizationsRealExportPair_WhenConsentRefDoesNotMatch_ThenItDefaultsToEnglishRatherThanFailing()
	{
		// Given — the real formENG.json/formFR.json pair carries a different
		// ref for the publication-consent field in each language. The whole
		// pair still imports; only that one field is flagged.
		var result = TypeformQuestionMapper.Map(Load("form-en.json"), Load("form-fr.json"));

		// When
		var consent = DraftFor(result, "08f3eedb-e682-431d-be9b-2d83765bf022");

		// Then
		consent.Type.ShouldBe(QuestionType.YesNo);
		consent.FrenchDefaultedToEnglish.ShouldBeTrue();
	}

	// -------------------------------------------------------- hpac extension --

	private static TypeformField Field(string reference,
									   string title,
									   string typeformType,
									   TypeformHpacExtension? hpac = null)
	{
		return new TypeformField(reference, reference, title, typeformType, SubfieldKey: null, new TypeformFieldProperties(
			Description: null, AllowMultipleSelection: null, AllowOtherChoice: null, Choices: null, Fields: null, hpac));
	}

	private static TypeformDocument Document(params TypeformField[] fields)
	{
		return new TypeformDocument(fields, []);
	}

	[Theory]
	[InlineData("statement", "group")]
	[InlineData("statement", "statement")]
	[InlineData("multiple_choice", "autocomplete")]
	[InlineData("multiple_choice", "single_select")]
	public void GivenAnHpacType_WhenMapped_ThenItOverridesTheAmbiguousNativeType(string nativeType,
																				 string hpacType)
	{
		// Given
		var hpac = new TypeformHpacExtension(hpacType, false, false, null, null, null);
		var english = Document(Field("field-ref", "Field", nativeType, hpac));
		var french = Document(Field("field-ref", "Champ", nativeType, hpac));

		// When
		var result = TypeformQuestionMapper.Map(english, french);

		// Then
		DraftFor(result, "field-ref").Type.ShouldBe(EnumCode.TryParse<QuestionType>(hpacType, out var expected) ? expected : default);
	}

	[Fact]
	public void GivenAnHpacExtension_WhenMapped_ThenPrivacyAndRequiredCarryOver()
	{
		// Given
		var hpac = new TypeformHpacExtension("short_text", true, true, null, null, null);
		var english = Document(Field("field-ref", "Field", "short_text", hpac));
		var french = Document(Field("field-ref", "Champ", "short_text", hpac));

		// When
		var draft = DraftFor(TypeformQuestionMapper.Map(english, french), "field-ref");

		// Then
		draft.IsPrivate.ShouldBeTrue();
		draft.IsRequired.ShouldBeTrue();
	}

	[Fact]
	public void GivenAnHpacExtensionNamingADependencyAndAGroup_WhenMapped_ThenTheyAreSetByKeyEvenThoughTheExportIsFlat()
	{
		// Given — a flat export: no native Typeform group/contact_info nesting
		// carries this relationship, only the hpac extension does.
		var hpac = new TypeformHpacExtension("short_text", false, false, "parent-ref", "yes", "group-ref");
		var english = Document(Field("child-ref", "Child", "short_text", hpac));
		var french = Document(Field("child-ref", "Enfant", "short_text", hpac));

		// When
		var draft = DraftFor(TypeformQuestionMapper.Map(english, french), "child-ref");

		// Then
		draft.DependsOnKey.ShouldBe("parent-ref");
		draft.DependsOnOptionCode.ShouldBe("yes");
		draft.GroupedUnderKey.ShouldBe("group-ref");
	}

	[Fact]
	public void GivenNoHpacExtension_WhenMapped_ThenBehaviorIsUnchangedFromBeforeTheExtensionExisted()
	{
		// Given — a real Typeform export, or a hand-authored fixture, has no
		// hpac object at all.
		var english = Document(Field("field-ref", "Field", "short_text"));
		var french = Document(Field("field-ref", "Champ", "short_text"));

		// When
		var draft = DraftFor(TypeformQuestionMapper.Map(english, french), "field-ref");

		// Then
		draft.IsPrivate.ShouldBeTrue();
		draft.IsRequired.ShouldBeFalse();
		draft.DependsOnKey.ShouldBeNull();
		draft.GroupedUnderKey.ShouldBeNull();
	}

	[Fact]
	public void GivenNoHpacExtensionOnAGroupField_WhenMapped_ThenItIsNotMarkedPrivate()
	{
		// Given — a real Typeform group/contact_info field, never exported by
		// TypeformExportBuilder, so it carries no hpac object.
		var english = Document(Field("group-ref", "Group", "statement"));
		var french = Document(Field("group-ref", "Groupe", "statement"));

		// When
		var draft = DraftFor(TypeformQuestionMapper.Map(english, french), "group-ref");

		// Then
		draft.IsPrivate.ShouldBeFalse();
	}

	[Fact]
	public void GivenAnHpacExtensionWithAnUnknownType_WhenMapped_ThenTheNativelyDerivedTypeIsKept()
	{
		// Given — defensive: an extension this system did not write, or from a
		// future version, names a type this version does not recognize.
		var hpac = new TypeformHpacExtension("some_future_type", false, false, null, null, null);
		var english = Document(Field("field-ref", "Field", "short_text", hpac));
		var french = Document(Field("field-ref", "Champ", "short_text", hpac));

		// When
		var draft = DraftFor(TypeformQuestionMapper.Map(english, french), "field-ref");

		// Then
		draft.Type.ShouldBe(QuestionType.ShortText);
	}

	[Fact]
	public void GivenALiveQuestionBank_WhenExportedAndReimported_ThenTheDraftsMatchTheOriginalQuestions()
	{
		// Given
		var at = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);
		var group = Question.Create("aircraft", QuestionType.Group, "Aircraft:", "Aéronef:", at, isPrivate: false);
		var parent = Question.Create("country", QuestionType.YesNo, "Country?", "Pays?", at, isPrivate: false);
		var child = Question.Create(
			"model", QuestionType.ShortText, "Model", "Modèle", at, isPrivate: true, isRequired: true,
			dependsOnQuestionId: parent.Id, groupedUnderQuestionId: group.Id);
		var multi = Question.Create(
			"ratings", QuestionType.MultiSelect, "Ratings", "Qualifications", at, isPrivate: false,
			options: [new QuestionOptionInput("p1", "P1", "P1")]);

		// When
		var (english, french) = TypeformExportBuilder.Build([group, parent, child, multi]);
		var result = TypeformQuestionMapper.Map(english, french);

		// Then
		var groupDraft = DraftFor(result, "aircraft");
		groupDraft.Type.ShouldBe(QuestionType.Group);
		groupDraft.LabelEn.ShouldBe("Aircraft:");
		groupDraft.LabelFr.ShouldBe("Aéronef:");

		var childDraft = DraftFor(result, "model");
		childDraft.Type.ShouldBe(QuestionType.ShortText);
		childDraft.IsPrivate.ShouldBeTrue();
		childDraft.IsRequired.ShouldBeTrue();
		childDraft.DependsOnKey.ShouldBe("country");
		childDraft.GroupedUnderKey.ShouldBe("aircraft");

		var multiDraft = DraftFor(result, "ratings");
		multiDraft.Type.ShouldBe(QuestionType.MultiSelect);
		multiDraft.Options.Single().Code.ShouldBe("p1");
	}
}
