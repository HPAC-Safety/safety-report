using Reqnroll;
using Xunit.v3;

namespace HpacSafety.Acceptance.Tests;

/// <summary>
///     Skips a <c>@ui</c> scenario, which this suite is never meant to execute.
/// </summary>
/// <remarks>
///     A <c>@ui</c> scenario asserts browser-observable behavior and executes
///     through <c>playwright-bdd</c> in <c>tests/e2e</c> — ADR-0053. The feature
///     files are shared, so Reqnroll's generator still emits a
///     <c>[Fact]</c> for every scenario in them, including the ones whose
///     step definitions are TypeScript. Without this hook such a scenario runs here
///     and fails for want of a C# binding it is never meant to have, which is what
///     a plain <c>dotnet test</c> used to report. Skipping is the mechanism; the
///     category filter in CI is a second line of defence — ADR-0073.
///     <para>
///     The hook throws an exception whose message starts with xUnit v3's
///     <see cref="DynamicSkipToken.Value" />, the contract for a dynamic skip, which
///     the generated <c>[Fact]</c> reports as skipped. It does not call
///     <c>Assert.Skip</c>, which does the same but is banned with the rest of
///     <c>Xunit.Assert</c> (ADR-0013). <c>ITestRunner.SkipScenarioAsync</c>
///     from a hook does too, but with Reqnroll's Cucumber Messages formatter on
///     (<c>REQNROLL_FORMATTERS</c>, which CI sets for the claim gate, ADR-0195) it
///     fails the test with "Stack empty" instead of skipping it.
///     </para>
/// </remarks>
[Binding]
public sealed class UiScenarioHooks
{
	[BeforeScenario("ui")]
	public static void SkipUiScenario()
	{
		throw new InvalidOperationException(
			DynamicSkipToken.Value + "A @ui scenario executes through playwright-bdd, not Reqnroll (ADR-0053).");
	}
}
