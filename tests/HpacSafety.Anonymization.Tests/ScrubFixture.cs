using HpacSafety.Core.Features.Anonymization;
using HpacSafety.Core.Features.Reporting;

namespace HpacSafety.Anonymization.Tests;

/// <summary>
/// Fixture data for the golden-file suite. <b>Every value here is invented.</b>
/// No real report content, no real pilot, no real launch site, no real HPAC
/// member number, and no real aircraft brand appears in this repository — see
/// tests/README.md. Domains are RFC 2606 reserved (<c>example.com</c>,
/// <c>example.org</c>) so no fixture can ever resolve to somebody's site.
/// </summary>
internal static class ScrubFixture
{
    internal const string ReporterFirstName = "Marc";
    internal const string ReporterLastName = "Delacroix";
    internal const string ReporterFullName = $"{ReporterFirstName} {ReporterLastName}";

    internal const string PilotFirstName = "Sarah";
    internal const string PilotLastName = "Whitlock";
    internal const string PilotFullName = $"{PilotFirstName} {PilotLastName}";

    internal const string ReporterEmail = "marc.delacroix@example.com";
    internal const string ReporterPhone = "403-555-0142";
    internal const string MemberNumber = "48213";
    internal const string ClubUrl = "https://www.ferndale-freeflight.example.org/logbook/2026";
    internal const string SiteName = "Mount Ferndale";
    internal const string LandingZoneName = "Kettle Flats";
    internal const string Manufacturer = "Vantara";
    internal const string Model = "Halcyon 3";

    /// <summary>The scrub under test, wired with the English role words.</summary>
    internal static DeterministicScrub Scrub() => new(ScrubVocabulary.EnglishCanada);

    /// <summary>
    /// A report carrying every structured identifying field the form collects,
    /// with the narrative supplied by the caller.
    /// </summary>
    internal static ScrubRequest Report(string narrative) => new()
    {
        Province = Province.BritishColumbia,
        Fields =
        [
            new ScrubField(ScrubFieldKind.ReporterName, "From", ReporterFullName),
            new ScrubField(ScrubFieldKind.ContactDetail, "Phone number", ReporterPhone),
            new ScrubField(ScrubFieldKind.ContactDetail, "Email", ReporterEmail),
            new ScrubField(ScrubFieldKind.MemberIdentifier, "HPAC member number", MemberNumber),
            new ScrubField(ScrubFieldKind.PilotName, "Pilot", PilotFullName),
            new ScrubField(ScrubFieldKind.Location, "Where", SiteName),
            new ScrubField(ScrubFieldKind.AircraftIdentity, "Manufacturer", Manufacturer),
            new ScrubField(ScrubFieldKind.AircraftIdentity, "Model", Model),
            new ScrubField(ScrubFieldKind.FreeText, "Certification", "EN B"),
            new ScrubField(ScrubFieldKind.Narrative, "Description", narrative),
        ],
    };

    /// <summary>A report whose only content is one free-text field.</summary>
    internal static ScrubRequest NarrativeOnly(string narrative) => new()
    {
        Province = Province.BritishColumbia,
        Fields = [new ScrubField(ScrubFieldKind.Narrative, "Description", narrative)],
    };
}
