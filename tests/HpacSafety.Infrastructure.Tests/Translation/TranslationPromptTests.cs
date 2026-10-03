using HpacSafety.Core;
using HpacSafety.Infrastructure.Translation;
using Shouldly;

namespace HpacSafety.Infrastructure.Tests.Translation;

/// <summary>
///     The translation prompt and term list, the files <c>tools/i18n/translator.ts</c>
///     reads too (ADR-0179). These tests read the repository's committed copies
///     and compare them with what the assembly embedded.
/// </summary>
public class TranslationPromptTests
{
	[Fact]
	public void GivenCommittedPromptFile_WhenRendered_ThenEmbeddedCopyIsTheCommittedFile()
	{
		// Given
		var committed = File.ReadAllText(Path.Combine(LocalesDirectory(), TranslationPrompt.CurrentFileName));

		// When
		var rendered = TranslationPrompt.Render(Locale.EnCa, Locale.FrCa);

		// Then — every slot is filled and nothing else was changed: the prose
		// before the first slot is byte for byte the committed file's
		var head = committed[..committed.IndexOf("[[source_language]]", StringComparison.Ordinal)];
		rendered.ShouldStartWith(head);
		rendered.ShouldNotContain("[[");
	}

	[Fact]
	public void GivenTermsSentence_WhenBuilt_ThenItIsExactlyWhatTheJavaScriptToolsBuild()
	{
		// Given
		var terms = """{"_comment":"ignored","upload":{"fr-CA":"téléverser","forbidden":["télécharg"]}}""";

		// When
		var sentences = TranslationPrompt.TermInstructions(terms);

		// Then — the same literal tests/js/i18n/translate-locale.test.ts asserts
		sentences.ShouldBe([
			"\"upload\" (English, in any form) is \"téléverser\" in French, conjugated or as a noun to fit. Never \"télécharg…\" in French.",
		]);
	}

	[Fact]
	public void GivenSeveralForbiddenForms_WhenBuilt_ThenEveryOneIsNamed()
	{
		// Given
		var terms = """{"report":{"fr-CA":"signalement","forbidden":["rapport","article"]}}""";

		// When
		var sentence = TranslationPrompt.TermInstructions(terms).Single();

		// Then
		sentence.ShouldEndWith("Never \"rapport…\" or \"article…\" in French.");
	}

	[Theory]
	[InlineData("""{"upload":{"forbidden":["x"]}}""")]
	[InlineData("""{"upload":{"fr-CA":"téléverser"}}""")]
	[InlineData("""{"upload":{"fr-CA":"téléverser","forbidden":[]}}""")]
	public void GivenEntryThatCannotBeChecked_WhenBuilt_ThenRefused(string terms)
	{
		// Given / When / Then — a term nobody can check protects nobody
		Should.Throw<InvalidOperationException>(() => TranslationPrompt.TermInstructions(terms));
	}

	[Fact]
	public void GivenNoTerms_WhenRendered_ThenPromptSaysNoneRatherThanLeavingAHole()
	{
		// Given / When
		var rendered = TranslationPrompt.Render("Terms:\n[[terms]]", Locale.EnCa, Locale.FrCa, []);

		// Then
		rendered.ShouldBe("Terms:\n- (none)");
	}

	private static string LocalesDirectory()
	{
		var directory = new DirectoryInfo(AppContext.BaseDirectory);

		while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "locales", "terms.json")))
		{
			directory = directory.Parent;
		}

		return Path.Combine(
			directory?.FullName ?? throw new DirectoryNotFoundException("Could not find locales/."),
			"locales");
	}
}
