using System.Diagnostics;
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

    [Given(@"a key exists in en-CA\.json but not in fr-CA\.json")]
    public void GivenAKeyExistsInEnglishButNotFrench()
    {
        _localesDir = Path.Combine(Path.GetTempPath(), $"locales-stub-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_localesDir);
        File.WriteAllText(
            Path.Combine(_localesDir, "en-CA.json"),
            JsonSerializer.Serialize(new { nav = new { contact = "Contact" } }));
    }

    [When(@"the local build runs")]
    public void WhenTheLocalBuildRuns()
    {
        RunNodeTool("tools/stub-missing-translations.mjs", expectSuccess: true, "--locales", _localesDir);
    }

    [Then(@"fr-CA\.json gains that key with its English text prefixed with a # marker")]
    public void ThenFrenchGainsAStubbedKey()
    {
        var french = File.ReadAllText(Path.Combine(_localesDir, "fr-CA.json"));
        using var document = JsonDocument.Parse(french);
        document.RootElement.GetProperty("nav").GetProperty("contact").GetString().ShouldBe("#Contact");
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
