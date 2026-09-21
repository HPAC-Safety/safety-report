using Shouldly;

namespace HpacSafety.Core.Tests;

/// <summary>
/// A blob key is the only thing standing between an attacker-supplied string and
/// the filesystem in <c>FileSystemBlobStore</c>, and it is also where the storage
/// layout stops being a convention and becomes a rule: a key that is not
/// namespaced by a report id cannot be constructed. See ADR-0026.
/// </summary>
public class BlobKeyTests
{
    private const string ReportId = "dQw4w9WgXcQ";

    [Fact]
    public void GivenReportsMedia_WhenKeyIsBuilt_ThenReportIdIsTopLevelDirectory()
    {
        // Given / When
        var original = BlobKey.For(ReportId, MediaCompartment.Original, "photo.jpg");
        var stripped = BlobKey.For(ReportId, MediaCompartment.Stripped, "photo.jpg");

        // Then
        original.Value.ShouldBe("dQw4w9WgXcQ/original/photo.jpg");
        stripped.Value.ShouldBe("dQw4w9WgXcQ/stripped/photo.jpg");
    }

    [Fact]
    public void GivenUnverifiedUpload_WhenKeyIsBuilt_ThenQuarantineIsTopLevelDirectory()
    {
        // Given / When
        var key = BlobKey.For(ReportId, MediaCompartment.Quarantine, "photo.jpg");

        // Then
        // Quarantine sits above the report id so that one literal prefix expires
        // every unverified upload. An S3 lifecycle filter cannot express
        // "*/quarantine/". See ADR-0026.
        key.Value.ShouldBe("quarantine/dQw4w9WgXcQ/photo.jpg");
        key.Value.ShouldStartWith("quarantine/");
    }

    [Theory]
    [InlineData("dQw4w9WgXcQ/original/photo.jpg", MediaCompartment.Original)]
    [InlineData("dQw4w9WgXcQ/stripped/photo.jpg", MediaCompartment.Stripped)]
    [InlineData("quarantine/dQw4w9WgXcQ/photo.jpg", MediaCompartment.Quarantine)]
    public void GivenStoredKey_WhenParsed_ThenReportAndCompartmentRoundTrip(string candidate, MediaCompartment expected)
    {
        // Given / When
        var key = BlobKey.Parse(candidate);

        // Then
        key.Value.ShouldBe(candidate);
        key.Compartment.ShouldBe(expected);
        key.ReportId.ShouldBe(ReportId);
        key.FileName.ShouldBe("photo.jpg");
    }

    [Theory]
    // Traversal, in every shape the filesystem store must never see.
    [InlineData("../../etc/passwd")]
    [InlineData("dQw4w9WgXcQ/original/../../../etc/passwd")]
    [InlineData("dQw4w9WgXcQ/original/..")]
    [InlineData("dQw4w9WgXcQ/original/.")]
    [InlineData("/dQw4w9WgXcQ/original/photo.jpg")]
    [InlineData("dQw4w9WgXcQ/original/photo.jpg/")]
    [InlineData("dQw4w9WgXcQ//photo.jpg")]
    [InlineData("dQw4w9WgXcQ/original/photo.jpg\\x")]
    [InlineData("dQw4w9WgXcQ/original/pho to.jpg")]
    [InlineData("dQw4w9WgXcQ/original/pho\nto.jpg")]
    [InlineData("dQw4w9WgXcQ/original/.hidden")]
    // Not namespaced by a report at all.
    [InlineData("photo.jpg")]
    [InlineData("original/photo.jpg")]
    [InlineData("reports/9f1c8a/photo.jpg")]
    [InlineData("dQw4w9WgXcQ/photo.jpg")]
    // A compartment this system does not have.
    [InlineData("dQw4w9WgXcQ/thumbnails/photo.jpg")]
    [InlineData("dQw4w9WgXcQ/Original/photo.jpg")]
    // Report ids that are not tiny ids: wrong length, wrong alphabet.
    [InlineData("short/original/photo.jpg")]
    [InlineData("dQw4w9WgXcQextra/original/photo.jpg")]
    [InlineData("dQw4w9WgXc./original/photo.jpg")]
    [InlineData("quarantine/short/photo.jpg")]
    [InlineData("")]
    [InlineData(null)]
    public void GivenKeyIsNotOneOfThreeShapes_WhenParsed_ThenRefused(string? candidate)
    {
        // Given / When
        var parsed = BlobKey.TryParse(candidate, out _);

        // Then
        parsed.ShouldBeFalse();
        Should.Throw<DomainRuleViolationException>(() => BlobKey.Parse(candidate));
    }

    [Fact]
    public void GivenKeyOnlyLooksLikeDerivative_WhenParsed_ThenNotStrippedKey()
    {
        // Given
        // "strippedish" is not "stripped". The compartment is a whole segment,
        // not a string prefix, and a near-miss must not read as a derivative.
        var parsed = BlobKey.TryParse("dQw4w9WgXcQ/strippedish/photo.jpg", out _);

        // Then
        parsed.ShouldBeFalse();
    }

    [Fact]
    public void GivenReportIdIsNotTinyId_WhenKeyIsBuilt_ThenRefused()
    {
        // Given / When / Then
        // Identifiers here are 11 characters of A-Za-z0-9-_ — see ADR-0026.
        Should.Throw<DomainRuleViolationException>(() => BlobKey.For("too-short", MediaCompartment.Original, "photo.jpg"));
        Should.Throw<DomainRuleViolationException>(() => BlobKey.For("dQw4w9WgXcQtoolong", MediaCompartment.Original, "photo.jpg"));
        Should.Throw<DomainRuleViolationException>(() => BlobKey.For("dQw4w9WgXc/", MediaCompartment.Original, "photo.jpg"));
    }

    [Fact]
    public void GivenFileNameLongerThanLimit_WhenKeyIsBuilt_ThenRefused()
    {
        // Given
        var fileName = new string('a', BlobKey.MaxFileNameLength + 1);

        // When / Then
        Should.Throw<DomainRuleViolationException>(() => BlobKey.For(ReportId, MediaCompartment.Original, fileName));
    }

    [Theory]
    [InlineData("UPPER.jpg")]
    [InlineData("123.jpg")]
    [InlineData("dash-name.jpg")]
    [InlineData("under_score.jpg")]
    public void GivenFileNameUsingAllowedAlphabet_WhenKeyIsBuilt_ThenPreserved(string fileName)
    {
        // Given / When
        var key = BlobKey.For(ReportId, MediaCompartment.Original, fileName);

        // Then
        key.FileName.ShouldBe(fileName);
    }

    [Fact]
    public void GivenQuarantinedUpload_WhenMovesCompartment_ThenReportAndFileAreCarriedAcross()
    {
        // Given
        var quarantined = BlobKey.For(ReportId, MediaCompartment.Quarantine, "photo.jpg");

        // When
        var original = quarantined.In(MediaCompartment.Original);
        var stripped = quarantined.In(MediaCompartment.Stripped);

        // Then
        original.Value.ShouldBe("dQw4w9WgXcQ/original/photo.jpg");
        stripped.Value.ShouldBe("dQw4w9WgXcQ/stripped/photo.jpg");
        original.ReportId.ShouldBe(quarantined.ReportId);
        stripped.FileName.ShouldBe(quarantined.FileName);
        original.ShouldNotBe(stripped);
    }

    [Fact]
    public void GivenTwoReports_WhenTheirKeysAreBuilt_ThenNeitherCanReachOthersDirectory()
    {
        // Given
        var mine = BlobKey.For(ReportId, MediaCompartment.Original, "photo.jpg");
        var theirs = BlobKey.For("kJQP7kiw5Fk", MediaCompartment.Original, "photo.jpg");

        // When / Then
        mine.Value.ShouldNotBe(theirs.Value);
        mine.Value.ShouldStartWith(ReportId + "/");
        theirs.Value.ShouldStartWith("kJQP7kiw5Fk/");
    }
}
