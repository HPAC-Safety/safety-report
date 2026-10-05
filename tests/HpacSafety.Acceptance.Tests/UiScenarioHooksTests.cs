using System.Reflection;
using System.Xml.Linq;
using Reqnroll;
using Shouldly;
using Xunit.Sdk;

namespace HpacSafety.Acceptance.Tests;

/// <summary>
///     Guards the <c>@ui</c> skip itself.
/// </summary>
/// <remarks>
///     Every default run filters <c>@ui</c> scenarios out (CONV-010): CI by its
///     <c>--filter</c>, a local or IDE run by the project's settings file. So a
///     removed or mis-scoped hook, the backstop for a run whose settings replace
///     that file, would show up nowhere. These assert the hook is present and
///     scoped to the tag, and that the settings file still filters.
/// </remarks>
public sealed class UiScenarioHooksTests
{
	[Fact]
	public void GivenAcceptanceSuite_WhenHooksAreInspected_ThenABeforeScenarioHookIsScopedToTheUiTag()
	{
		var hook = typeof(UiScenarioHooks)
			.GetMethods(BindingFlags.Public | BindingFlags.Static | BindingFlags.Instance | BindingFlags.DeclaredOnly)
			.Select(method => method.GetCustomAttribute<BeforeScenarioAttribute>())
			.FirstOrDefault(attribute => attribute is not null);

		hook.ShouldNotBeNull();
		hook.Tags.ShouldBe(["ui"]);
	}

	[Fact]
	public void GivenUiScenario_WhenHookRuns_ThenItThrowsTheSkipThatSurvivesTheMessageFormatter()
	{
		Should.Throw<SkipException>(UiScenarioHooks.SkipUiScenario);
	}

	/// <summary>
	///     CONV-010: the project's own settings file is what keeps a default local or
	///     IDE run from reporting every <c>@ui</c> scenario as skipped. CI passes its
	///     own <c>--settings</c> and never reads it, so only this test notices if the
	///     property or the filter is removed.
	/// </summary>
	[Fact]
	public void GivenAcceptanceProject_WhenItsDefaultSettingsAreRead_ThenTheyFilterOutUiScenarios()
	{
		var project = Path.Combine(RepositoryRoot(), "tests", "HpacSafety.Acceptance.Tests");

		var settingsPath = XDocument.Load(Path.Combine(project, "HpacSafety.Acceptance.Tests.csproj"))
			.Descendants("RunSettingsFilePath")
			.Select(element => element.Value)
			.SingleOrDefault();
		settingsPath.ShouldBe("$(MSBuildProjectDirectory)/acceptance.runsettings");

		XDocument.Load(Path.Combine(project, "acceptance.runsettings"))
			.Descendants("RunConfiguration")
			.Elements("TestCaseFilter")
			.Select(element => element.Value.Trim())
			.SingleOrDefault()
			.ShouldBe("Category!=ui");
	}

	private static string RepositoryRoot()
	{
		var directory = new DirectoryInfo(AppContext.BaseDirectory);

		while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "AGENTS.md")))
		{
			directory = directory.Parent;
		}

		return directory?.FullName ?? throw new DirectoryNotFoundException("Could not find the repository root.");
	}
}
