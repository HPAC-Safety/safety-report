using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Reqnroll;
using Shouldly;

namespace HpacSafety.Acceptance.Tests;

// Only the non-@ui scenarios in web-localization-and-design.feature execute
// here. @ui scenarios execute via playwright-bdd in tests/e2e instead — see
// ADR-0053. This scenario asserts a source/build-time structural invariant
// (no literal user-facing strings, locale key parity), which the repository
// already has purpose-built Node tools for; running them is the check,
// rather than reimplementing their logic in C#.
[Binding]
public sealed class WebLocalizationAndDesignSteps
{
	private static void RunNodeTool(string relativeScriptPath)
	{
		RunNodeTool(relativeScriptPath, true);
	}

	private static int RunNodeTool(string relativeScriptPath,
								   bool expectSuccess,
								   params string[] args)
	{
		return RunNodeTool(relativeScriptPath, expectSuccess, out _, args);
	}

	private static int RunNodeTool(string relativeScriptPath,
								   bool expectSuccess,
								   out string combinedOutput,
								   params string[] args)
	{
		var repositoryRoot = RepositoryRoot();

		using var process = new Process
		{
			StartInfo = new ProcessStartInfo("node")
			{
				WorkingDirectory = repositoryRoot,
				RedirectStandardOutput = true,
				RedirectStandardError = true,
			},
		};
		process.StartInfo.ArgumentList.Add(relativeScriptPath);
		foreach (var arg in args)
		{
			process.StartInfo.ArgumentList.Add(arg);
		}

		process.Start();
		var output = process.StandardOutput.ReadToEnd();
		var error = process.StandardError.ReadToEnd();
		process.WaitForExit();

		combinedOutput = $"{output}\n{error}";

		if (expectSuccess)
		{
			process.ExitCode.ShouldBe(0, $"{relativeScriptPath} failed:\n{output}\n{error}");
		}

		return process.ExitCode;
	}

	private static string RepositoryRoot()
	{
		var directory = new DirectoryInfo(AppContext.BaseDirectory);

		while (directory is not null
			   && !File.Exists(Path.Combine(directory.FullName, "HpacSafety.slnx")))
		{
			directory = directory.Parent;
		}

		return directory?.FullName ?? throw new InvalidOperationException("Could not locate the repository root.");
	}
#pragma warning disable CA1822 // Reqnroll step bindings must be instance methods to be discovered.

	[Given(@"the UI renders chrome or a stable validation\/error message")]
	public void GivenTheUiRendersChromeOrAStableMessage()
	{
		// Contextual only — the actual invariant is asserted below by
		// running the tools that scan every UI source file.
	}

	[When(@"the string is displayed")]
	public void WhenTheStringIsDisplayed()
	{
		// Contextual only, see above.
	}

	[Then(@"it comes from a committed locale catalogue with key parity between en-CA and fr-CA")]
	public void ThenLocaleKeyParity()
	{
		RunNodeTool("tools/check-locales.mjs");
	}

	[Then(@"no user-facing literal appears directly in code")]
	public void ThenNoHardcodedStrings()
	{
		RunNodeTool("tools/check-hardcoded-strings.mjs");
	}

	private string _localesDir = string.Empty;

	[Given(@"a key exists in en-CA\.json but not in fr-CA\.json, or in fr-CA\.json but not in en-CA\.json")]
	public void GivenEachFileHasAKeyTheOtherLacks()
	{
		_localesDir = Path.Combine(Path.GetTempPath(), $"locales-stub-{Guid.NewGuid():N}");
		Directory.CreateDirectory(_localesDir);

		// One gap in each direction, so the assertion below cannot pass by
		// only ever filling French.
		File.WriteAllText(
			Path.Combine(_localesDir, "en-CA.json"),
			JsonSerializer.Serialize(new { nav = new { contact = "Contact" } }));
		File.WriteAllText(
			Path.Combine(_localesDir, "fr-CA.json"),
			JsonSerializer.Serialize(new { nav = new { aide = "Aide" } }));
	}

	[When(@"the local build runs, or a commit is made that stages a locales\/ file")]
	public void WhenTheStubberRuns()
	{
		RunNodeTool("tools/stub-missing-translations.mjs", true, "--locales", _localesDir);

		// The commit half of that sentence. Running git here would prove
		// little that the hook's own three verified cases do not already
		// cover; what matters to this scenario is that the hook reaches the
		// same tool the local build does, before it checks parity.
		//
		// Comment lines are dropped first. Both tool names appear in the
		// hook's header prose, so searching the whole file would find them
		// there and pass whatever the code below it actually does.
		var hookLines = File
			.ReadAllLines(Path.Combine(RepositoryRoot(), ".githooks", "pre-commit"))
			.Where(line => !line.TrimStart().StartsWith('#'))
			.ToList();

		var stubAt = hookLines.FindIndex(line => line.Contains("stub-missing-translations.mjs", StringComparison.Ordinal));
		var checkAt = hookLines.FindIndex(line => line.Contains("check-locales.mjs", StringComparison.Ordinal));

		stubAt.ShouldBeGreaterThan(-1, "the pre-commit hook no longer runs the stubber");
		checkAt.ShouldBeGreaterThan(stubAt, "the hook checks parity before stubbing, so adding a key still blocks a commit");
	}

	[Then(@"the file missing that key gains it, with the other file's text prefixed with a # marker")]
	public void ThenEachFileGainsTheOthersMissingKey()
	{
		using var french = JsonDocument.Parse(File.ReadAllText(Path.Combine(_localesDir, "fr-CA.json")));
		using var english = JsonDocument.Parse(File.ReadAllText(Path.Combine(_localesDir, "en-CA.json")));

		french.RootElement.GetProperty("nav").GetProperty("contact").GetString().ShouldBe("#Contact");
		english.RootElement.GetProperty("nav").GetProperty("aide").GetString().ShouldBe("#Aide");
	}

	[Then(@"a key still carrying that # marker fails locale verification, so it can never reach main untranslated")]
	public void ThenAStubbedKeyFailsVerification()
	{
		var exitCode = RunNodeTool("tools/translate-locale.mjs", false, "--check", "--locales", _localesDir);
		exitCode.ShouldNotBe(0);
	}

	// --- a hand-edited French value (ADR-0070) ---------------------------

	private string _correctionDir = string.Empty;
	private int _verifyExitCode;
	private string _verifyOutput = string.Empty;

	/// <summary>
	///     A locale set as a real generate would leave it: both hashes stamped, so
	///     a later edit to either side is visible.
	/// </summary>
	private void WriteCorrectionFixture(string english,
										string french,
										string stampedEnglish,
										string stampedFrench)
	{
		_correctionDir = Path.Combine(Path.GetTempPath(), $"locales-correction-{Guid.NewGuid():N}");
		Directory.CreateDirectory(_correctionDir);

		File.WriteAllText(
			Path.Combine(_correctionDir, "en-CA.json"),
			JsonSerializer.Serialize(new { nav = new { contact = english } }));
		File.WriteAllText(
			Path.Combine(_correctionDir, "fr-CA.json"),
			JsonSerializer.Serialize(new { nav = new { contact = french } }));
		File.WriteAllText(
			Path.Combine(_correctionDir, "fr-CA.meta.json"),
			JsonSerializer.Serialize(new Dictionary<string, object>
			{
				["nav.contact"] = new
				{
					source_hash = Sha256(stampedEnglish),
					target_hash = Sha256(stampedFrench),
					provider = "deepl:FR-CA:prefer_more",
					reviewed = false,
				},
			}));
	}

	[Given(@"a French value is edited by hand and its English is unchanged")]
	public void GivenAFrenchValueEditedByHand()
	{
		// Stamped against "Nous joindre"; the file now says something else.
		WriteCorrectionFixture(
			"Contact us", "Joignez-nous",
			"Contact us", "Nous joindre");
	}

	[Given(@"a key is edited in both en-CA\.json and fr-CA\.json")]
	public void GivenBothLanguagesEdited()
	{
		WriteCorrectionFixture(
			"Get in touch", "Joignez-nous",
			"Contact us", "Nous joindre");
	}

	[When(@"the locales are verified")]
	public void WhenTheLocalesAreVerified()
	{
		_verifyExitCode = RunNodeTool(
			"tools/translate-locale.mjs",
			false,
			out _verifyOutput,
			"--check",
			"--locales",
			_correctionDir,
			"--allow-pending-translation");
	}

	[Then(@"the edit is accepted as a human correction")]
	public void ThenAcceptedAsACorrection()
	{
		// Accepted on a branch, and named rather than absorbed in silence.
		_verifyExitCode.ShouldBe(0);
		_verifyOutput.ShouldContain("was edited by hand");
	}

	[Then(@"verification says it will be recorded and never machine-translated again")]
	public void ThenVerificationSaysItWillBeRecorded()
	{
		// What the acceptance layer can honestly observe is what the check
		// tells the author. That the plan then skips it, and that applyPlan
		// re-stamps the provenance, are properties of those functions and are
		// asserted directly in tests/js/translate-locale.test.mjs.
		_verifyOutput.ShouldContain("never machine-translated again");
	}

	[Then(@"neither language is overwritten")]
	public void ThenNeitherLanguageIsOverwritten()
	{
		var english = File.ReadAllText(Path.Combine(_correctionDir, "en-CA.json"));
		var french = File.ReadAllText(Path.Combine(_correctionDir, "fr-CA.json"));

		english.ShouldContain("Get in touch");
		french.ShouldContain("Joignez-nous");
	}

	private static string Sha256(string value)
	{
		return Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(value)));
	}

#pragma warning restore CA1822
}
