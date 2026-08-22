using HpacSafety.Core.Features.Moderation;
using HpacSafety.Core.Features.Outbox;
using HpacSafety.Core.Features.QuestionBank;
using HpacSafety.Core.Features.Reporting;
using HpacSafety.Core.SharedKernel;
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
    public void Given_the_solution_scaffold_When_the_test_suite_runs_Then_it_executes()
    {
        // Given
        var scaffolded = true;

        // When
        var result = scaffolded;

        // Then
        result.ShouldBeTrue();
    }
}
