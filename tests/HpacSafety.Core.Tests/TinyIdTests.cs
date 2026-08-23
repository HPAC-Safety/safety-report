using HpacSafety.Core.SharedKernel;

using Shouldly;

namespace HpacSafety.Core.Tests;

/// <summary>
/// The identifier every row carries. Eleven characters, sixty-four symbols,
/// nothing encoded in it. See ADR-0034.
/// </summary>
public sealed class TinyIdTests
{
    [Fact]
    public void Given_a_new_identifier_When_it_is_read_Then_it_is_eleven_characters_of_the_alphabet()
    {
        // Given / When
        var id = TinyId.New();

        // Then
        id.Value.Length.ShouldBe(11);
        id.Value.ShouldAllBe(character => TinyId.Alphabet.Contains(character, StringComparison.Ordinal));
    }

    [Fact]
    public void Given_many_new_identifiers_When_they_are_compared_Then_none_of_them_repeat()
    {
        // Given / When
        var ids = Enumerable.Range(0, 10_000).Select(_ => TinyId.New()).ToList();

        // Then
        ids.Distinct().Count().ShouldBe(ids.Count);
    }

    [Fact]
    public void Given_the_alphabet_When_it_is_inspected_Then_it_is_sixty_four_distinct_url_safe_symbols()
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
    public void Given_an_identifier_written_down_When_it_is_read_back_Then_it_is_the_same_identifier()
    {
        // Given
        var id = TinyId.New();

        // When
        var reread = TinyId.Parse(id.Value);

        // Then
        reread.ShouldBe(id);
    }

    [Fact]
    public void Given_two_identifiers_differing_only_in_case_When_they_are_compared_Then_they_are_not_the_same()
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
    public void Given_text_that_is_not_an_identifier_When_it_is_parsed_Then_it_is_refused(string? candidate)
    {
        // Given / When / Then — a malformed identifier is unrepresentable, so
        // nothing downstream has to check.
        Should.Throw<DomainRuleViolationException>(() => TinyId.Parse(candidate));
        TinyId.TryParse(candidate, out _).ShouldBeFalse();
    }

    [Fact]
    public void Given_the_same_entropy_When_an_identifier_is_derived_twice_Then_it_is_the_same_identifier()
    {
        // Given — this is what keeps the seeded question bank idempotent.
        var entropy = new byte[] { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12 };

        // When / Then
        TinyId.FromEntropy(entropy).ShouldBe(TinyId.FromEntropy(entropy));
    }

    [Fact]
    public void Given_entropy_that_differs_only_beyond_the_eleventh_byte_When_identifiers_are_derived_Then_they_are_the_same()
    {
        // Given / When / Then — only the first eleven bytes are read, and that
        // is stated rather than accidental.
        TinyId.FromEntropy([1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 99])
            .ShouldBe(TinyId.FromEntropy([1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 100]));
    }

    [Fact]
    public void Given_too_few_bytes_When_an_identifier_is_derived_Then_it_is_refused()
    {
        // Given / When / Then
        Should.Throw<ArgumentException>(() => TinyId.FromEntropy(new byte[10]));
    }

    [Fact]
    public void Given_a_default_identifier_When_it_is_inspected_Then_it_says_it_is_empty_rather_than_throwing()
    {
        // Given
        TinyId id = default;

        // When / Then
        id.IsEmpty.ShouldBeTrue();
        id.Value.ShouldBe(string.Empty);
        id.ToString().ShouldBe(string.Empty);
    }

    [Fact]
    public void Given_a_real_identifier_When_it_is_inspected_Then_it_is_not_empty()
    {
        // Given / When / Then
        TinyId.New().IsEmpty.ShouldBeFalse();
    }

    [Fact]
    public void Given_an_identifier_When_it_is_written_into_a_url_or_a_blob_key_Then_it_needs_no_escaping()
    {
        // Given — #16 namespaces a blob key by report id.
        var id = TinyId.New();

        // When
        var escaped = Uri.EscapeDataString(id.Value);

        // Then
        escaped.ShouldBe(id.Value);
    }
}
