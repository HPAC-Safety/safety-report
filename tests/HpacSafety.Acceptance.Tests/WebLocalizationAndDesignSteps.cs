using System.Diagnostics;
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

#pragma warning restore CA1822

    private static void RunNodeTool(string relativeScriptPath)
    {
        var repositoryRoot = RepositoryRoot();

        using var process = new Process
        {
            StartInfo = new ProcessStartInfo("node", relativeScriptPath)
            {
                WorkingDirectory = repositoryRoot,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
            },
        };

        process.Start();
        var output = process.StandardOutput.ReadToEnd();
        var error = process.StandardError.ReadToEnd();
        process.WaitForExit();

        process.ExitCode.ShouldBe(0, $"{relativeScriptPath} failed:\n{output}\n{error}");
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
