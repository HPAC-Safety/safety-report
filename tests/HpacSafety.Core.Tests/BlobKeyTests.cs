using Shouldly;

namespace HpacSafety.Core.Tests;

/// <summary>
///     A blob key is the only thing standing between an attacker-supplied string and
///     the bucket, and it is also where the storage
///     layout stops being a convention and becomes a rule: a key that is not
///     namespaced by a report id — or, in quarantine, by an upload id — cannot be
///     constructed. See ADR-0026 and ADR-0096.
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
	public void GivenUnclaimedUpload_WhenKeyIsBuilt_ThenQuarantineIsTopLevelAndNamedOnlyByUploadId()
	{
		// Given
		var upload = UploadId.New();

		// When
		var key = BlobKey.ForUpload(upload);

		// Then
		// Quarantine sits at the top so that one literal prefix expires every
		// unclaimed upload. An S3 lifecycle filter cannot express
		// "*/quarantine/". See ADR-0026 and ADR-0096.
		key.Value.ShouldBe($"quarantine/{upload.Value}");
		key.Compartment.ShouldBe(MediaCompartment.Quarantine);
		key.ReportId.ShouldBeNull();
	}

	[Fact]
	public void GivenStoredUploadKey_WhenParsed_ThenRoundTrips()
	{
		// Given
		var upload = UploadId.New();

		// When
		var key = BlobKey.Parse($"quarantine/{upload.Value}");

		// Then
		key.ShouldBe(BlobKey.ForUpload(upload));
		key.FileName.ShouldBe(upload.Value);
	}

	[Fact]
	public void GivenQuarantineCompartment_WhenKeyIsBuiltForReport_ThenRefused()
	{
		// Given / When / Then
		// An upload waits in quarantine before any report exists; a report never
		// owns a quarantine key.
		Should.Throw<DomainRuleViolationException>(() => BlobKey.For(ReportId, MediaCompartment.Quarantine, "photo.jpg"));
	}

	[Fact]
	public void GivenUnclaimedUpload_WhenAskedForAnotherCompartment_ThenRefused()
	{
		// Given
		var key = BlobKey.ForUpload(UploadId.New());

		// When / Then
		Should.Throw<DomainRuleViolationException>(() => key.In(MediaCompartment.Original));
	}

	[Fact]
	public void GivenDefaultUploadId_WhenKeyIsBuilt_ThenRefused()
	{
		// Given / When / Then
		Should.Throw<DomainRuleViolationException>(() => BlobKey.ForUpload(default));
	}

	[Theory]
	[InlineData("dQw4w9WgXcQ/original/photo.jpg", MediaCompartment.Original)]
	[InlineData("dQw4w9WgXcQ/stripped/photo.jpg", MediaCompartment.Stripped)]
	public void GivenStoredKey_WhenParsed_ThenReportAndCompartmentRoundTrip(string candidate,
																			MediaCompartment expected)
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
	// Quarantine is named by an upload id alone: the old report-shaped key, a
	// short id, and anything nested are all refused.
	[InlineData("quarantine/dQw4w9WgXcQ/photo.jpg")]
	[InlineData("quarantine/tooshort")]
	[InlineData("quarantine/")]
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
	public void GivenOriginal_WhenMovesCompartment_ThenReportAndFileAreCarriedAcross()
	{
		// Given
		var original = BlobKey.For(ReportId, MediaCompartment.Original, "photo.jpg");

		// When
		var stripped = original.In(MediaCompartment.Stripped);

		// Then
		stripped.Value.ShouldBe("dQw4w9WgXcQ/stripped/photo.jpg");
		stripped.ReportId.ShouldBe(original.ReportId);
		stripped.FileName.ShouldBe(original.FileName);
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
