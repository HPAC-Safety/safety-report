using HpacSafety.Core;
using HpacSafety.Core.Enums;
using HpacSafety.Core.Values;
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
    [InlineData(AircraftClass.HighEnB, "high_en_b")]
    [InlineData(AircraftClass.NotDetermined, "not_determined")]
    [InlineData(InjurySeverity.Serious, "serious")]
    [InlineData(Province.BritishColumbia, "british_columbia")]
    public void Given_a_domain_value_When_it_is_written_as_a_code_Then_it_round_trips(Enum value, string expected)
    {
        // Given / When
        var code = value switch
        {
            AircraftClass aircraftClass => EnumCode.Of(aircraftClass),
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
        var parsed = EnumCode.TryParse<AircraftClass>("en_b_ish", out var aircraftClass);

        // Then — an unrecognized certification answer is never guessed at
        parsed.ShouldBeFalse();
        aircraftClass.ShouldBe(AircraftClass.NotDetermined);
    }
}
