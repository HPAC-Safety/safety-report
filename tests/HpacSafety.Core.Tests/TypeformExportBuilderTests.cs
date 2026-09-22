using HpacSafety.Core;
using HpacSafety.Core.Features.QuestionBank;
using HpacSafety.Core.Features.QuestionBank.Typeform;
using Shouldly;

namespace HpacSafety.Core.Tests;

/// <summary>
///     Turning the live question bank into an English/French Typeform-shaped
///     pair — the reverse of <see cref="TypeformQuestionMapper" /> — ADR-0077.
/// </summary>
public class TypeformExportBuilderTests
{
	private static readonly DateTimeOffset At = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);
	private static readonly Dictionary<TinyId, OptionSet> NoSets = [];

	[Fact]
	public void GivenLiveQuestions_WhenBuilt_ThenEachProducesAnEnglishAndFrenchField()
	{
		// Given
		var group = Question.Create("aircraft", QuestionType.Group, "Aircraft:", "Aéronef:", At, isPrivate: false);
		var child = Question.Create(
			"model", QuestionType.ShortText, "Model", "Modèle", At,
			groupedUnderQuestionId: group.Id, isPrivate: false);

		// When
		var (english, french) = TypeformExportBuilder.Build([group, child], NoSets);

		// Then
		english.Fields.Select(field => field.Ref).ShouldBe(["aircraft", "model"]);
		french.Fields.Select(field => field.Ref).ShouldBe(["aircraft", "model"]);
		english.Fields[1].Title.ShouldBe("Model");
		french.Fields[1].Title.ShouldBe("Modèle");
	}

	[Fact]
	public void GivenAQuestionGroupedUnderAnother_WhenBuilt_ThenTheHpacExtensionNamesTheParentByKey()
	{
		// Given
		var group = Question.Create("aircraft", QuestionType.Group, "Aircraft:", "Aéronef:", At, isPrivate: false);
		var child = Question.Create(
			"model", QuestionType.ShortText, "Model", "Modèle", At,
			groupedUnderQuestionId: group.Id, isPrivate: false);

		// When
		var (english, _) = TypeformExportBuilder.Build([group, child], NoSets);

		// Then
		var childField = english.Fields.Single(field => field.Ref == "model");
		childField.Properties.Hpac.ShouldNotBeNull();
		childField.Properties.Hpac!.GroupedUnderKey.ShouldBe("aircraft");
	}

	[Fact]
	public void GivenAConditionalQuestion_WhenBuilt_ThenTheHpacExtensionNamesTheParentByKeyAndOptionCode()
	{
		// Given
		var parent = Question.Create(
			"aircraft_type", QuestionType.SingleSelect, "Aircraft type", "Type d'aéronef", At, isPrivate: false,
			isActive: true,
			options: [new QuestionOptionInput("glider", "Hang glider", "Deltaplane", null)]);
		var child = Question.Create(
			"glider_model", QuestionType.ShortText, "Model", "Modèle", At, isPrivate: false,
			dependsOnQuestionId: parent.Id, dependsOnOptionCode: "glider");

		// When
		var (english, _) = TypeformExportBuilder.Build([parent, child], NoSets);

		// Then
		var childField = english.Fields.Single(field => field.Ref == "glider_model");
		childField.Properties.Hpac!.DependsOnKey.ShouldBe("aircraft_type");
		childField.Properties.Hpac.DependsOnOptionCode.ShouldBe("glider");
	}

	[Fact]
	public void GivenAPrivateRequiredQuestion_WhenBuilt_ThenTheHpacExtensionCarriesBoth()
	{
		// Given
		var question = Question.Create(
			"injury", QuestionType.ShortText, "Injury", "Blessure", At, isPrivate: true, isRequired: true);

		// When
		var (english, _) = TypeformExportBuilder.Build([question], NoSets);

		// Then
		var hpac = english.Fields.Single().Properties.Hpac!;
		hpac.IsPrivate.ShouldBeTrue();
		hpac.IsRequired.ShouldBeTrue();
	}

	[Fact]
	public void GivenAMultiSelectQuestion_WhenBuilt_ThenItExportsAsMultipleChoiceWithBothLanguagesOfEachOption()
	{
		// Given
		var question = Question.Create(
			"ratings", QuestionType.MultiSelect, "Ratings", "Qualifications", At, isPrivate: false,
			allowsReporterAdditions: true,
			options:
			[
				new QuestionOptionInput("p1", "P1", "P1", null),
				new QuestionOptionInput("p2", "P2", "P2", null),
			]);

		// When
		var (english, french) = TypeformExportBuilder.Build([question], NoSets);

		// Then
		var englishField = english.Fields.Single();
		englishField.Type.ShouldBe("multiple_choice");
		englishField.Properties.AllowMultipleSelection.ShouldBe(true);
		englishField.Properties.AllowOtherChoice.ShouldBe(true);
		englishField.Properties.Choices!.Select(choice => choice.Ref).ShouldBe(["p1", "p2"]);
		french.Fields.Single().Properties.Choices!.Select(choice => choice.Label).ShouldBe(["P1", "P2"]);
	}

	[Theory]
	[InlineData(QuestionType.ShortText, "short_text")]
	[InlineData(QuestionType.LongText, "long_text")]
	[InlineData(QuestionType.Email, "email")]
	[InlineData(QuestionType.Phone, "phone_number")]
	[InlineData(QuestionType.Date, "date")]
	[InlineData(QuestionType.Number, "number")]
	[InlineData(QuestionType.SingleSelect, "multiple_choice")]
	[InlineData(QuestionType.YesNo, "yes_no")]
	[InlineData(QuestionType.FileUpload, "file_upload")]
	[InlineData(QuestionType.Autocomplete, "multiple_choice")]
	[InlineData(QuestionType.Statement, "statement")]
	[InlineData(QuestionType.Group, "statement")]
	[InlineData(QuestionType.Checkbox, "short_text")]
	[InlineData(QuestionType.Time, "short_text")]
	public void GivenEveryQuestionType_WhenBuilt_ThenItExportsAsTheClosestNativeTypeWithHpacCarryingTheReal(
		QuestionType type, string nativeType)
	{
		// Given
		var question = Question.Create("field", type, "Field", "Champ", At, isPrivate: false);

		// When
		var (english, _) = TypeformExportBuilder.Build([question], NoSets);

		// Then
		var field = english.Fields.Single();
		field.Type.ShouldBe(nativeType);
		field.Properties.Hpac!.Type.ShouldBe(EnumCode.Of(type));
	}

	[Fact]
	public void GivenAMultiSelectThatDoesNotAllowReporterAdditions_WhenBuilt_ThenAllowOtherChoiceIsAbsent()
	{
		// Given
		var question = Question.Create(
			"ratings", QuestionType.MultiSelect, "Ratings", "Qualifications", At, isPrivate: false,
			allowsReporterAdditions: false,
			options: [new QuestionOptionInput("p1", "P1", "P1", null)]);

		// When
		var (english, _) = TypeformExportBuilder.Build([question], NoSets);

		// Then
		english.Fields.Single().Properties.AllowOtherChoice.ShouldBeNull();
	}

	[Fact]
	public void GivenAQuestionBackedByALiveOptionSet_WhenBuilt_ThenTheLiveChoicesAreExported()
	{
		// Given
		var set = OptionSet.Create("aerodromes", "Aerodromes", "Aérodromes", At);
		set.Add("alberta", "Alberta", "Alberta");
		var question = Question.Create(
			"aerodrome", QuestionType.Autocomplete, "Aerodrome", "Aérodrome", At, isPrivate: false,
			optionSetId: set.Id);

		// When
		var (english, _) = TypeformExportBuilder.Build([question], new Dictionary<TinyId, OptionSet> { [set.Id] = set });

		// Then
		english.Fields.Single().Properties.Choices!.Select(choice => choice.Ref).ShouldBe(["alberta"]);
	}

	[Fact]
	public void GivenAQuestionNamingAnOptionSetThatIsNoLongerLive_WhenBuilt_ThenItsOwnSnapshotIsExported()
	{
		// Given
		var question = Question.Create(
			"aerodrome", QuestionType.Autocomplete, "Aerodrome", "Aérodrome", At, isPrivate: false,
			optionSetId: TinyId.New(),
			options: [new QuestionOptionInput("alberta", "Alberta", "Alberta", null)]);

		// When
		var (english, _) = TypeformExportBuilder.Build([question], NoSets);

		// Then
		english.Fields.Single().Properties.Choices!.Select(choice => choice.Ref).ShouldBe(["alberta"]);
	}

	[Fact]
	public void GivenADependencyOnAQuestionNotAmongThoseExported_WhenBuilt_ThenTheHpacExtensionLeavesItUnnamed()
	{
		// Given — the parent exists in the database but was not included in
		// the list passed to Build, the same way a caller could pass a
		// partial list.
		var child = Question.Create(
			"child", QuestionType.ShortText, "Child", "Enfant", At, isPrivate: false,
			dependsOnQuestionId: TinyId.New());

		// When
		var (english, _) = TypeformExportBuilder.Build([child], NoSets);

		// Then
		english.Fields.Single().Properties.Hpac!.DependsOnKey.ShouldBeNull();
	}

	[Fact]
	public void GivenAnUnknownQuestionType_WhenBuilt_ThenRefuses()
	{
		// Given
		var question = Question.Create("field", (QuestionType)999, "Field", "Champ", At, isPrivate: false);

		// When / Then
		Should.Throw<ArgumentOutOfRangeException>(() => TypeformExportBuilder.Build([question], NoSets));
	}

	[Fact]
	public void GivenAGroupOrStatementQuestion_WhenBuilt_ThenTheNativeTypeIsAPlainStatement()
	{
		// Given
		var statement = Question.Create("intro", QuestionType.Statement, "Welcome", "Bienvenue", At, isPrivate: false);
		var group = Question.Create("aircraft", QuestionType.Group, "Aircraft:", "Aéronef:", At, isPrivate: false);

		// When
		var (english, _) = TypeformExportBuilder.Build([statement, group], NoSets);

		// Then
		english.Fields.ShouldAllBe(field => field.Type == "statement");
		english.Fields[0].Properties.Hpac!.Type.ShouldBe("statement");
		english.Fields[1].Properties.Hpac!.Type.ShouldBe("group");
	}

	[Fact]
	public void GivenTheExportedDocument_WhenSerializedAndParsedBack_ThenTheHpacExtensionSurvives()
	{
		// Given
		var question = Question.Create(
			"model", QuestionType.ShortText, "Model", "Modèle", At, isPrivate: true, isRequired: true);
		var (english, _) = TypeformExportBuilder.Build([question], NoSets);

		// When
		var json = english.ToJson();
		var parsed = TypeformDocument.Parse(json);

		// Then
		parsed.Fields.Single().Properties.Hpac.ShouldNotBeNull();
		parsed.Fields.Single().Properties.Hpac!.IsPrivate.ShouldBeTrue();
	}

	[Fact]
	public void GivenTheExportedDocument_WhenSerialized_ThenItValidatesAsAPlainTypeformFileWithoutTheExtension()
	{
		// Given
		var question = Question.Create(
			"model", QuestionType.ShortText, "Model", "Modèle", At, isPrivate: true,
			groupedUnderQuestionId: null);
		var (english, _) = TypeformExportBuilder.Build([question], NoSets);

		// When
		var json = english.ToJson();

		// Then — a plain Typeform importer, unaware of the hpac extension, would
		// still parse fields, refs, titles, and types from this file.
		using var document = System.Text.Json.JsonDocument.Parse(json);
		var field = document.RootElement.GetProperty("fields")[0];
		field.GetProperty("ref").GetString().ShouldBe("model");
		field.GetProperty("title").GetString().ShouldBe("Model");
		field.GetProperty("type").GetString().ShouldBe("short_text");
	}
}
