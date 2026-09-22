using Reqnroll;

namespace HpacSafety.Acceptance.Tests;

/// <summary>
///     Skips a <c>@ui</c> scenario, which this suite is never meant to execute.
/// </summary>
/// <remarks>
///     A <c>@ui</c> scenario asserts browser-observable behavior and executes
///     through <c>playwright-bdd</c> in <c>tests/e2e</c> — ADR-0053. The feature
///     files are shared, so Reqnroll's generator still emits a
///     <c>[SkippableFact]</c> for every scenario in them, including the ones whose
///     step definitions are TypeScript. Without this hook such a scenario runs here
///     and fails for want of a C# binding it is never meant to have, which is what
///     a plain <c>dotnet test</c> used to report. Skipping is the mechanism; the
///     category filter in CI is a second line of defence — ADR-0073.
/// </remarks>
[Binding]
public sealed class UiScenarioHooks
{
    [BeforeScenario("ui")]
    public static async Task SkipUiScenarioAsync(ScenarioContext scenarioContext)
    {
        ArgumentNullException.ThrowIfNull(scenarioContext);

        await scenarioContext.ScenarioContainer.Resolve<ITestRunner>().SkipScenarioAsync();
    }
}
