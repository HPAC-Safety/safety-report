using HpacSafety.Core.Features.PrivateAttachments;
using HpacSafety.Core.Features.PrivateNotes;
using HpacSafety.Core.Features.Reporting;
using HpacSafety.Core.Tests.Media;
using Shouldly;

namespace HpacSafety.Core.Tests.PrivateAttachments;

/// <summary>
///     A staff private attachment (ADR-0135): any type up to a configurable cap,
///     stored in its report's private compartment, downloaded only through its own
///     link, soft-deleted on removal, and referable from a private note on the same
///     report only.
/// </summary>
public sealed class PrivateAttachmentTests
{
	private const string Officer = "officer:synthetic";
	private static readonly DateTimeOffset At = new(2026, 9, 26, 9, 0, 0, TimeSpan.Zero);
	private static readonly TinyId Report = TinyId.New();

	[Theory]
	[InlineData(0, MediaRejectionReason.Empty)]
	[InlineData(-1, MediaRejectionReason.Empty)]
	[InlineData(1, MediaRejectionReason.None)]
	[InlineData(1_000, MediaRejectionReason.None)]
	[InlineData(1_001, MediaRejectionReason.TooLarge)]
	public void GivenCap_WhenSizeIsJudged_ThenOnlyAboveZeroAndWithinCapPasses(long byteSize,
																		   MediaRejectionReason expected)
	{
		// Given
		var policy = new PrivateAttachmentPolicy(1_000);

		// When
		var verdict = policy.JudgeSize(byteSize);

		// Then
		verdict.ShouldBe(expected);
	}

	[Fact]
	public void GivenNoCap_WhenPolicyIsBuilt_ThenRefused()
	{
		// Then
		Should.Throw<ArgumentOutOfRangeException>(() => new PrivateAttachmentPolicy(0));
	}

	[Theory]
	[InlineData("application/zip", "application/zip")]
	[InlineData("Application/X-7z-Compressed; foo=bar", "application/x-7z-compressed")]
	[InlineData("video/x-matroska", "video/x-matroska")]
	[InlineData(null, "application/octet-stream")]
	[InlineData("", "application/octet-stream")]
	[InlineData("   ", "application/octet-stream")]
	[InlineData("malformed-type", "application/octet-stream")]
	[InlineData("text/html\r\nX-Evil: 1", "application/octet-stream")]
	[InlineData("a/b/c", "application/octet-stream")]
	public void GivenDeclaredType_WhenSigned_ThenWellFormedEssenceOrOctetStream(string? declared,
																				string expected)
	{
		// When
		var signed = PrivateAttachmentPolicy.ContentTypeFor(declared);

		// Then
		signed.ShouldBe(expected);
	}

	[Fact]
	public void GivenOverlongType_WhenSigned_ThenOctetStream()
	{
		// When
		var signed = PrivateAttachmentPolicy.ContentTypeFor($"application/{new string('x', 200)}");

		// Then
		signed.ShouldBe(PrivateAttachmentPolicy.FallbackContentType);
	}

	[Fact]
	public async Task GivenAnyTypeWithinCap_WhenPrivateUploadIsMinted_ThenOnePutToQuarantineAndNothingWritten()
	{
		// Given
		var store = new InMemoryBlobStore();
		var link = new UploadLink(store, ReporterPolicy(), new FixedClock(At));

		// When
		var minted = await link.MintPrivate(new PrivateAttachmentPolicy(5_000), "application/zip", 5_000, CancellationToken.None);

		// Then
		minted.IsMinted.ShouldBeTrue();
		minted.ContentType.ShouldBe("application/zip");
		minted.ExpiresAt.ShouldBe(At.Add(BlobUrlLifetime.Maximum));
		var url = minted.Url!.ToString();
		url.ShouldContain($"quarantine/{minted.UploadId.Value}?op=put");
		url.ShouldContain("ct=application%2Fzip&");
		url.ShouldContain("len=5000&");
		store.Keys.ShouldBeEmpty();
	}

	[Theory]
	[InlineData(0, MediaRejectionReason.Empty)]
	[InlineData(5_001, MediaRejectionReason.TooLarge)]
	public async Task GivenRefusedSize_WhenPrivateUploadIsMinted_ThenNothingIsMinted(long byteSize,
																					 MediaRejectionReason expected)
	{
		// Given
		var link = new UploadLink(new InMemoryBlobStore(), ReporterPolicy(), new FixedClock(At));

		// When
		var minted = await link.MintPrivate(new PrivateAttachmentPolicy(5_000), "application/zip", byteSize, CancellationToken.None);

		// Then
		minted.IsMinted.ShouldBeFalse();
		minted.RejectionReason.ShouldBe(expected);
		minted.Url.ShouldBeNull();
		minted.ContentType.ShouldBeNull();
	}

	[Fact]
	public async Task GivenNoPolicy_WhenPrivateUploadIsMinted_ThenRefused()
	{
		// Given
		var link = new UploadLink(new InMemoryBlobStore(), ReporterPolicy(), new FixedClock(At));

		// When / Then
		await Should.ThrowAsync<ArgumentNullException>(() => link.MintPrivate(null!, "application/zip", 1, CancellationToken.None));
	}

	[Fact]
	public void GivenPrivateKey_WhenBuiltAndParsed_ThenRoundTripsUnderReport()
	{
		// Given
		var id = TinyId.New();

		// When
		var key = BlobKey.For(Report.Value, MediaCompartment.Private, id.Value);

		// Then
		key.Value.ShouldBe($"{Report.Value}/private/{id.Value}");
		BlobKey.Parse(key.Value).ShouldBe(key);
		key.AcceptsDirectUpload.ShouldBeFalse();
		key.In(MediaCompartment.Original).Value.ShouldBe($"{Report.Value}/original/{id.Value}");
	}

	[Fact]
	public void GivenValidClaim_WhenAdded_ThenNameSanitizedDescriptionTrimmedAndAdderRecorded()
	{
		// When
		var attachment = Added(fileName: "C:\\Users\\x\\Coroner: report?.zip", description: "  Received from the coroner.  ");

		// Then
		attachment.OriginalFileName.ShouldBe("Coroner report.zip");
		attachment.Description.ShouldBe("Received from the coroner.");
		attachment.AddedBySubject.ShouldBe(Officer);
		attachment.AddedAt.ShouldBe(At);
		attachment.ContentType.ShouldBe("application/zip");
		attachment.ByteSize.ShouldBe(42);
		attachment.BlobKey.ShouldBe($"{Report.Value}/private/{attachment.Id.Value}");
		attachment.Deleted.ShouldBeNull();
		attachment.DeletedBySubject.ShouldBeNull();
	}

	[Theory]
	[InlineData(null)]
	[InlineData("")]
	[InlineData("   ")]
	public void GivenNoDescription_WhenAdded_ThenNoneIsStored(string? description)
	{
		// When
		var attachment = Added(description: description);

		// Then
		attachment.Description.ShouldBeNull();
	}

	[Theory]
	[InlineData(null)]
	[InlineData("")]
	[InlineData("\"/:*?<>|")]
	[InlineData("...")]
	public void GivenUnusableName_WhenAdded_ThenRefused(string? fileName)
	{
		// Then
		Should.Throw<DomainRuleViolationException>(() => Added(fileName: fileName));
	}

	[Fact]
	public void GivenOverlongDescription_WhenAdded_ThenRefused()
	{
		// Then
		Should.Throw<DomainRuleViolationException>(() => Added(description: new string('a', PrivateAttachment.DescriptionMaxLength + 1)));
		Added(description: new string('a', PrivateAttachment.DescriptionMaxLength)).Description!.Length.ShouldBe(PrivateAttachment.DescriptionMaxLength);
	}

	[Fact]
	public void GivenEmptyFile_WhenAdded_ThenRefused()
	{
		// Then
		Should.Throw<DomainRuleViolationException>(() => Added(byteSize: 0));
	}

	[Fact]
	public void GivenKeyOutsidePrivateCompartmentOrForAnotherReportOrId_WhenAdded_ThenRefused()
	{
		// Given
		var id = TinyId.New();
		BlobKey[] wrong =
		[
			BlobKey.For(Report.Value, MediaCompartment.Original, id.Value),
			BlobKey.For(TinyId.New().Value, MediaCompartment.Private, id.Value),
			BlobKey.For(Report.Value, MediaCompartment.Private, TinyId.New().Value),
			BlobKey.ForUpload(UploadId.New()),
		];

		// Then
		foreach (var key in wrong)
		{
			Should.Throw<DomainRuleViolationException>(() =>
				PrivateAttachment.Add(id, Report, key, "a.zip", "application/zip", 1, null, Officer, At));
		}
	}

	[Fact]
	public void GivenAttachment_WhenRemovedTwice_ThenFirstRemovalIsKept()
	{
		// Given
		var attachment = Added();

		// When
		attachment.Remove("admin:synthetic", At.AddHours(1));
		attachment.Remove("admin:other", At.AddHours(2));

		// Then
		attachment.Deleted.ShouldBe(At.AddHours(1));
		attachment.DeletedBySubject.ShouldBe("admin:synthetic");
	}

	[Fact]
	public async Task GivenPrivateKey_WhenDownloadLinkIsRequested_ThenForcedDownloadUnderItsName()
	{
		// Given
		var attachment = Added();

		// When
		var url = await new PrivateAttachmentLink(new InMemoryBlobStore())
			.CreateDownloadUrl(BlobKey.Parse(attachment.BlobKey), attachment.OriginalFileName, BlobUrlLifetime.Maximum, CancellationToken.None);

		// Then
		url.AbsoluteUri.ShouldContain($"{attachment.BlobKey}?op=get&fn=Coroner%20report.zip&ttl=900");
	}

	[Theory]
	[InlineData(MediaCompartment.Original)]
	[InlineData(MediaCompartment.Stripped)]
	[InlineData(MediaCompartment.Quarantine)]
	public async Task GivenKeyOutsidePrivateCompartment_WhenDownloadLinkIsRequested_ThenRefused(MediaCompartment compartment)
	{
		// Given
		var key = compartment == MediaCompartment.Quarantine
			? BlobKey.ForUpload(UploadId.New())
			: BlobKey.For(Report.Value, compartment, "report.pdf");

		// When / Then
		await Should.ThrowAsync<DomainRuleViolationException>(() =>
			new PrivateAttachmentLink(new InMemoryBlobStore()).CreateDownloadUrl(key, "report.pdf", BlobUrlLifetime.Maximum, CancellationToken.None));
	}

	[Fact]
	public async Task GivenLongerLifetime_WhenDownloadLinkIsRequested_ThenRefused()
	{
		// Given
		var attachment = Added();

		// When / Then
		await Should.ThrowAsync<DomainRuleViolationException>(() =>
			new PrivateAttachmentLink(new InMemoryBlobStore())
				.CreateDownloadUrl(BlobKey.Parse(attachment.BlobKey), "a.zip", BlobUrlLifetime.Maximum.Add(TimeSpan.FromSeconds(1)), CancellationToken.None));
	}

	[Fact]
	public void GivenAttachmentOnSameReport_WhenNoteIsWrittenAndEdited_ThenEachRevisionKeepsItsOwnReference()
	{
		// Given
		var attachment = Added();

		// When
		var note = PrivateNote.Write(Report, Officer, "Synthetic: see the coroner's report.", At, attachment);
		note.Edit(Officer, "Synthetic: no longer relevant.", 1, At.AddMinutes(1));

		// Then
		note.Revisions[0].AttachmentId.ShouldBe(attachment.Id);
		note.Current.AttachmentId.ShouldBeNull();
	}

	[Fact]
	public void GivenAttachmentOnAnotherReport_WhenNoteRefersToIt_ThenRefused()
	{
		// Given
		var elsewhere = Added(report: TinyId.New());
		var note = PrivateNote.Write(Report, Officer, "Synthetic: a note.", At);

		// Then
		Should.Throw<DomainRuleViolationException>(() => PrivateNote.Write(Report, Officer, "Synthetic: a note.", At, elsewhere));
		Should.Throw<DomainRuleViolationException>(() => note.Edit(Officer, "Synthetic: an edit.", 1, At, elsewhere));
		note.Revisions.Count.ShouldBe(1);
	}

	[Fact]
	public void GivenRemovedAttachment_WhenNoteRefersToIt_ThenRefused()
	{
		// Given
		var removed = Added();
		removed.Remove(Officer, At);

		// Then
		Should.Throw<DomainRuleViolationException>(() => PrivateNote.Write(Report, Officer, "Synthetic: a note.", At, removed));
	}

	private static MediaPolicy ReporterPolicy()
	{
		return new MediaPolicy(MediaSizeLimits.Uniform(10), MediaType.All);
	}

	private static PrivateAttachment Added(string? fileName = "Coroner: report?.zip",
										   string? description = null,
										   long byteSize = 42,
										   TinyId? report = null)
	{
		var reportId = report ?? Report;
		var id = TinyId.New();
		return PrivateAttachment.Add(
			id,
			reportId,
			BlobKey.For(reportId.Value, MediaCompartment.Private, id.Value),
			fileName,
			"application/zip",
			byteSize,
			description,
			Officer,
			At);
	}
}
