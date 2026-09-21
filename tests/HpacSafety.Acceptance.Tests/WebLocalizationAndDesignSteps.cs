using System.Diagnostics;
using System.Linq;
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
        RunNodeTool("tools/stub-missing-translations.mjs", expectSuccess: true, "--locales", _localesDir);

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
        var exitCode = RunNodeTool("tools/translate-locale.mjs", expectSuccess: false, "--check", "--locales", _localesDir);
        exitCode.ShouldNotBe(0);
    }

#pragma warning restore CA1822

    private static void RunNodeTool(string relativeScriptPath) => RunNodeTool(relativeScriptPath, expectSuccess: true);

    private static int RunNodeTool(string relativeScriptPath, bool expectSuccess, params string[] args)
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

        if (expectSuccess)
        {
            process.ExitCode.ShouldBe(0, $"{relativeScriptPath} failed:\n{output}\n{error}");
        }

        return process.ExitCode;
    }

    private static string RepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);

        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "HpacSafety.slnx")))
        {
            directory = directory.Parent;
        }

        return directory?.FullName ?? throw new InvalidOperationException("Could not locate the repository root.");
    }
}
