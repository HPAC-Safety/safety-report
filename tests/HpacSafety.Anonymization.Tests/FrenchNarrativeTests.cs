using HpacSafety.Core.Features.Anonymization;
using HpacSafety.Core.Features.Reporting;
using HpacSafety.Core.SharedKernel;
using Shouldly;

namespace HpacSafety.Anonymization.Tests;

/// <summary>
/// The golden-file suite for a report filed in French. It is a separate file
/// because the French case carries a rule English does not have: the article.
/// </summary>
/// <remarks>
/// <b>Every value here is invented.</b> No real report content, no real pilot,
/// no real launch site — see tests/README.md.
/// </remarks>
public class FrenchNarrativeTests
{
    private const string PilotFirstName = "Élise";
    private const string PilotLastName = "Tremblay";
    private const string PilotFullName = $"{PilotFirstName} {PilotLastName}";

    private const string ReporterFirstName = "Julien";
    private const string ReporterLastName = "Gagnon";
    private const string ReporterFullName = $"{ReporterFirstName} {ReporterLastName}";

    private const string SiteName = "Mont Belair";
    private const string Manufacturer = "Vantara";
    private const string Model = "Halcyon 3";

    private static DeterministicScrub Scrub() => new(ScrubVocabulary.FrenchCanada);

    // ---- the pinned terminology --------------------------------------------

    [Fact]
    public void Given_the_french_vocabulary_When_it_is_read_Then_it_carries_the_terms_hpac_decided()
    {
        // Given
        var vocabulary = ScrubVocabulary.FrenchCanada;

        // When
        var pilot = vocabulary.Pilot;
        var reporter = vocabulary.Reporter;

        // Then — these are HPAC terminology chosen by a person, not a
        // translation. Pinning them here is the point: a change to either is a
        // decision, and it fails this test until somebody makes it deliberately.
        pilot.ShouldBe("le pilote");
        reporter.ShouldBe("le déclarant");
    }

    // ---- names --------------------------------------------------------------

    [Fact]
    public void Given_a_french_narrative_naming_the_pilot_When_it_is_scrubbed_Then_the_name_is_absent()
    {
        // Given
        var report = Report($"{PilotFullName} a subi une fermeture asymétrique à 60 mètres du sol.");

        // When
        var scrubbed = Scrub().Scrub(report);

        // Then
        scrubbed.Text.ShouldNotContain(PilotFirstName, Case.Insensitive);
        scrubbed.Text.ShouldNotContain(PilotLastName, Case.Insensitive);
        scrubbed.Text.ShouldContain(ScrubVocabulary.FrenchCanada.Pilot);
    }

    [Fact]
    public void Given_a_french_narrative_naming_the_reporter_When_it_is_scrubbed_Then_the_name_is_absent()
    {
        // Given
        var report = Report($"{ReporterFullName} était à la radio et a prévenu les secours.");

        // When
        var scrubbed = Scrub().Scrub(report);

        // Then
        scrubbed.Text.ShouldNotContain(ReporterFirstName, Case.Insensitive);
        scrubbed.Text.ShouldNotContain(ReporterLastName, Case.Insensitive);
        scrubbed.Text.ShouldContain(ScrubVocabulary.FrenchCanada.Reporter);
    }

    // ---- the article does not move -----------------------------------------

    [Fact]
    public void Given_a_woman_was_flying_When_the_french_narrative_is_scrubbed_Then_the_article_stays_masculine()
    {
        // Given — the whole point. "la pilote" in a fifty-person flying
        // community narrows the field considerably, and it would put back
        // exactly the fact the scrub just took out.
        var report = Report($"{PilotFirstName} a jeté son parachute de secours; elle s'est posée dans un champ.");

        // When
        var scrubbed = Scrub().Scrub(report);

        // Then — the scrub wrote "le pilote" where the name was. The narrative
        // never contained "la pilote", so asserting its absence would prove
        // nothing; what is asserted is the substitution the scrub performed.
        scrubbed.Text.ShouldContain("le pilote a jeté");
    }

    [Fact]
    public void Given_a_woman_filed_for_another_pilot_When_it_is_scrubbed_Then_both_articles_stay_masculine()
    {
        // Given
        var report = new ScrubRequest
        {
            Province = Province.Quebec,
            Fields =
            [
                new ScrubField(ScrubFieldKind.ReporterName, "De", "Chantal Bergeron"),
                new ScrubField(ScrubFieldKind.PilotName, "Pilote", ReporterFullName),
                new ScrubField(
                    ScrubFieldKind.Narrative,
                    "Description",
                    $"Chantal a vu l'accident depuis le décollage; {ReporterFirstName} volait sous le vent."),
            ],
        };

        // When
        var scrubbed = Scrub().Scrub(report);

        // Then
        scrubbed.Text.ShouldNotContain("Chantal", Case.Insensitive);
        scrubbed.Text.ShouldNotContain("Bergeron", Case.Insensitive);
        scrubbed.Text.ShouldContain("le déclarant");
        scrubbed.Text.ShouldNotContain("la déclarante");
        scrubbed.Text.ShouldNotContain("la pilote");
    }

    [Fact]
    public void Given_a_french_name_particle_When_a_french_narrative_is_scrubbed_Then_ordinary_words_survive()
    {
        // Given — "de" and "la" are half of French. A name rule that eats them
        // has destroyed every French report the system will ever handle. Run
        // through the French vocabulary, which is the only way this test means
        // what its name says.
        var report = new ScrubRequest
        {
            Province = Province.Quebec,
            Fields =
            [
                new ScrubField(ScrubFieldKind.PilotName, "Pilote", "Marc de la Roche"),
                new ScrubField(
                    ScrubFieldKind.Narrative,
                    "Description",
                    "Le vent de la vallée a tourné et la voile a fermé."),
            ],
        };

        // When
        var scrubbed = Scrub().Scrub(report);

        // Then
        scrubbed.Text.ShouldContain("de la vallée");
        scrubbed.Text.ShouldNotContain("Roche", Case.Insensitive);
    }

    [Fact]
    public void Given_an_elided_article_before_a_name_When_it_is_scrubbed_Then_the_result_is_not_d_apostrophe_le()
    {
        // Given — "d'Élise" is how French actually writes it, and a bare
        // substitution produces "d'le pilote", which is not French.
        var report = Report($"La voile d'{PilotFirstName} a fermé sur la crête.");

        // When
        var scrubbed = Scrub().Scrub(report);

        // Then
        scrubbed.Text.ShouldNotContain(PilotFirstName, Case.Insensitive);
        scrubbed.Text.ShouldNotContain("d'le");
        scrubbed.Text.ShouldContain("du pilote");
    }

    [Fact]
    public void Given_a_capitalised_elided_article_before_a_name_When_it_is_scrubbed_Then_the_article_is_absorbed()
    {
        // Given — "L'Élise" is the other elision, and it produced "L'le pilote":
        // one letter away from the breakage the contraction fix existed to
        // remove. The role word carries its own article, so the elided one goes.
        var report = Report($"L'{PilotFirstName} était en finale quand la voile a fermé.");

        // When
        var scrubbed = Scrub().Scrub(report);

        // Then
        scrubbed.Text.ShouldNotContain("L'le");
        scrubbed.Text.ShouldNotContain("l'le");
        scrubbed.Text.ShouldContain("Le pilote était en finale");
    }

    [Fact]
    public void Given_gendered_words_the_reporter_wrote_When_it_is_scrubbed_Then_they_survive_untouched()
    {
        // Given — the source text itself says "la pilote". Stage 1 replaces
        // names, not the reporter's grammar, and this pins that honestly
        // instead of asserting against a fixture that never contained it.
        var report = Report($"{PilotFirstName} était la pilote; elle s'est posée dans un champ.");

        // When
        var scrubbed = Scrub().Scrub(report);

        // Then — the name is gone, the scrub's own article is masculine, and
        // the reporter's own words are left alone. Stage 2 rewrites; stage 3
        // flags what is left. See ADR-0028.
        scrubbed.Text.ShouldNotContain(PilotFirstName, Case.Insensitive);
        scrubbed.Text.ShouldContain("le pilote était la pilote");
        scrubbed.Text.ShouldContain("elle s'est posée");
    }

    // ---- everything at once, and what must survive -------------------------

    [Fact]
    public void Given_a_french_report_carrying_every_identifier_When_it_is_scrubbed_Then_none_of_them_remain()
    {
        // Given
        var report = Report(
            $"""
             Je m'appelle {ReporterFullName}, HPAC #48213, et je remplis ce rapport pour {PilotFullName}.
             Nous avons décollé du {SiteName} vers 14 h et elle volait une {Manufacturer} {Model}.
             {PilotFirstName} a subi une fermeture asymétrique et a jeté sa réserve à 300 mètres.
             Joignez-moi au 418-555-0142 ou à julien.gagnon@example.com.
             """);

        // When
        var scrubbed = Scrub().Scrub(report);

        // Then
        scrubbed.Text.ShouldNotContain(ReporterFirstName, Case.Insensitive);
        scrubbed.Text.ShouldNotContain(ReporterLastName, Case.Insensitive);
        scrubbed.Text.ShouldNotContain(PilotFirstName, Case.Insensitive);
        scrubbed.Text.ShouldNotContain(PilotLastName, Case.Insensitive);
        scrubbed.Text.ShouldNotContain("48213");
        scrubbed.Text.ShouldNotContain("418-555-0142");
        scrubbed.Text.ShouldNotContain("@");
        scrubbed.Text.ShouldNotContain(SiteName, Case.Insensitive);
        scrubbed.Text.ShouldNotContain("Belair", Case.Insensitive);
        scrubbed.Text.ShouldNotContain(Manufacturer, Case.Insensitive);
        scrubbed.Text.ShouldNotContain(Model, Case.Insensitive);
    }

    [Fact]
    public void Given_a_french_report_carrying_every_identifier_When_it_is_scrubbed_Then_the_safety_lesson_survives()
    {
        // Given — a scrub that deleted everything would pass every absence
        // assertion in this file. This is the one that stops it.
        var report = Report(
            $"""
             Je m'appelle {ReporterFullName} et je remplis ce rapport pour {PilotFullName}.
             Nous avons décollé du {SiteName}. Le vent de la vallée a tourné et la voile a fermé.
             {PilotFirstName} a subi une fermeture asymétrique et a jeté sa réserve à 300 mètres.
             """);

        // When
        var scrubbed = Scrub().Scrub(report);

        // Then
        scrubbed.Text.ShouldContain("fermeture asymétrique");
        scrubbed.Text.ShouldContain("réserve");
        scrubbed.Text.ShouldContain("300 mètres");
        scrubbed.Text.ShouldContain("de la vallée");
        scrubbed.Text.ShouldContain("EN B");
        scrubbed.Text.ShouldContain(EnumCode.Of(Province.Quebec));
    }

    /// <summary>A French report with every structured identifying answer filled in.</summary>
    private static ScrubRequest Report(string narrative) => new()
    {
        Province = Province.Quebec,
        Fields =
        [
            new ScrubField(ScrubFieldKind.ReporterName, "De", ReporterFullName),
            new ScrubField(ScrubFieldKind.ContactDetail, "Téléphone", "418-555-0142"),
            new ScrubField(ScrubFieldKind.ContactDetail, "Courriel", "julien.gagnon@example.com"),
            new ScrubField(ScrubFieldKind.MemberIdentifier, "Numéro de membre HPAC", "48213"),
            new ScrubField(ScrubFieldKind.PilotName, "Pilote", PilotFullName),
            new ScrubField(ScrubFieldKind.Location, "Où", SiteName),
            new ScrubField(ScrubFieldKind.AircraftIdentity, "Fabricant", Manufacturer),
            new ScrubField(ScrubFieldKind.AircraftIdentity, "Modèle", Model),
            new ScrubField(ScrubFieldKind.FreeText, "Certification", "EN B"),
            new ScrubField(ScrubFieldKind.Narrative, "Description", narrative),
        ],
    };
}
