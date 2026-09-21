using System.Reflection;

using Reqnroll;
using Shouldly;

namespace HpacSafety.Acceptance.Tests;

/// <summary>
/// Guards the <c>@ui</c> skip itself.
/// </summary>
/// <remarks>
/// CI still runs this suite with <c>--filter "Category!=ui"</c>, so a removed
/// or mis-scoped hook would go unnoticed there and only surface as a wall of
/// pending-step failures on a developer's machine — exactly the state ADR-0073
/// was written to end. This asserts the hook is present and scoped to the tag,
/// which the category filter cannot hide.
/// </remarks>
public sealed class UiScenarioHooksTests
{
    [Fact]
    public void GivenAcceptanceSuite_WhenHooksAreInspected_ThenABeforeScenarioHookIsScopedToTheUiTag()
    {
        BeforeScenarioAttribute? hook = typeof(UiScenarioHooks)
            .GetMethods(BindingFlags.Public | BindingFlags.Static | BindingFlags.Instance | BindingFlags.DeclaredOnly)
            .Select(method => method.GetCustomAttribute<BeforeScenarioAttribute>())
            .FirstOrDefault(attribute => attribute is not null);

        hook.ShouldNotBeNull();
        hook.Tags.ShouldBe(["ui"]);
    }
}
