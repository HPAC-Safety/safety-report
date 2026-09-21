
using Shouldly;

namespace HpacSafety.Core.Tests;

/// <summary>
/// The identifier every row carries. Eleven characters, sixty-four symbols,
/// nothing encoded in it. See ADR-0034.
/// </summary>
public sealed class TinyIdTests
{
    [Fact]
    public void GivenNewIdentifier_WhenRead_ThenElevenCharactersOfAlphabet()
    {
        // Given / When
        var id = TinyId.New();

        // Then
        id.Value.Length.ShouldBe(11);
        id.Value.ShouldAllBe(character => TinyId.Alphabet.Contains(character, StringComparison.Ordinal));
    }

    [Fact]
    public void GivenManyNewIdentifiers_WhenTheyAreCompared_ThenNoneOfThemRepeat()
    {
        // Given / When
        var ids = Enumerable.Range(0, 10_000).Select(_ => TinyId.New()).ToList();

        // Then
        ids.Distinct().Count().ShouldBe(ids.Count);
    }

    [Fact]
    public void GivenAlphabet_WhenInspected_ThenSixtyFourDistinctUrlSafeSymbols()
    {
        // Given / When / Then — sixty-four is what makes each character exactly
        // six bits, and what keeps the masking in New() uniform.
        TinyId.Alphabet.Length.ShouldBe(64);
        TinyId.Alphabet.Distinct().Count().ShouldBe(64);
        TinyId.Alphabet.ShouldNotContain("+");
        TinyId.Alphabet.ShouldNotContain("/");
        TinyId.Alphabet.ShouldNotContain("=");
    }

    [Fact]
    public void GivenIdentifierWrittenDown_WhenReadBack_ThenSameIdentifier()
    {
        // Given
        var id = TinyId.New();

        // When
        var reread = TinyId.Parse(id.Value);

        // Then
        reread.ShouldBe(id);
    }

    [Fact]
    public void GivenTwoIdentifiersDifferingOnlyInCase_WhenTheyAreCompared_ThenTheyAreNotSame()
    {
        // Given / When / Then — the alphabet is case-sensitive, so folding case
        // would collapse two real identifiers into one.
        TinyId.Parse("aaaaaaaaaaa").ShouldNotBe(TinyId.Parse("AAAAAAAAAAA"));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("tooshort")]
    [InlineData("waytoolongtobeone")]
    [InlineData("has space!!")]
    [InlineData("plus+slash/")]
    [InlineData("équateur123")]
    public void GivenTextIsNotIdentifier_WhenParsed_ThenRefused(string? candidate)
    {
        // Given / When / Then — a malformed identifier is unrepresentable, so
        // nothing downstream has to check.
        Should.Throw<DomainRuleViolationException>(() => TinyId.Parse(candidate));
        TinyId.TryParse(candidate, out _).ShouldBeFalse();
    }

    [Fact]
    public void GivenSameEntropy_WhenIdentifierIsDerivedTwice_ThenSameIdentifier()
    {
        // Given — this is what keeps the seeded question bank idempotent.
        var entropy = new byte[] { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12 };

        // When / Then
        TinyId.FromEntropy(entropy).ShouldBe(TinyId.FromEntropy(entropy));
    }

    [Fact]
    public void GivenEntropyDiffersOnlyBeyondEleventhByte_WhenIdentifiersAreDerived_ThenTheyAreSame()
    {
        // Given / When / Then — only the first eleven bytes are read, and that
        // is stated rather than accidental.
        TinyId.FromEntropy([1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 99])
            .ShouldBe(TinyId.FromEntropy([1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 100]));
    }

    [Fact]
    public void GivenTooFewBytes_WhenIdentifierIsDerived_ThenRefused()
    {
        // Given / When / Then
        Should.Throw<ArgumentException>(() => TinyId.FromEntropy(new byte[10]));
    }

    [Fact]
    public void GivenDefaultIdentifier_WhenInspected_ThenSaysEmptyRatherThanThrowing()
    {
        // Given
        TinyId id = default;

        // When / Then
        id.IsEmpty.ShouldBeTrue();
        id.Value.ShouldBe(string.Empty);
        id.ToString().ShouldBe(string.Empty);
    }

    [Fact]
    public void GivenRealIdentifier_WhenInspected_ThenNotEmpty()
    {
        // Given / When / Then
        TinyId.New().IsEmpty.ShouldBeFalse();
    }

    [Fact]
    public void GivenIdentifier_WhenWrittenIntoUrlOrBlobKey_ThenNeedsNoEscaping()
    {
        // Given — #16 namespaces a blob key by report id.
        var id = TinyId.New();

        // When
        var escaped = Uri.EscapeDataString(id.Value);

        // Then
        escaped.ShouldBe(id.Value);
    }
}
