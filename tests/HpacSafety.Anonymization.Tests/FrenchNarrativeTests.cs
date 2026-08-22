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

        // Then
        scrubbed.Text.ShouldContain("le pilote");
        scrubbed.Text.ShouldNotContain("la pilote");
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
