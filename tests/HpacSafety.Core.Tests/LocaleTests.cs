using HpacSafety.Core.Features.Moderation;
using HpacSafety.Core.Features.Outbox;
using HpacSafety.Core.Features.QuestionBank;
using HpacSafety.Core.Features.Reporting;
using HpacSafety.Core.SharedKernel;
using Shouldly;

namespace HpacSafety.Core.Tests;

/// <summary>Locales are a closed pair, and domain values round-trip as stable
/// invariant codes rather than display text.</summary>
public class LocaleTests
{
    [Fact]
    public void Given_an_official_locale_When_its_counterpart_is_asked_for_Then_it_is_the_other_one()
    {
        // Given / When / Then
        Locale.EnCa.Counterpart.ShouldBe(Locale.FrCa);
        Locale.FrCa.Counterpart.ShouldBe(Locale.EnCa);
    }

    [Fact]
    public void Given_an_unsupported_code_When_it_is_parsed_Then_it_is_refused()
    {
        // Given / When
        void Parsing() => Locale.Parse("es-MX");

        // Then
        Should.Throw<DomainRuleViolationException>(Parsing);
    }

    [Theory]
    [InlineData(Discipline.HangGliding, "hang_gliding")]
    [InlineData(Discipline.Unknown, "unknown")]
    [InlineData(InjurySeverity.Serious, "serious")]
    [InlineData(Province.BritishColumbia, "british_columbia")]
    public void Given_a_domain_value_When_it_is_written_as_a_code_Then_it_round_trips(Enum value, string expected)
    {
        // Given / When
        var code = value switch
        {
            Discipline discipline => EnumCode.Of(discipline),
            InjurySeverity severity => EnumCode.Of(severity),
            Province province => EnumCode.Of(province),
            _ => throw new ArgumentOutOfRangeException(nameof(value)),
        };

        // Then
        code.ShouldBe(expected);
    }

    [Fact]
    public void Given_an_unknown_code_When_it_is_parsed_Then_nothing_is_guessed()
    {
        // Given / When
        var parsed = EnumCode.TryParse<Discipline>("hang_gliding_ish", out var discipline);

        // Then — an unrecognized code is never guessed at
        parsed.ShouldBeFalse();
        discipline.ShouldBe(Discipline.Unknown);
    }
}
