using System.Reflection;
using HpacSafety.Core.Features.Anonymization;
using Shouldly;

namespace HpacSafety.Anonymization.Tests;

/// <summary>
/// The scrub is only provable in a plain unit test for as long as
/// <c>HpacSafety.Core</c> depends on nothing. This pins that down so the day
/// somebody adds a package to Core is the day a test goes red, rather than the
/// day the golden-file suite quietly starts needing a database.
/// </summary>
public class CoreDependencyTests
{
    [Fact]
    public void Given_the_core_project_When_its_references_are_read_Then_it_has_no_package_references()
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
    public void Given_the_core_assembly_When_its_references_are_read_Then_only_the_framework_is_referenced()
    {
        // Given
        var core = typeof(DeterministicScrub).Assembly;

        // When
        var referenced = core.GetReferencedAssemblies().Select(name => name.Name ?? string.Empty).ToList();

        // Then
        referenced.ShouldAllBe(name => name.StartsWith("System", StringComparison.Ordinal)
            || name.StartsWith("Microsoft.", StringComparison.Ordinal)
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
