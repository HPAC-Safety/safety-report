using HpacSafety.Core.Features.Reporting;
using HpacSafety.Core.SharedKernel;

using Shouldly;

namespace HpacSafety.Core.Tests;

public sealed class ReportRecordTests
{
    private static readonly DateTimeOffset Now = new(2026, 8, 22, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Given_a_second_summary_When_it_is_added_Then_it_is_refused()
    {
        // Given
        var report = new Report(Locale.EnCa, Now);
        report.AddSummary(Summary.Generated(report.Id, Locale.EnCa, "One", "model", "v4", Now));

        // When
        var adding = () => report.AddSummary(
            Summary.Generated(report.Id, Locale.EnCa, "Two", "model", "v4", Now));

        // Then
        adding.ShouldThrow<DomainRuleViolationException>();
    }

    [Fact]
    public void Given_a_summary_for_another_report_When_it_is_added_Then_it_is_refused()
    {
        var report = new Report(Locale.EnCa, Now);
        var summary = Summary.Generated(TinyId.New(), Locale.EnCa, "One", "model", "v4", Now);

        var adding = () => report.AddSummary(summary);

        adding.ShouldThrow<DomainRuleViolationException>();
    }

    [Fact]
    public void Given_an_approved_summary_When_it_is_edited_Then_approval_is_cleared()
    {
        // Given
        var summary = Summary.Generated(TinyId.New(), Locale.EnCa, "The pilot landed hard.", "model", "v4", Now);
        summary.Approve(TinyId.New(), Now);

        // When
        summary.Rewrite("The pilot landed hard in gusty conditions.");

        // Then
        summary.IsApproved.ShouldBeFalse();
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    public void Given_a_blank_summary_When_it_is_created_Then_it_is_rejected(string text)
    {
        var creating = () => Summary.Generated(TinyId.New(), Locale.EnCa, text, "model", "v4", Now);

        creating.ShouldThrow<DomainRuleViolationException>();
    }
}
