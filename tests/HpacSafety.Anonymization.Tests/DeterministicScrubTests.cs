using HpacSafety.Core.Features.Anonymization;
using HpacSafety.Core.Features.Reporting;
using HpacSafety.Core.SharedKernel;
using Shouldly;

namespace HpacSafety.Anonymization.Tests;

/// <summary>
/// The golden-file suite for stage 1 of the anonymization pipeline. Each case
/// seeds a fixture report with a known, invented identifier and asserts that the
/// specific token is absent from what the scrub produces.
/// </summary>
/// <remarks>
/// These tests assert <b>absence of an identifier</b>, never an exact output
/// sentence — see docs/testing-conventions.md. They touch no database, no
/// network, and no model.
/// </remarks>
public class DeterministicScrubTests
{
    // ---- names in the narrative -------------------------------------------

    [Fact]
    public void Given_a_narrative_naming_the_pilot_When_it_is_scrubbed_Then_the_name_is_absent()
    {
        // Given
        var report = ScrubFixture.Report(
            $"{ScrubFixture.PilotFullName} spiralled in from about 200 feet and landed hard on her left side.");

        // When
        var scrubbed = ScrubFixture.Scrub().Scrub(report);

        // Then
        scrubbed.Text.ShouldNotContain(ScrubFixture.PilotFirstName, Case.Insensitive);
        scrubbed.Text.ShouldNotContain(ScrubFixture.PilotLastName, Case.Insensitive);
    }

    [Fact]
    public void Given_a_narrative_naming_the_reporter_When_it_is_scrubbed_Then_the_name_is_absent()
    {
        // Given
        var report = ScrubFixture.Report(
            $"{ScrubFixture.ReporterFullName} was on radio and called the launch marshal.");

        // When
        var scrubbed = ScrubFixture.Scrub().Scrub(report);

        // Then
        scrubbed.Text.ShouldNotContain(ScrubFixture.ReporterFirstName, Case.Insensitive);
        scrubbed.Text.ShouldNotContain(ScrubFixture.ReporterLastName, Case.Insensitive);
    }

    [Fact]
    public void Given_a_pilot_name_in_the_narrative_When_it_is_scrubbed_Then_the_pilot_role_word_stands_in_its_place()
    {
        // Given
        var report = ScrubFixture.Report($"{ScrubFixture.PilotFirstName} deployed the reserve at 150 feet.");

        // When
        var scrubbed = ScrubFixture.Scrub().Scrub(report);

        // Then
        scrubbed.Text.ShouldContain(ScrubVocabulary.EnglishCanada.Pilot);
    }

    [Fact]
    public void Given_a_reporter_who_is_not_the_pilot_When_both_names_appear_Then_each_takes_its_own_role_word()
    {
        // Given
        var report = ScrubFixture.Report(
            $"{ScrubFixture.ReporterFirstName} watched from launch while {ScrubFixture.PilotFirstName} flew the ridge.");

        // When
        var scrubbed = ScrubFixture.Scrub().Scrub(report);

        // Then
        scrubbed.Text.ShouldContain(ScrubVocabulary.EnglishCanada.Reporter);
        scrubbed.Text.ShouldContain(ScrubVocabulary.EnglishCanada.Pilot);
    }

    [Fact]
    public void Given_a_reporter_who_is_the_pilot_When_the_name_appears_Then_it_is_absent_and_reads_as_the_pilot()
    {
        // Given
        var report = new ScrubRequest
        {
            Province = Province.Alberta,
            Fields =
            [
                new ScrubField(ScrubFieldKind.ReporterName, "From", ScrubFixture.PilotFullName),
                new ScrubField(ScrubFieldKind.PilotName, "Pilot", ScrubFixture.PilotFullName),
                new ScrubField(
                    ScrubFieldKind.Narrative,
                    "Description",
                    $"I am {ScrubFixture.PilotFullName} and I misjudged the final glide."),
            ],
        };

        // When
        var scrubbed = ScrubFixture.Scrub().Scrub(report);

        // Then
        scrubbed.Text.ShouldNotContain(ScrubFixture.PilotFirstName, Case.Insensitive);
        scrubbed.Text.ShouldNotContain(ScrubFixture.PilotLastName, Case.Insensitive);
        scrubbed.Text.ShouldContain(ScrubVocabulary.EnglishCanada.Pilot);
        scrubbed.Text.ShouldNotContain(ScrubVocabulary.EnglishCanada.Reporter);
    }

    [Fact]
    public void Given_a_name_written_in_a_different_case_When_it_is_scrubbed_Then_it_is_still_absent()
    {
        // Given
        var report = ScrubFixture.Report("SARAH shouted a warning, and sarah's wing collapsed anyway.");

        // When
        var scrubbed = ScrubFixture.Scrub().Scrub(report);

        // Then
        scrubbed.Text.ShouldNotContain("SARAH", Case.Insensitive);
    }

    [Fact]
    public void Given_a_name_that_is_part_of_a_longer_word_When_it_is_scrubbed_Then_the_surrounding_word_survives()
    {
        // Given — "Marc" inside "Marconi" is not the reporter.
        var report = ScrubFixture.NarrativeOnly("The Marconi antenna on the ridge was the only landmark.");
        var scrub = new DeterministicScrub(ScrubVocabulary.EnglishCanada);

        // When
        var scrubbed = scrub.Scrub(report with
        {
            Fields =
            [
                new ScrubField(ScrubFieldKind.ReporterName, "From", ScrubFixture.ReporterFullName),
                .. report.Fields,
            ],
        });

        // Then
        scrubbed.Text.ShouldContain("Marconi");
    }

    [Fact]
    public void Given_a_name_spelled_without_its_accents_in_the_narrative_When_it_is_scrubbed_Then_it_is_still_absent()
    {
        // Given — the name field says "Renée"; the narrative says "Renee".
        var report = new ScrubRequest
        {
            Province = Province.Quebec,
            Fields =
            [
                new ScrubField(ScrubFieldKind.PilotName, "Pilot", "Renée Boucher"),
                new ScrubField(ScrubFieldKind.Narrative, "Description", "Renee flew into the lee side and sank out."),
            ],
        };

        // When
        var scrubbed = ScrubFixture.Scrub().Scrub(report);

        // Then
        scrubbed.Text.ShouldNotContain("Renee", Case.Insensitive);
        scrubbed.Text.ShouldNotContain("Boucher", Case.Insensitive);
    }

    [Fact]
    public void Given_a_narrative_that_adds_accents_the_name_field_lacked_When_it_is_scrubbed_Then_it_is_still_absent()
    {
        // Given — the mirror image, which is just as common.
        var report = new ScrubRequest
        {
            Province = Province.Quebec,
            Fields =
            [
                new ScrubField(ScrubFieldKind.PilotName, "Pilot", "Renee Boucher"),
                new ScrubField(ScrubFieldKind.Narrative, "Description", "Renée flew into the lee side and sank out."),
            ],
        };

        // When
        var scrubbed = ScrubFixture.Scrub().Scrub(report);

        // Then
        scrubbed.Text.ShouldNotContain("Renée", Case.Insensitive);
    }

    [Fact]
    public void Given_a_double_barrelled_given_name_When_only_half_of_it_is_used_in_the_narrative_Then_it_is_absent()
    {
        // Given
        var report = new ScrubRequest
        {
            Province = Province.Ontario,
            Fields =
            [
                new ScrubField(ScrubFieldKind.PilotName, "Pilot", "Sarah-Jane O'Brien"),
                new ScrubField(ScrubFieldKind.Narrative, "Description", "Sarah turned downwind low and Brien called it."),
            ],
        };

        // When
        var scrubbed = ScrubFixture.Scrub().Scrub(report);

        // Then
        scrubbed.Text.ShouldNotContain("Sarah", Case.Insensitive);
        scrubbed.Text.ShouldNotContain("Brien", Case.Insensitive);
    }


    // ---- leaks the second independent audit proved --------------------------

    [Fact]
    public void Given_a_two_letter_surname_in_a_longer_name_answer_When_it_is_scrubbed_Then_it_is_absent()
    {
        // Given — Ng, Wu, Li, Vo, Ha are common surnames, not exotic. Gating
        // the parts at three characters dropped them on the floor.
        var report = new ScrubRequest
        {
            Province = Province.BritishColumbia,
            Fields =
            [
                new ScrubField(ScrubFieldKind.PilotName, "Pilot", "Sarah Ng"),
                new ScrubField(
                    ScrubFieldKind.Narrative,
                    "Description",
                    "Ng spiralled in from 200 feet. Ng's reserve failed to open."),
            ],
        };

        // When
        var scrubbed = ScrubFixture.Scrub().Scrub(report);

        // Then
        scrubbed.Text.ShouldNotContain("Ng", Case.Insensitive);
    }

    [Fact]
    public void Given_a_short_site_answer_When_it_is_scrubbed_Then_it_is_absent_from_the_narrative()
    {
        // Given — a three-character launch name never became a matcher at all.
        var report = new ScrubRequest
        {
            Province = Province.BritishColumbia,
            Fields =
            [
                new ScrubField(ScrubFieldKind.Location, "Where", "Cox"),
                new ScrubField(ScrubFieldKind.AircraftIdentity, "Manufacturer", "UP"),
                new ScrubField(ScrubFieldKind.Narrative, "Description", "Launched at Cox on a UP wing."),
            ],
        };

        // When
        var scrubbed = ScrubFixture.Scrub().Scrub(report);

        // Then
        scrubbed.Text.ShouldNotContain("Cox", Case.Insensitive);
        scrubbed.Text.ShouldNotContain("UP", Case.Sensitive);
    }

    [Fact]
    public void Given_a_compact_aircraft_answer_When_the_narrative_spaces_it_out_Then_it_is_absent()
    {
        // Given — the mirror of the "Halcyon 3" case. A short form field
        // invites a compact spelling; prose spaces it out.
        var report = new ScrubRequest
        {
            Province = Province.Alberta,
            Fields =
            [
                new ScrubField(ScrubFieldKind.AircraftIdentity, "Model", "Halcyon3"),
                new ScrubField(ScrubFieldKind.Location, "Where", "MountFerndale"),
                new ScrubField(
                    ScrubFieldKind.Narrative,
                    "Description",
                    "We flew Mount Ferndale on a Halcyon 3 that afternoon."),
            ],
        };

        // When
        var scrubbed = ScrubFixture.Scrub().Scrub(report);

        // Then
        scrubbed.Text.ShouldNotContain("Halcyon", Case.Insensitive);
        scrubbed.Text.ShouldNotContain("Ferndale", Case.Insensitive);
    }

    [Fact]
    public void Given_an_unclassified_field_When_its_value_recurs_in_the_narrative_Then_it_is_absent_there_too()
    {
        // Given — dropping the field is not enough. ADR-0027 justifies the
        // fail-closed zero value with exactly this scenario, so it has to hold
        // for the narrative as well as for the field.
        var report = new ScrubRequest
        {
            Province = Province.Alberta,
            Fields =
            [
                new ScrubField(ScrubFieldKind.Unclassified, "Next of kin", "Helene Marchetti"),
                new ScrubField(
                    ScrubFieldKind.Narrative,
                    "Description",
                    "Helene Marchetti drove her to the hospital."),
            ],
        };

        // When
        var scrubbed = ScrubFixture.Scrub().Scrub(report);

        // Then
        scrubbed.Text.ShouldNotContain("Helene", Case.Insensitive);
        scrubbed.Text.ShouldNotContain("Marchetti", Case.Insensitive);
    }

    [Theory]
    [InlineData("48 213")]
    [InlineData("48-213")]
    [InlineData("48.213")]
    public void Given_a_member_number_written_in_digit_groups_When_it_is_scrubbed_Then_it_is_absent(string written)
    {
        // Given — "48 213" is the standard fr-CA rendering of the same number.
        var report = ScrubFixture.NarrativeOnly($"Mon numéro de membre est {written}.");

        // When
        var scrubbed = ScrubFixture.Scrub().Scrub(report);

        // Then
        scrubbed.Text.ShouldNotContain(written);
        scrubbed.Text.ShouldNotContain("213");
    }

    [Fact]
    public void Given_a_role_word_written_by_an_earlier_matcher_When_a_later_token_matches_it_Then_it_is_left_alone()
    {
        // Given — "Pilot" is the reporter's surname here. Rewriting the value
        // once per token let a later matcher fire inside "the pilot", which an
        // earlier matcher had just written.
        var report = new ScrubRequest
        {
            Province = Province.Ontario,
            Fields =
            [
                new ScrubField(ScrubFieldKind.ReporterName, "From", "Ann Pilot"),
                new ScrubField(ScrubFieldKind.PilotName, "Pilot", "Sarah Whitlock"),
                new ScrubField(ScrubFieldKind.Narrative, "Description", "Whitlock flew the ridge."),
            ],
        };

        // When
        var scrubbed = ScrubFixture.Scrub().Scrub(report);

        // Then
        scrubbed.Text.ShouldNotContain("the the");
        scrubbed.Text.ShouldContain("the pilot flew the ridge.");
    }

    [Fact]
    public void Given_gendered_words_the_reporter_wrote_When_it_is_scrubbed_Then_they_survive_untouched()
    {
        // Given — stage 1 does not rewrite the reporter's own prose, and this
        // pins that honestly rather than leaving the docs claiming otherwise.
        // Pronouns and agreement are stage 2's job and stage 3 flags what is
        // left. See ADR-0028.
        var report = ScrubFixture.Report($"{ScrubFixture.PilotFirstName} landed hard; she broke her ankle.");

        // When
        var scrubbed = ScrubFixture.Scrub().Scrub(report);

        // Then — the name is gone; the gendered pronoun the reporter wrote is not.
        scrubbed.Text.ShouldNotContain(ScrubFixture.PilotFirstName, Case.Insensitive);
        scrubbed.Text.ShouldContain("she broke her ankle");
    }

    // ---- gaps the anonymization auditor proved -----------------------------

    [Fact]
    public void Given_a_pluralised_surname_in_the_narrative_When_it_is_scrubbed_Then_it_is_absent()
    {
        // Given — "Whitlock's" was caught because an apostrophe is not a word
        // character. "Whitlocks" was not, and it names the same family.
        var report = ScrubFixture.Report("The Whitlocks were both flying that afternoon.");

        // When
        var scrubbed = ScrubFixture.Scrub().Scrub(report);

        // Then
        scrubbed.Text.ShouldNotContain(ScrubFixture.PilotLastName, Case.Insensitive);
    }

    [Fact]
    public void Given_a_pluralised_site_name_in_the_narrative_When_it_is_scrubbed_Then_it_is_absent()
    {
        // Given
        var report = ScrubFixture.Report("Both Ferndales are known for that rotor.");

        // When
        var scrubbed = ScrubFixture.Scrub().Scrub(report);

        // Then
        scrubbed.Text.ShouldNotContain("Ferndale", Case.Insensitive);
    }

    [Fact]
    public void Given_a_name_answer_in_decomposed_unicode_When_the_narrative_uses_the_composed_form_Then_it_is_absent()
    {
        // Given — the same name, two encodings. A browser can submit either,
        // and they are not equal byte for byte.
        const string decomposed = "Rene\u0301e Boucher";
        const string composed = "Ren\u00e9e";

        var report = new ScrubRequest
        {
            Province = Province.Quebec,
            Fields =
            [
                new ScrubField(ScrubFieldKind.PilotName, "Pilot", decomposed),
                new ScrubField(ScrubFieldKind.Narrative, "Description", $"{composed} sank out behind the ridge."),
            ],
        };

        // When
        var scrubbed = ScrubFixture.Scrub().Scrub(report);

        // Then
        scrubbed.Text.ShouldNotContain(composed, Case.Insensitive);
        scrubbed.Text.ShouldNotContain("Boucher", Case.Insensitive);
    }

    [Fact]
    public void Given_an_aircraft_model_written_without_its_space_When_it_is_scrubbed_Then_it_is_absent()
    {
        // Given — the field says "Halcyon 3"; the narrative says "Halcyon3".
        var report = ScrubFixture.Report("I had been flying the Halcyon3 for two seasons.");

        // When
        var scrubbed = ScrubFixture.Scrub().Scrub(report);

        // Then
        scrubbed.Text.ShouldNotContain("Halcyon", Case.Insensitive);
    }

    [Fact]
    public void Given_a_single_letter_name_answer_When_the_narrative_is_scrubbed_Then_ordinary_words_survive()
    {
        // Given — a reporter who types an initial into the name field. Matching
        // a one-character token would replace every standalone "a" in the
        // narrative with "the pilot" and destroy the report.
        var report = new ScrubRequest
        {
            Province = Province.Alberta,
            Fields =
            [
                new ScrubField(ScrubFieldKind.PilotName, "Pilot", "A"),
                new ScrubField(ScrubFieldKind.Narrative, "Description", "I took a big collapse over a spine."),
            ],
        };

        // When
        var scrubbed = ScrubFixture.Scrub().Scrub(report);

        // Then
        scrubbed.Text.ShouldContain("a big collapse");
        scrubbed.Text.ShouldContain("a spine");
    }

    [Fact]
    public void Given_a_two_letter_surname_as_the_whole_answer_When_it_is_scrubbed_Then_it_is_absent()
    {
        // Given — "Le" is a common Vietnamese surname as well as a French
        // article. When it is the entire name answer it is a surname, and the
        // cost of matching it is that a French narrative loses some articles.
        // That cost is accepted: over-redaction is recoverable, a named pilot
        // is not. See ADR-0027.
        var report = new ScrubRequest
        {
            Province = Province.Quebec,
            Fields =
            [
                new ScrubField(ScrubFieldKind.PilotName, "Pilote", "Le"),
                new ScrubField(ScrubFieldKind.Narrative, "Description", "Le a subi une fermeture."),
            ],
        };

        // When
        var scrubbed = ScrubFixture.Scrub().Scrub(report);

        // Then
        scrubbed.Text.ShouldNotContain("Le ", Case.Sensitive);
    }

    // ---- contact details ---------------------------------------------------

    [Theory]
    [InlineData("403-555-0142")]
    [InlineData("(403) 555-0142")]
    [InlineData("403.555.0142")]
    [InlineData("403 555 0142")]
    [InlineData("4035550142")]
    [InlineData("+1 403 555 0142")]
    [InlineData("1-800-555-0142")]
    [InlineData("+1 (403) 555-0142")]
    [InlineData("403\u2013555\u20130142")]
    [InlineData("1 403 555 0142")]
    [InlineData("555-0142")]
    [InlineData("403/555/0142")]
    [InlineData("(403)  555-0142")]
    [InlineData("403\u2011555\u20110142")]
    [InlineData("+44 7700 900142")]
    public void Given_a_phone_number_in_the_narrative_When_it_is_scrubbed_Then_the_number_is_absent(string phone)
    {
        // Given
        var report = ScrubFixture.NarrativeOnly($"Call me at {phone} if you need more detail.");

        // When
        var scrubbed = ScrubFixture.Scrub().Scrub(report);

        // Then — the last four digits are the part that is common to every row,
        // so asserting on them cannot be vacuous the way a literal "555" was
        // for the international case.
        scrubbed.Text.ShouldNotContain(phone);
        scrubbed.Text.ShouldNotContain(phone[^4..]);
    }

    [Fact]
    public void Given_an_altitude_in_the_narrative_When_it_is_scrubbed_Then_the_safety_detail_survives()
    {
        // Given — a phone rule that eats "1500 feet" has destroyed the lesson.
        var report = ScrubFixture.NarrativeOnly("The collapse happened at 1500 feet above the valley floor.");

        // When
        var scrubbed = ScrubFixture.Scrub().Scrub(report);

        // Then
        scrubbed.Text.ShouldContain("1500 feet");
    }

    [Theory]
    [InlineData("marc.delacroix@example.com")]
    [InlineData("Marc.Delacroix+safety@mail.example.org")]
    [InlineData("m_delacroix99@lists.example.net")]
    public void Given_an_email_address_in_the_narrative_When_it_is_scrubbed_Then_the_address_is_absent(string email)
    {
        // Given
        var report = ScrubFixture.NarrativeOnly($"Reach me on {email} for the GPS track.");

        // When
        var scrubbed = ScrubFixture.Scrub().Scrub(report);

        // Then
        scrubbed.Text.ShouldNotContain(email, Case.Insensitive);
        scrubbed.Text.ShouldNotContain("@");
    }

    [Theory]
    [InlineData("https://www.ferndale-freeflight.example.org/logbook/2026")]
    [InlineData("http://example.com/track?id=88214")]
    [InlineData("www.ferndale-freeflight.example.org")]
    [InlineData("ferndale-freeflight.example.org/tracks")]
    [InlineData("Ferndale-Freeflight.example.ORG")]
    public void Given_a_url_in_the_narrative_When_it_is_scrubbed_Then_the_url_is_absent(string url)
    {
        // Given
        var report = ScrubFixture.NarrativeOnly($"The flight track is posted at {url} for anyone who wants it.");

        // When
        var scrubbed = ScrubFixture.Scrub().Scrub(report);

        // Then
        scrubbed.Text.ShouldNotContain(url, Case.Insensitive);
        scrubbed.Text.ShouldNotContain("ferndale", Case.Insensitive);
    }

    [Theory]
    [InlineData("HPAC #48213")]
    [InlineData("HPAC member 48213")]
    [InlineData("HPAC member number 48213")]
    [InlineData("member #48213")]
    [InlineData("membership number 48213")]
    [InlineData("member no. 48213")]
    [InlineData("HPAC: 48213")]
    [InlineData("membre 48213")]
    [InlineData("num\u00e9ro de membre 48213")]
    [InlineData("my HPAC number is 48213")]
    [InlineData("HPAC ID 48213")]
    [InlineData("my membership is 48213")]
    [InlineData("my HPAC no is 48213")]
    public void Given_an_hpac_member_number_in_the_narrative_When_it_is_scrubbed_Then_the_number_is_absent(string written)
    {
        // Given
        var report = ScrubFixture.NarrativeOnly($"I have been flying since 2014, {written}, mostly in the valley.");

        // When
        var scrubbed = ScrubFixture.Scrub().Scrub(report);

        // Then
        scrubbed.Text.ShouldNotContain(ScrubFixture.MemberNumber);
    }

    // ---- structured fields -------------------------------------------------

    [Fact]
    public void Given_structured_contact_fields_When_the_report_is_scrubbed_Then_they_are_dropped_outright()
    {
        // Given
        var report = ScrubFixture.Report("An ordinary top landing that went wrong.");

        // When
        var scrubbed = ScrubFixture.Scrub().Scrub(report);

        // Then
        scrubbed.Text.ShouldNotContain(ScrubFixture.ReporterPhone);
        scrubbed.Text.ShouldNotContain(ScrubFixture.ReporterEmail, Case.Insensitive);
        scrubbed.Text.ShouldNotContain(ScrubFixture.MemberNumber);
        scrubbed.Text.ShouldNotContain(ScrubFixture.ReporterLastName, Case.Insensitive);
        scrubbed.Text.ShouldNotContain(ScrubFixture.PilotLastName, Case.Insensitive);
        scrubbed.Fields.ShouldNotContain(field => field.Kind == ScrubFieldKind.ContactDetail);
        scrubbed.Fields.ShouldNotContain(field => field.Kind == ScrubFieldKind.ReporterName);
        scrubbed.Fields.ShouldNotContain(field => field.Kind == ScrubFieldKind.PilotName);
        scrubbed.Fields.ShouldNotContain(field => field.Kind == ScrubFieldKind.MemberIdentifier);
    }

    // ---- location ----------------------------------------------------------

    [Fact]
    public void Given_a_launch_site_in_the_where_field_When_it_is_scrubbed_Then_the_site_is_replaced_by_the_province()
    {
        // Given
        var report = ScrubFixture.Report("A collapse on the ridge line.");

        // When
        var scrubbed = ScrubFixture.Scrub().Scrub(report);

        // Then
        scrubbed.Text.ShouldNotContain(ScrubFixture.SiteName, Case.Insensitive);
        scrubbed.Text.ShouldContain(EnumCode.Of(Province.BritishColumbia));
    }

    [Fact]
    public void Given_a_launch_site_named_in_the_narrative_When_it_is_scrubbed_Then_the_site_name_is_absent()
    {
        // Given
        var report = ScrubFixture.Report(
            $"We launched from {ScrubFixture.SiteName} in light east wind and I landed short of the usual field.");

        // When
        var scrubbed = ScrubFixture.Scrub().Scrub(report);

        // Then
        scrubbed.Text.ShouldNotContain(ScrubFixture.SiteName, Case.Insensitive);
        scrubbed.Text.ShouldNotContain("Ferndale", Case.Insensitive);
    }

    [Fact]
    public void Given_a_landing_zone_name_in_a_second_location_field_When_it_is_scrubbed_Then_it_is_absent_everywhere()
    {
        // Given
        var report = new ScrubRequest
        {
            Province = Province.Quebec,
            Fields =
            [
                new ScrubField(ScrubFieldKind.Location, "Where", ScrubFixture.SiteName),
                new ScrubField(ScrubFieldKind.Location, "Landing zone", ScrubFixture.LandingZoneName),
                new ScrubField(
                    ScrubFieldKind.Narrative,
                    "Description",
                    $"I overflew {ScrubFixture.LandingZoneName} and put down in a ploughed field instead."),
            ],
        };

        // When
        var scrubbed = ScrubFixture.Scrub().Scrub(report);

        // Then
        scrubbed.Text.ShouldNotContain(ScrubFixture.LandingZoneName, Case.Insensitive);
        scrubbed.Text.ShouldNotContain(ScrubFixture.SiteName, Case.Insensitive);
        scrubbed.Text.ShouldContain(EnumCode.Of(Province.Quebec));
    }

    [Fact]
    public void Given_no_province_was_answered_When_the_location_is_generalized_Then_the_location_field_is_dropped()
    {
        // Given — with nothing to generalize to, the safe answer is no location.
        var report = new ScrubRequest
        {
            Province = Province.NotAnswered,
            Fields =
            [
                new ScrubField(ScrubFieldKind.Location, "Where", ScrubFixture.SiteName),
                new ScrubField(ScrubFieldKind.Narrative, "Description", "A hard landing in rotor."),
            ],
        };

        // When
        var scrubbed = ScrubFixture.Scrub().Scrub(report);

        // Then
        scrubbed.Text.ShouldNotContain(ScrubFixture.SiteName, Case.Insensitive);
        scrubbed.Fields.ShouldNotContain(field => field.Kind == ScrubFieldKind.Location);
    }

    // ---- aircraft identity -------------------------------------------------

    [Fact]
    public void Given_an_aircraft_make_and_model_When_the_report_is_scrubbed_Then_both_are_absent()
    {
        // Given
        var report = ScrubFixture.Report(
            $"I was on a {ScrubFixture.Manufacturer} {ScrubFixture.Model} that I had flown for two seasons.");

        // When
        var scrubbed = ScrubFixture.Scrub().Scrub(report);

        // Then
        scrubbed.Text.ShouldNotContain(ScrubFixture.Manufacturer, Case.Insensitive);
        scrubbed.Text.ShouldNotContain(ScrubFixture.Model, Case.Insensitive);
        scrubbed.Fields.ShouldNotContain(field => field.Kind == ScrubFieldKind.AircraftIdentity);
    }

    [Fact]
    public void Given_an_aircraft_make_and_model_When_the_report_is_scrubbed_Then_the_certification_answer_survives()
    {
        // Given — the class comes from the reporter's own answer and is publishable.
        var report = ScrubFixture.Report("A frontal collapse in thermic air.");

        // When
        var scrubbed = ScrubFixture.Scrub().Scrub(report);

        // Then
        scrubbed.Text.ShouldContain("EN B");
    }

    // ---- what the reporter told us, found again in the narrative -----------

    [Fact]
    public void Given_a_social_handle_in_a_contact_field_When_it_recurs_in_the_narrative_Then_it_is_absent_there_too()
    {
        // Given — no pattern matches an @handle. The reporter handed us the
        // exact string in a structured answer, so nothing has to.
        var report = new ScrubRequest
        {
            Province = Province.Alberta,
            Fields =
            [
                new ScrubField(ScrubFieldKind.ContactDetail, "Social", "@sarahflies"),
                new ScrubField(
                    ScrubFieldKind.Narrative,
                    "Description",
                    "There is video of the collapse on my page, @sarahflies, if that helps."),
            ],
        };

        // When
        var scrubbed = ScrubFixture.Scrub().Scrub(report);

        // Then
        scrubbed.Text.ShouldNotContain("sarahflies", Case.Insensitive);
    }

    [Fact]
    public void Given_a_member_number_in_a_structured_field_When_it_recurs_in_the_narrative_Then_it_is_absent_there_too()
    {
        // Given — written with no keyword near it, so only the structured
        // answer can find it.
        var report = new ScrubRequest
        {
            Province = Province.Alberta,
            Fields =
            [
                new ScrubField(ScrubFieldKind.MemberIdentifier, "HPAC member number", ScrubFixture.MemberNumber),
                new ScrubField(
                    ScrubFieldKind.Narrative,
                    "Description",
                    $"I gave the paramedics my number, {ScrubFixture.MemberNumber}, at the landing field."),
            ],
        };

        // When
        var scrubbed = ScrubFixture.Scrub().Scrub(report);

        // Then
        scrubbed.Text.ShouldNotContain(ScrubFixture.MemberNumber);
    }

    // ---- fields nobody classified ------------------------------------------

    [Fact]
    public void Given_a_field_whose_handling_was_never_classified_When_it_is_scrubbed_Then_it_is_dropped()
    {
        // Given — an administrator adds a "next of kin" question and the role
        // mapping misses it. The safe answer is to drop it, not to publish it.
        var report = new ScrubRequest
        {
            Province = Province.Alberta,
            Fields =
            [
                new ScrubField(ScrubFieldKind.Unclassified, "Next of kin", "Helene Marchetti"),
                new ScrubField(ScrubFieldKind.Narrative, "Description", "A hard landing in the trees."),
            ],
        };

        // When
        var scrubbed = ScrubFixture.Scrub().Scrub(report);

        // Then
        scrubbed.Text.ShouldNotContain("Helene", Case.Insensitive);
        scrubbed.Text.ShouldNotContain("Marchetti", Case.Insensitive);
        scrubbed.Fields.Count.ShouldBe(1);
    }

    [Fact]
    public void Given_the_default_field_kind_When_it_is_read_Then_it_is_the_one_that_drops()
    {
        // Given
        var uninitialised = default(ScrubFieldKind);

        // When
        var kind = uninitialised;

        // Then
        kind.ShouldBe(ScrubFieldKind.Unclassified);
    }

    // ---- everything at once ------------------------------------------------

    [Fact]
    public void Given_a_narrative_carrying_every_category_of_identifier_When_it_is_scrubbed_Then_none_of_them_remain()
    {
        // Given
        var report = ScrubFixture.Report(
            $"""
             My name is {ScrubFixture.ReporterFullName}, HPAC #{ScrubFixture.MemberNumber}, and I am filing this for
             {ScrubFixture.PilotFullName}. We launched from {ScrubFixture.SiteName} at about 1400 and she was flying a
             {ScrubFixture.Manufacturer} {ScrubFixture.Model}. {ScrubFixture.PilotFirstName} took a big asymmetric on the
             ridge line and threw the reserve. Reach me at {ScrubFixture.ReporterPhone} or
             {ScrubFixture.ReporterEmail}; the track is at {ScrubFixture.ClubUrl}.
             """);

        // When
        var scrubbed = ScrubFixture.Scrub().Scrub(report);

        // Then
        scrubbed.Text.ShouldNotContain(ScrubFixture.ReporterFirstName, Case.Insensitive);
        scrubbed.Text.ShouldNotContain(ScrubFixture.ReporterLastName, Case.Insensitive);
        scrubbed.Text.ShouldNotContain(ScrubFixture.PilotFirstName, Case.Insensitive);
        scrubbed.Text.ShouldNotContain(ScrubFixture.PilotLastName, Case.Insensitive);
        scrubbed.Text.ShouldNotContain(ScrubFixture.MemberNumber);
        scrubbed.Text.ShouldNotContain(ScrubFixture.ReporterPhone);
        scrubbed.Text.ShouldNotContain(ScrubFixture.ReporterEmail, Case.Insensitive);
        scrubbed.Text.ShouldNotContain(ScrubFixture.ClubUrl, Case.Insensitive);
        scrubbed.Text.ShouldNotContain(ScrubFixture.SiteName, Case.Insensitive);
        scrubbed.Text.ShouldNotContain(ScrubFixture.Manufacturer, Case.Insensitive);
        scrubbed.Text.ShouldNotContain(ScrubFixture.Model, Case.Insensitive);
        scrubbed.Text.ShouldNotContain("@");
        scrubbed.Text.ShouldNotContain("http", Case.Insensitive);

        // and the safety lesson is still there
        scrubbed.Text.ShouldContain("asymmetric");
        scrubbed.Text.ShouldContain("reserve");
        scrubbed.Text.ShouldContain("EN B");
    }

    // ---- guard rails -------------------------------------------------------

    [Fact]
    public void Given_no_vocabulary_When_a_scrub_is_constructed_Then_it_refuses()
    {
        // Given
        ScrubVocabulary? missing = null;

        // When / Then
        Should.Throw<ArgumentNullException>(() => new DeterministicScrub(missing!));
    }

    [Fact]
    public void Given_no_request_When_the_scrub_runs_Then_it_refuses()
    {
        // Given
        var scrub = ScrubFixture.Scrub();

        // When / Then
        Should.Throw<ArgumentNullException>(() => scrub.Scrub(null!));
    }

    [Fact]
    public void Given_a_blank_role_word_When_a_vocabulary_is_built_Then_it_refuses()
    {
        // Given
        const string blank = "   ";

        // When / Then
        Should.Throw<ArgumentException>(() => new ScrubVocabulary(blank, "the pilot"));
        Should.Throw<ArgumentException>(() => new ScrubVocabulary("the reporter", blank));
    }

    [Fact]
    public void Given_a_field_with_no_value_When_the_report_is_scrubbed_Then_the_field_is_dropped()
    {
        // Given
        var report = new ScrubRequest
        {
            Province = Province.Ontario,
            Fields =
            [
                new ScrubField(ScrubFieldKind.FreeText, "Damage", null),
                new ScrubField(ScrubFieldKind.FreeText, "Injury description", "   "),
                new ScrubField(ScrubFieldKind.Narrative, "Description", "A broken riser on inflation."),
            ],
        };

        // When
        var scrubbed = ScrubFixture.Scrub().Scrub(report);

        // Then
        scrubbed.Fields.Count.ShouldBe(1);
        scrubbed.Text.ShouldNotContain("Damage");
    }
}
