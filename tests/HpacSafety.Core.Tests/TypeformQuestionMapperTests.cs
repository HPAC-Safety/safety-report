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

	private static ImportedQuestionDraft DraftFor(TypeformImportResult result, string ref_)
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
	public void GivenTypeformType_WhenMapped_ThenQuestionTypeMatches(string fieldRef, QuestionType expected)
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
	public void GivenMultiSelectWithoutOtherChoice_WhenMapped_ThenReporterAdditionsAreClosed()
	{
		// Given / When
		var result = MapSynthetic();

		// Then
		DraftFor(result, "multi-ref").AllowsReporterAdditions.ShouldBeFalse();
	}

	[Fact]
	public void GivenMultiSelectWithOtherChoice_WhenMapped_ThenReporterAdditionsAreEnabled()
	{
		// Given / When — Typeform's allow_other_choice maps to the ADR-0063
		// amendment (ADR-0077), reusing the same reporter-addition mechanism
		// a type-ahead already has.
		var result = MapSynthetic();

		// Then
		DraftFor(result, "multiother-ref").AllowsReporterAdditions.ShouldBeTrue();
	}

	[Fact]
	public void GivenSingleSelectMultipleChoice_WhenMapped_ThenReporterAdditionsAreNeverEnabled()
	{
		// Given / When — out of ADR-0077's scope; nothing currently needs it.
		var result = MapSynthetic();

		// Then
		DraftFor(result, "dropdown-ref").AllowsReporterAdditions.ShouldBeFalse();
		DraftFor(result, "single-ref").AllowsReporterAdditions.ShouldBeFalse();
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
}
