using HpacSafety.Core.SharedKernel;
using HpacSafety.Infrastructure.Persistence.Seeding;

using Shouldly;

namespace HpacSafety.Infrastructure.Tests.Seeding;

/// <summary>
/// Seeded identifiers have to be the same on every database the migration is
/// applied to, and the same again in a generated SQL script.
/// </summary>
public sealed class SeedIdsTests
{
    [Fact]
    public void Given_the_same_name_When_an_identifier_is_derived_twice_Then_it_is_the_same_identifier()
    {
        // Given / When
        var first = SeedIds.For("question:province");
        var second = SeedIds.For("question:province");

        // Then
        first.ShouldBe(second);
    }

    [Fact]
    public void Given_a_known_name_When_its_identifier_is_derived_Then_it_is_the_value_already_written_to_every_database()
    {
        // Given — pinned. Changing the derivation re-identifies every seeded
        // row, which is a data migration rather than an edit.
        const string expected = "d6G5jIiIY3z";

        // When
        var actual = SeedIds.For("question:consent_publish");

        // Then
        actual.Value.ShouldBe(expected);
    }

    [Fact]
    public void Given_two_different_names_When_identifiers_are_derived_Then_they_differ()
    {
        // Given / When / Then
        SeedIds.For("question:province").ShouldNotBe(SeedIds.For("question:pilot_injury"));
    }

    [Fact]
    public void Given_a_derived_identifier_When_it_is_read_Then_it_is_an_ordinary_eleven_character_identifier()
    {
        // Given / When — a seeded row must be indistinguishable from a minted
        // one; deriving it changes where the entropy came from, nothing else.
        var id = SeedIds.For("question:province");

        // Then
        id.Value.Length.ShouldBe(TinyId.Length);
        TinyId.Parse(id.Value).ShouldBe(id);
    }

    [Fact]
    public void Given_every_seeded_row_When_their_identifiers_are_collected_Then_none_of_them_collide()
    {
        // Given
        var ids = new List<TinyId>();

        // When
        foreach (var question in QuestionBankSeed.Questions)
        {
            ids.Add(SeedIds.For($"question:{question.Key}"));
            ids.Add(SeedIds.For($"question_version:{question.Key}:1"));

            foreach (var option in question.Options)
            {
                ids.Add(SeedIds.For($"question_option:{question.Key}:{option.Code}"));
            }
        }

        // Then
        ids.Distinct().Count().ShouldBe(ids.Count);
    }

    [Fact]
    public void Given_no_name_When_an_identifier_is_asked_for_Then_it_refuses()
    {
        // Given / When / Then
        Should.Throw<ArgumentException>(() => SeedIds.For("  "));
    }
}
