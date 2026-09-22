using HpacSafety.Core.Features.QuestionBank;
using HpacSafety.Core.Features.Reporting;
using Shouldly;

namespace HpacSafety.Core.Tests;

/// <summary>
///     Locales are a closed pair, and domain values round-trip as stable
///     invariant codes rather than display text.
/// </summary>
public class LocaleTests
{
    [Fact]
    public void GivenOfficialLocale_WhenCounterpartIsAskedFor_ThenOtherOne()
    {
        // Given / When / Then
        Locale.EnCa.Counterpart.ShouldBe(Locale.FrCa);
        Locale.FrCa.Counterpart.ShouldBe(Locale.EnCa);
    }

    [Fact]
    public void GivenUnsupportedCode_WhenParsed_ThenRefused()
    {
        // Given / When
        static void Parsing()
        {
            Locale.Parse("es-MX");
        }

        // Then
        Should.Throw<DomainRuleViolationException>(Parsing);
    }

    [Theory]
    [InlineData(ReportStatus.SummaryFailed, "summary_failed")]
    [InlineData(ReportStatus.PendingReview, "pending_review")]
    [InlineData(QuestionRole.ConsentPublish, "consent_publish")]
    public void GivenDomainValue_WhenWrittenAsCode_ThenRoundTrips(Enum value, string expected)
    {
        // Given / When
        var code = value switch
        {
            ReportStatus status => EnumCode.Of(status),
            QuestionRole role => EnumCode.Of(role),
            _ => throw new ArgumentOutOfRangeException(nameof(value))
        };

        // Then
        code.ShouldBe(expected);
    }

    [Fact]
    public void GivenUnknownCode_WhenParsed_ThenNothingIsGuessed()
    {
        // Given / When
        var parsed = EnumCode.TryParse<ReportStatus>("mildly_startled", out var status);

        // Then — an unrecognized code is never guessed at
        parsed.ShouldBeFalse();
        status.ShouldBe(ReportStatus.Submitted);
    }
}
