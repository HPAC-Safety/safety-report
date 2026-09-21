using HpacSafety.Core;
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
    public void GivenSameName_WhenIdentifierIsDerivedTwice_ThenSameIdentifier()
    {
        // Given / When
        var first = SeedIds.For("question:province");
        var second = SeedIds.For("question:province");

        // Then
        first.ShouldBe(second);
    }

    [Fact]
    public void GivenKnownName_WhenIdentifierIsDerived_ThenValueAlreadyWrittenToEveryDatabase()
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
    public void GivenTwoDifferentNames_WhenIdentifiersAreDerived_ThenTheyDiffer()
    {
        // Given / When / Then
        SeedIds.For("question:province").ShouldNotBe(SeedIds.For("question:pilot_injury"));
    }

    [Fact]
    public void GivenDerivedIdentifier_WhenRead_ThenOrdinaryElevenCharacterIdentifier()
    {
        // Given / When — a seeded row must be indistinguishable from a minted
        // one; deriving it changes where the entropy came from, nothing else.
        var id = SeedIds.For("question:province");

        // Then
        id.Value.Length.ShouldBe(TinyId.Length);
        TinyId.Parse(id.Value).ShouldBe(id);
    }

    [Fact]
    public void GivenEverySeededRow_WhenTheirIdentifiersAreCollected_ThenNoneOfThemCollide()
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
    public void GivenNoName_WhenIdentifierIsAskedFor_ThenRefuses()
    {
        // Given / When / Then
        Should.Throw<ArgumentException>(() => SeedIds.For("  "));
    }
}
