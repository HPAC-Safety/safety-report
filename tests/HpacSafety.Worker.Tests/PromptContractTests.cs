using System.Security.Cryptography;
using HpacSafety.Worker.Summarization;
using Shouldly;

namespace HpacSafety.Worker.Tests;

/// <summary>
///     The prompt the Worker ships is the implementation of every anonymization and
///     accuracy rule, so each rule is checked against the file itself (REQ-AI-024).
///     What the model then writes is judged by the reviewer, not by a test.
/// </summary>
public sealed class PromptContractTests
{
	/// <summary>Each rule REQ-AI-024 names, and the words the prompt must use to state it.</summary>
	public static TheoryData<string, string[]> Rules => new()
	{
		{ "accuracy", ["must be supported by `report_content`", "Do not invent, assume, or infer", "Never guess"] },
		{ "pilot", ["| the pilot | le pilote |"] },
		{ "other people", ["| the instructor | l'instructeur |", "| a witness | un témoin |", "| another pilot | un autre pilote |", "| the reporter | le déclarant |", "| a person | une personne |", "Do not invent a role"] },
		{ "places", ["| the launch site | le site de décollage |", "| the landing field | le champ d'atterrissage |", "| the location | le lieu |"] },
		{ "dates and times", ["An exact calendar date | its month and year, or its season", "A time of day | keep it exactly as reported"] },
		{ "organizations", ["| the club, the school, the company | le club, l'école, l'entreprise |"] },
		{ "aircraft", ["make or model | its category"] },
		{ "forbidden wording", ["an invented name", "\"redacted\"", "\"caviardé\"", "a placeholder", "Never comment on what was removed"] },
		{ "markers", ["`[PRIVATE:<question-key>]`", "Resolve every marker", "must never appear in a summary"] },
		{ "private context", ["Never take a fact from `private_context` and put it in a summary"] },
		{ "response", ["{\"ai_summary_en\":\"...\",\"ai_summary_fr\":\"...\"}", "no Markdown fence, no commentary, no additional key"] },
	};

	/// <summary>The current prompt as the Worker loads it from its output directory.</summary>
	internal static string CurrentPrompt()
	{
		return File.ReadAllText(PromptPath(PromptDrivenSummarizer.CurrentPromptFileName));
	}

	[Theory]
	[MemberData(nameof(Rules))]
	public void GivenCurrentPrompt_WhenRead_ThenStatesRule(string rule,
															string[] phrases)
	{
		// Given
		ArgumentNullException.ThrowIfNull(phrases);
		var prompt = Collapse(CurrentPrompt());

		// When / Then — compared with line wrapping collapsed to single spaces
		foreach (var phrase in phrases)
		{
			prompt.ShouldContain(Collapse(phrase), Case.Sensitive, $"the {rule} rule");
		}
	}

	[Fact]
	public void GivenCurrentPrompt_WhenNamed_ThenItIsVersionThree()
	{
		// Given / When / Then
		PromptDrivenSummarizer.CurrentPromptFileName.ShouldBe("summarize-anonymize.v3.md");
		PromptDrivenSummarizer.CurrentPromptVersion.ShouldBe("summarize-anonymize.v3");
	}

	[Fact]
	public void GivenCurrentPromptExample_WhenRead_ThenItsSampleNamesAppearOnlyInTheInput()
	{
		// Given — the worked example's output must itself obey the rules it teaches
		var prompt = CurrentPrompt();
		var output = prompt[prompt.IndexOf("Output:", StringComparison.Ordinal)..prompt.IndexOf("Note what the example did", StringComparison.Ordinal)];

		// When / Then
		foreach (var identifying in new[] { "Jonathan", "Reyes", "Jo ", "Tamsin", "Pellburg", "Hendry", "Aurora", "555-0142", "14 June", "2026-06-14", "[PRIVATE" })
		{
			output.ShouldNotContain(identifying);
		}
	}

	[Theory]
	[InlineData("summarize-anonymize.v1.md", "e752ef4da24622343de428c8de6223f38154cdf960a11360e8a037dfe6f7b47c")]
	[InlineData("summarize-anonymize.v2.md", "d1e817512eeb2ed40eb2e2e1f379f67c544d61fb3451b9ba3786a9bd95e08040")]
	public void GivenShippedPromptVersion_WhenHashed_ThenItIsUnchanged(string fileName,
																		string sha256)
	{
		// Given — a summary's prompt_version must keep naming the exact words sent
		var bytes = File.ReadAllBytes(PromptPath(fileName));

		// When
		var hash = Convert.ToHexStringLower(SHA256.HashData(bytes));

		// Then
		hash.ShouldBe(sha256, $"{fileName} is immutable; add a new version instead");
	}

	private static string Collapse(string text)
	{
		return string.Join(' ', text.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
	}

	private static string PromptPath(string fileName)
	{
		return Path.Combine(AppContext.BaseDirectory, "Prompts", fileName);
	}
}
