using Shouldly;

namespace HpacSafety.Core.Tests;

/// <summary>
/// Seed test establishing the conventions every test in this repository follows:
/// Shouldly assertions, and Given/When/Then in both the name and the body.
/// See docs/testing-conventions.md.
/// </summary>
public class ConventionsTests
{
    [Fact]
    public void GivenSolutionScaffold_WhenTestSuiteRuns_ThenExecutes()
    {
        // Given
        var scaffolded = true;

        // When
        var result = scaffolded;

        // Then
        result.ShouldBeTrue();
    }
}
