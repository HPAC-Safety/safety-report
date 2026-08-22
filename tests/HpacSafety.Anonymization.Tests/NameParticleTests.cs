using HpacSafety.Core.Features.Anonymization;
using HpacSafety.Core.Features.Reporting;
using Shouldly;

namespace HpacSafety.Anonymization.Tests;

/// <summary>
/// A name particle must be told apart from a surname without ever consulting the
/// reporter's shift key. Typing your own name in lower case on a phone is
/// ordinary; typing a surname in capitals is standard on official
/// French-Canadian forms. Both used to change what got redacted.
/// </summary>
public class NameParticleTests
{
    // ---- a short surname is matched in every casing --------------------------

    [Theory]
    [InlineData("thanh le")]
    [InlineData("Thanh Le")]
    [InlineData("THANH LE")]
    public void Given_a_two_word_name_ending_in_a_particle_word_When_it_is_scrubbed_Then_the_surname_is_absent(
        string answer)
    {
        // Given — two words, so the last one is the surname whatever its casing.
        var report = Name(answer, "Le spiralled in from 200 feet. Le's reserve failed to open.");

        // When
        var scrubbed = ScrubFixture.Scrub().Scrub(report);

        // Then
        scrubbed.Text.ShouldNotContain("Le ", Case.Insensitive);
        scrubbed.Text.ShouldNotContain("Le's", Case.Insensitive);
    }

    [Theory]
    [InlineData("anneke van")]
    [InlineData("Anneke Van")]
    [InlineData("ANNEKE VAN")]
    public void Given_a_surname_that_is_also_a_particle_word_When_it_is_scrubbed_Then_it_is_absent(string answer)
    {
        // Given
        var report = Name(answer, "Van called the rescue in.");

        // When
        var scrubbed = ScrubFixture.Scrub().Scrub(report);

        // Then
        scrubbed.Text.ShouldNotContain("Van", Case.Insensitive);
    }

    [Theory]
    [InlineData("marie-le tremblay")]
    [InlineData("Marie-Le Tremblay")]
    [InlineData("MARIE-LE TREMBLAY")]
    public void Given_a_hyphenated_given_name_containing_a_particle_word_When_it_is_scrubbed_Then_it_is_absent(
        string answer)
    {
        // Given — "le" here is half of a compound given name, not a particle.
        // A hyphen is the signal, and a hyphen does not depend on casing.
        var report = Name(answer, "Le a subi une fermeture asymétrique.");

        // When
        var scrubbed = new DeterministicScrub(ScrubVocabulary.FrenchCanada).Scrub(report);

        // Then — asserting the absence of "le " would be meaningless here,
        // because the French role word contains it. What must be gone is the
        // name standing on its own as the subject of the sentence.
        scrubbed.Text.ShouldNotContain("Le a subi");
        scrubbed.Text.ShouldContain("le pilote a subi");
    }

    // ---- and the French articles still survive, in every casing --------------

    [Theory]
    [InlineData("marc de la roche")]
    [InlineData("Marc de la Roche")]
    [InlineData("Marc De La Roche")]
    [InlineData("Marc DE LA ROCHE")]
    public void Given_a_name_with_interior_particles_When_a_french_narrative_is_scrubbed_Then_the_articles_survive(
        string answer)
    {
        // Given — "de" and "la" sit between other parts of the name, which is
        // what makes them particles. Capitals do not change that.
        var report = Name(answer, "Le vent de la vallée a tourné et la voile a fermé.");

        // When
        var scrubbed = new DeterministicScrub(ScrubVocabulary.FrenchCanada).Scrub(report);

        // Then
        scrubbed.Text.ShouldContain("de la vallée");
        scrubbed.Text.ShouldContain("la voile");
        scrubbed.Text.ShouldNotContain("Roche", Case.Insensitive);
    }

    private static ScrubRequest Name(string pilot, string narrative) => new()
    {
        Province = Province.Quebec,
        Fields =
        [
            new ScrubField(ScrubFieldKind.PilotName, "Pilot", pilot),
            new ScrubField(ScrubFieldKind.Narrative, "Description", narrative),
        ],
    };
}
