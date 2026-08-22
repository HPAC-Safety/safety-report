using HpacSafety.Core.Features.Anonymization;
using Shouldly;

namespace HpacSafety.Anonymization.Tests;

/// <summary>
/// The scrub is only provable in a plain unit test for as long as
/// <c>HpacSafety.Core</c> depends on nothing. This pins that down so the day
/// somebody adds a dependency to Core is the day a test goes red, rather than
/// the day the golden-file suite quietly starts needing a database.
/// </summary>
public class CoreDependencyTests
{
    [Fact]
    public void Given_the_core_project_file_When_it_is_read_Then_it_declares_no_references_of_its_own()
    {
        // Given
        var project = Path.Combine(RepositoryRoot(), "src", "HpacSafety.Core", "HpacSafety.Core.csproj");

        // When
        var contents = File.ReadAllText(project);

        // Then
        contents.ShouldNotContain("PackageReference");
        contents.ShouldNotContain("ProjectReference");
    }

    [Fact]
    public void Given_the_core_assembly_When_its_references_are_read_Then_nothing_outside_the_framework_is_referenced()
    {
        // Given — the project file is only half the story. `Directory.Build.props`
        // applies to every project, and an analyzer added there is invisible to
        // the test above. This one reads what Core actually compiled against, so
        // an EF Core or an SDK reference arriving by any route fails it.
        var core = typeof(DeterministicScrub).Assembly;

        // When
        var referenced = core.GetReferencedAssemblies()
            .Select(name => name.Name ?? string.Empty)
            .ToList();

        // Then
        referenced.ShouldNotBeEmpty();
        referenced.ShouldAllBe(name => name.StartsWith("System.", StringComparison.Ordinal)
            || name == "System"
            || name == "netstandard"
            || name == "mscorlib");
    }

    private static string RepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);

        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "HpacSafety.slnx")))
        {
            directory = directory.Parent;
        }

        directory.ShouldNotBeNull("The repository root was not found above the test output directory.");
        return directory.FullName;
    }
}
