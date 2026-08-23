using HpacSafety.Core.Features.QuestionBank;
using HpacSafety.Core.Features.Reporting;
using HpacSafety.Core.SharedKernel;
using Shouldly;

namespace HpacSafety.Core.Tests;

/// <summary>
/// The reporter's own certification answer is the only source of a published
/// class. These tests pin that down from both sides: what the vocabulary
/// recognises, and — more importantly — everything it refuses to guess at.
/// See docs/aircraft-classification.md.
/// </summary>
public class AircraftClassifierTests
{
    private static readonly DateTimeOffset Now = new(2026, 8, 22, 12, 0, 0, TimeSpan.Zero);

    private readonly VocabularyAircraftClassifier _classifier = new();

    [Fact]
    public void Given_the_reporter_answered_low_B_When_the_class_is_resolved_Then_it_is_low_EN_B()
    {
        // Given
        const string answer = "low B";

        // When
        var classification = _classifier.Classify(answer, Discipline.Paragliding);

        // Then
        classification.Class.ShouldBe(AircraftClass.LowEnB);
    }

    [Fact]
    public void Given_an_unrecognised_answer_When_the_class_is_resolved_Then_it_is_class_not_determined()
    {
        // Given
        const string answer = "the blue one";

        // When
        var classification = _classifier.Classify(answer, Discipline.Paragliding);

        // Then
        classification.Class.ShouldBe(AircraftClass.NotDetermined);
        classification.IsDetermined.ShouldBeFalse();
    }

    [Theory]
    [InlineData("EN A", AircraftClass.EnA)]
    [InlineData("en-a", AircraftClass.EnA)]
    [InlineData("EN 926 A", AircraftClass.EnA)]
    [InlineData("low B", AircraftClass.LowEnB)]
    [InlineData("EN B (low)", AircraftClass.LowEnB)]
    [InlineData("low EN-B", AircraftClass.LowEnB)]
    [InlineData("B (high)", AircraftClass.HighEnB)]
    [InlineData("high EN B", AircraftClass.HighEnB)]
    [InlineData("EN C", AircraftClass.EnC)]
    [InlineData("en-d", AircraftClass.EnD)]
    [InlineData("CCC", AircraftClass.Ccc)]
    [InlineData("uncertified", AircraftClass.Uncertified)]
    [InlineData("not certified", AircraftClass.Uncertified)]
    [InlineData("prototype", AircraftClass.Uncertified)]
    public void Given_a_paraglider_certification_answer_When_the_class_is_resolved_Then_it_is_the_answered_class(
        string answer, AircraftClass expected)
    {
        // Given / When
        var classification = _classifier.Classify(answer, Discipline.Paragliding);

        // Then
        classification.Class.ShouldBe(expected);
    }

    [Theory]
    [InlineData("single surface", AircraftClass.SingleSurface)]
    [InlineData("single-surface", AircraftClass.SingleSurface)]
    [InlineData("double surface kingposted", AircraftClass.DoubleSurfaceKingposted)]
    [InlineData("kingpost", AircraftClass.DoubleSurfaceKingposted)]
    [InlineData("topless", AircraftClass.Topless)]
    [InlineData("Topless!", AircraftClass.Topless)]
    [InlineData("rigid", AircraftClass.Rigid)]
    [InlineData("uncertified", AircraftClass.Uncertified)]
    [InlineData("not certified", AircraftClass.Uncertified)]
    public void Given_a_hang_glider_certification_answer_When_the_class_is_resolved_Then_it_is_the_structural_class(
        string answer, AircraftClass expected)
    {
        // Given / When
        var classification = _classifier.Classify(answer, Discipline.HangGliding);

        // Then
        classification.Class.ShouldBe(expected);
    }

    [Fact]
    public void Given_a_hang_glider_answered_uncertified_When_the_class_is_resolved_Then_it_is_uncertified_and_never_an_EN_class()
    {
        // Given — uncertified hang gliders exist, and refusing the answer would lose a true one
        AircraftClass[] enClasses =
        [
            AircraftClass.EnA, AircraftClass.LowEnB, AircraftClass.HighEnB,
            AircraftClass.EnB, AircraftClass.EnC, AircraftClass.EnD, AircraftClass.Ccc,
        ];

        // When
        var classification = _classifier.Classify("uncertified", Discipline.HangGliding);

        // Then
        classification.Class.ShouldBe(AircraftClass.Uncertified);
        enClasses.ShouldNotContain(classification.Class);
    }

    [Fact]
    public void Given_a_hang_glider_that_is_both_uncertified_and_topless_When_it_is_resolved_Then_the_structural_class_wins()
    {
        // Given — the structural class is the more useful of the two answers

        // When
        var classification = _classifier.Classify("topless, uncertified", Discipline.HangGliding);

        // Then
        classification.Class.ShouldBe(AircraftClass.Topless);
    }

    [Theory]
    [InlineData("EN B (low)")]
    [InlineData("EN B")]
    [InlineData("EN A")]
    [InlineData("high B")]
    [InlineData("EN D")]
    [InlineData("CCC")]
    [InlineData("LTF 1-2")]
    public void Given_a_hang_glider_answered_with_an_EN_class_When_the_class_is_resolved_Then_no_EN_class_is_returned(string answer)
    {
        // Given — hang gliders are not EN-rated; the paraglider vocabulary must not transfer

        // When
        var classification = _classifier.Classify(answer, Discipline.HangGliding);

        // Then
        classification.Class.ShouldBe(AircraftClass.NotDetermined);
    }

    [Theory]
    [InlineData("topless")]
    [InlineData("rigid")]
    [InlineData("single surface")]
    [InlineData("double surface kingposted")]
    [InlineData("tandem")]
    [InlineData("uncertified")]
    [InlineData("n/a")]
    [InlineData("EN B")]
    [InlineData("Wills Wing T3")]
    public void Given_any_hang_glider_answer_When_the_class_is_resolved_Then_it_is_never_an_EN_class(string answer)
    {
        // Given
        AircraftClass[] enClasses =
        [
            AircraftClass.EnA, AircraftClass.LowEnB, AircraftClass.HighEnB,
            AircraftClass.EnB, AircraftClass.EnC, AircraftClass.EnD, AircraftClass.Ccc,
        ];

        // When
        var classification = _classifier.Classify(answer, Discipline.HangGliding);

        // Then
        enClasses.ShouldNotContain(classification.Class);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("n/a")]
    [InlineData("N/A")]
    [InlineData("na")]
    [InlineData("none")]
    [InlineData("unknown")]
    [InlineData("?")]
    [InlineData("-")]
    [InlineData("dont remember")]
    public void Given_a_non_answer_When_the_class_is_resolved_Then_it_is_class_not_determined(string? answer)
    {
        // Given / When
        var classification = _classifier.Classify(answer, Discipline.Paragliding);

        // Then
        classification.Class.ShouldBe(AircraftClass.NotDetermined);
        classification.Markers.ShouldBe(AircraftMarker.None);
    }

    [Theory]
    [InlineData("EN B")]
    [InlineData("en-b")]
    [InlineData("B")]
    public void Given_an_EN_B_answer_with_no_band_When_the_class_is_resolved_Then_it_is_plain_EN_B(string answer)
    {
        // Given — the reporter did answer, and "EN B" is a true answer

        // When
        var classification = _classifier.Classify(answer, Discipline.Paragliding);

        // Then — kept as given, and never widened into a band
        classification.Class.ShouldBe(AircraftClass.EnB);
    }

    [Fact]
    public void Given_a_plain_EN_B_answer_When_the_class_is_resolved_Then_neither_band_is_chosen()
    {
        // Given
        const string answer = "EN B";

        // When
        var classification = _classifier.Classify(answer, Discipline.Paragliding);

        // Then — the low/high split is the goal for new reports, not something to invent here
        classification.Class.ShouldNotBe(AircraftClass.LowEnB);
        classification.Class.ShouldNotBe(AircraftClass.HighEnB);
    }

    [Theory]
    [InlineData("LTF 1-2")]
    [InlineData("LTF 1/2")]
    [InlineData("DHV 1-2")]
    [InlineData("LTF 2")]
    public void Given_an_answer_in_the_LTF_scheme_When_the_class_is_resolved_Then_it_is_class_not_determined(string answer)
    {
        // Given — mapping LTF bands onto EN bands is a judgement HPAC has not made

        // When
        var classification = _classifier.Classify(answer, Discipline.Paragliding);

        // Then — a reviewer decides, the classifier never does
        classification.Class.ShouldBe(AircraftClass.NotDetermined);
    }

    [Fact]
    public void Given_a_make_and_model_as_the_certification_answer_When_the_class_is_resolved_Then_nothing_is_inferred_from_it()
    {
        // Given — there is no model-to-class lookup table in this system
        const string answer = "Ozone Rush 6";

        // When
        var classification = _classifier.Classify(answer, Discipline.Paragliding);

        // Then
        classification.Class.ShouldBe(AircraftClass.NotDetermined);
    }

    [Fact]
    public void Given_a_tandem_answer_carrying_a_class_When_it_is_resolved_Then_the_marker_accompanies_the_class()
    {
        // Given
        const string answer = "tandem, high EN-B";

        // When
        var classification = _classifier.Classify(answer, Discipline.Paragliding);

        // Then — the band survives the tandem marker
        classification.Class.ShouldBe(AircraftClass.HighEnB);
        classification.IsTandem.ShouldBeTrue();
        classification.Codes.ShouldBe(["tandem", "high_en_b"]);
    }

    [Theory]
    [InlineData(Discipline.Paragliding, AircraftClass.TandemParaglider)]
    [InlineData(Discipline.HangGliding, AircraftClass.TandemHangGlider)]
    public void Given_a_tandem_answer_with_no_class_When_it_is_resolved_Then_it_is_the_tandem_of_that_discipline(
        Discipline discipline, AircraftClass expected)
    {
        // Given / When
        var classification = _classifier.Classify("tandem", discipline);

        // Then
        classification.Class.ShouldBe(expected);
        classification.IsTandem.ShouldBeTrue();
        classification.Codes.ShouldBe([EnumCode.Of(expected)]);
    }

    [Fact]
    public void Given_a_tandem_answer_and_no_discipline_When_it_is_resolved_Then_the_discipline_is_not_guessed()
    {
        // Given / When
        var classification = _classifier.Classify("tandem", Discipline.Unknown);

        // Then — the marker is what the reporter said; the discipline is not invented
        classification.Class.ShouldBe(AircraftClass.NotDetermined);
        classification.IsTandem.ShouldBeTrue();
    }

    [Theory]
    [InlineData("mini wing", Discipline.Unknown, AircraftClass.MiniWing, AircraftMarker.MiniWing)]
    [InlineData("miniwing", Discipline.Unknown, AircraftClass.MiniWing, AircraftMarker.MiniWing)]
    [InlineData("speedwing", Discipline.Unknown, AircraftClass.Speedwing, AircraftMarker.Speedwing)]
    [InlineData("speed flying", Discipline.Unknown, AircraftClass.Speedwing, AircraftMarker.Speedwing)]
    [InlineData("", Discipline.MiniWing, AircraftClass.MiniWing, AircraftMarker.MiniWing)]
    [InlineData("", Discipline.Speedflying, AircraftClass.Speedwing, AircraftMarker.Speedwing)]
    public void Given_a_mini_wing_or_speedwing_answer_When_it_is_resolved_Then_the_wing_type_stands_in_for_the_class(
        string answer, Discipline discipline, AircraftClass expected, AircraftMarker marker)
    {
        // Given / When
        var classification = _classifier.Classify(answer, discipline);

        // Then
        classification.Class.ShouldBe(expected);
        classification.Markers.ShouldBe(marker);
    }

    [Fact]
    public void Given_a_mini_wing_that_carries_an_EN_class_When_it_is_resolved_Then_the_class_is_kept_with_the_marker()
    {
        // Given
        const string answer = "mini wing, EN A";

        // When
        var classification = _classifier.Classify(answer, Discipline.MiniWing);

        // Then
        classification.Class.ShouldBe(AircraftClass.EnA);
        classification.Markers.ShouldBe(AircraftMarker.MiniWing);
        classification.Codes.ShouldBe(["mini_wing", "en_a"]);
    }

    [Fact]
    public void Given_a_speedwing_that_carries_an_EN_class_When_it_is_resolved_Then_the_class_is_kept_with_the_marker()
    {
        // Given
        const string answer = "speed wing, EN A";

        // When
        var classification = _classifier.Classify(answer, Discipline.Speedflying);

        // Then
        classification.Class.ShouldBe(AircraftClass.EnA);
        classification.Markers.ShouldBe(AircraftMarker.Speedwing);
        classification.Codes.ShouldBe(["speedwing", "en_a"]);
    }

    [Theory]
    [InlineData("ENB low", AircraftClass.LowEnB)]
    [InlineData("enc", AircraftClass.EnC)]
    public void Given_the_class_written_without_a_separator_When_it_is_resolved_Then_it_still_normalizes(
        string answer, AircraftClass expected)
    {
        // Given / When
        var classification = _classifier.Classify(answer, Discipline.Paragliding);

        // Then
        classification.Class.ShouldBe(expected);
    }

    [Fact]
    public void Given_an_answer_naming_two_different_classes_When_it_is_resolved_Then_neither_is_chosen()
    {
        // Given
        const string answer = "EN A or EN C, cant remember";

        // When
        var classification = _classifier.Classify(answer, Discipline.Paragliding);

        // Then
        classification.Class.ShouldBe(AircraftClass.NotDetermined);
    }

    [Theory]
    [InlineData("it's a really nice wing, not sure of class")]
    [InlineData("I'd guess it was fine")]
    [InlineData("c'est un bon jour")]
    public void Given_prose_containing_a_stray_single_letter_When_it_is_resolved_Then_nothing_is_inferred_from_the_noise(string answer)
    {
        // Given — none of these sentences names a certification. A bare "a", "c",
        // or "d" produced by tokenizing an apostrophe or a foreign article is
        // noise, not an answer, and must never resolve to an EN class.

        // When
        var classification = _classifier.Classify(answer, Discipline.Paragliding);

        // Then
        classification.Class.ShouldBe(AircraftClass.NotDetermined);
    }

    [Theory]
    [InlineData("EN B", AircraftClass.EnB)]
    [InlineData("B (high)", AircraftClass.HighEnB)]
    [InlineData("low B", AircraftClass.LowEnB)]
    [InlineData("EN 926 A", AircraftClass.EnA)]
    public void Given_a_short_legitimate_certification_answer_When_it_is_resolved_Then_the_fix_for_stray_letters_does_not_break_it(
        string answer, AircraftClass expected)
    {
        // Given — the fix above must not cost a genuine short answer its class

        // When
        var classification = _classifier.Classify(answer, Discipline.Paragliding);

        // Then
        classification.Class.ShouldBe(expected);
    }

    [Fact]
    public void Given_an_undetermined_classification_When_its_codes_are_read_Then_it_says_so_rather_than_being_empty()
    {
        // Given / When
        var classification = _classifier.Classify("n/a", Discipline.Paragliding);

        // Then
        classification.Codes.ShouldBe(["not_determined"]);
    }

    [Fact]
    public void Given_an_aircraft_with_a_make_and_model_When_its_class_is_recorded_Then_neither_reaches_the_published_codes()
    {
        // Given
        var report = new Report(Locale.EnCa, Now);
        var aircraft = report.AddAircraft(Discipline.Paragliding, "Ozone", "Rush 6", "high B");

        // When
        aircraft.Classify(_classifier.Classify(aircraft.CertificationAnswer, aircraft.Discipline));
        var published = string.Join(" ", aircraft.Classification.Codes);

        // Then — make and model are Internal tier and never published
        published.ShouldBe("high_en_b");
        published.ShouldNotContain("ozone");
        published.ShouldNotContain("rush");
    }

    [Fact]
    public void Given_a_classified_aircraft_When_a_reviewer_corrects_it_by_hand_Then_the_correction_stands()
    {
        // Given — "class not determined" is correctable, never a guess
        var report = new Report(Locale.EnCa, Now);
        var aircraft = report.AddAircraft(Discipline.Paragliding, "Ozone", "Rush 6", "no idea");
        aircraft.Classify(_classifier.Classify(aircraft.CertificationAnswer, aircraft.Discipline));
        aircraft.Class.ShouldBe(AircraftClass.NotDetermined);

        // When
        aircraft.Classify(AircraftClass.HighEnB);

        // Then
        aircraft.Class.ShouldBe(AircraftClass.HighEnB);
        aircraft.Classification.Class.ShouldBe(AircraftClass.HighEnB);
    }

    [Fact]
    public void Given_the_reporter_wrote_the_answer_in_any_case_or_punctuation_When_it_is_resolved_Then_it_still_normalizes()
    {
        // Given
        string[] shapes = ["LOW EN-B", "  low   b  ", "en_b, low", "B(LOW)", "EN-B – low"];

        // When
        var classes = shapes.Select(shape => _classifier.Classify(shape, Discipline.Paragliding).Class);

        // Then
        classes.ShouldAllBe(c => c == AircraftClass.LowEnB);
    }

    [Fact]
    public void Given_an_answer_naming_both_bands_When_it_is_resolved_Then_it_is_plain_EN_B()
    {
        // Given — the reporter has not chosen a band, but they have told us it is a B
        const string answer = "low or high B, not sure";

        // When
        var classification = _classifier.Classify(answer, Discipline.Paragliding);

        // Then — the B survives; the band is not picked for them
        classification.Class.ShouldBe(AircraftClass.EnB);
    }
}
