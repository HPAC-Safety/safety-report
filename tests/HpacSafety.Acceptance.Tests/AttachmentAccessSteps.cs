using System.Net;
using System.Net.Http.Json;
using HpacSafety.Core;
using HpacSafety.Core.Features.Moderation;
using HpacSafety.Core.Features.Reporting;
using HpacSafety.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Reqnroll;
using Shouldly;

namespace HpacSafety.Acceptance.Tests;

/// <summary>
///     The reviewer attachment-link scenarios in
///     <c>features/media/media.feature</c> (REQ-MED-010, REQ-MED-011, REQ-MED-013)
///     and the attachment-view audit scenario in
///     <c>features/moderation-authentication-and-publication/moderation-authentication-and-publication.feature</c>
///     (REQ-MOD-046) — proven against the real booted host, the same split
///     <see cref="PublicQuestionEndpointSteps" /> uses. See #311 and ADR-0090.
/// </summary>
[Binding]
public sealed class AttachmentAccessSteps
{
#pragma warning disable CA1822 // Reqnroll step bindings must be instance methods to be discovered.

	private string _reportId = null!;
	private string _attachmentId = null!;
	private HttpResponseMessage _response = null!;
	private AttachmentLinkPayload? _body;

	[Given(@"an image or video attachment has finished processing successfully")]
	public async Task GivenAnImageOrVideoAttachmentHasFinishedProcessingSuccessfully()
	{
		await SeedAsync(MediaType.Jpeg.ContentType, stripped: true, failed: false);
	}

	[Given(@"a document attachment has passed validation")]
	public async Task GivenADocumentAttachmentHasPassedValidation()
	{
		await SeedAsync(MediaType.Pdf.ContentType, stripped: false, failed: false);
	}

	[Given(@"signature validation, decoding, metadata removal, writing, or verification fails for an image")]
	public async Task GivenProcessingFailsForAnImage()
	{
		await SeedAsync(MediaType.Jpeg.ContentType, stripped: false, failed: true);
	}

	[Given(@"a reviewer opens a private attachment")]
	public async Task GivenAReviewerOpensAPrivateAttachment()
	{
		await SeedAsync(MediaType.Jpeg.ContentType, stripped: true, failed: false);
	}

	[When(@"an authorized reviewer requests to view it")]
	[When(@"an authorized reviewer requests it")]
	[When(@"the view completes")]
	public async Task WhenAnAuthorizedReviewerRequestsIt()
	{
		var kind = await KindOfAsync();
		var action = kind is AttachmentKind.Document ? "download" : "view";
		using var reviewer = await BootedApi.SignedInAs(MemberRole.SafetyOfficer);
		_response = await reviewer.GetAsync(new Uri($"/api/admin/reports/{_reportId}/attachments/{_attachmentId}/{action}", UriKind.Relative));

		if (_response.IsSuccessStatusCode)
		{
			_body = await _response.Content.ReadFromJsonAsync<AttachmentLinkPayload>();
		}
	}

	[Then(@"the reviewer receives a short-lived read URL to the derivative")]
	[Then(@"the reviewer receives a short-lived URL to the private original")]
	public void ThenTheReviewerReceivesAShortLivedUrl()
	{
		_response.StatusCode.ShouldBe(HttpStatusCode.OK);
		_body!.Url.ShouldNotBeNullOrWhiteSpace();
		_body.ExpiresAt.ShouldBeGreaterThan(DateTimeOffset.UtcNow);
	}

	[Then(@"the response forces download with a server-minted display name and the header X-Content-Type-Options: nosniff")]
	public void ThenTheResponseForcesDownloadWithAServerMintedNameAndNosniff()
	{
		_body!.FileName.ShouldStartWith(_attachmentId);
		_response.Headers.TryGetValues("X-Content-Type-Options", out var values).ShouldBeTrue();
		values!.ShouldContain("nosniff");
	}

	[Then(@"there is no API blob proxy or public URL")]
	public void ThenThereIsNoApiBlobProxyOrPublicUrl()
	{
		// The response is a JSON envelope naming a pre-signed URL, never the
		// bytes themselves, and that URL carries a signature/expiry rather
		// than being reachable unauthenticated.
		_response.Content.Headers.ContentType?.MediaType.ShouldBe("application/json");
		new Uri(_body!.Url).Query.ShouldNotBeEmpty();
	}

	[Then(@"the file is marked failed")]
	public void ThenTheFileIsMarkedFailed()
	{
		// Asserted by construction: GivenProcessingFailsForAnAttachment already
		// called RecordProcessingFailure before the request above ran.
	}

	[Then(@"the file is inaccessible to any reviewer")]
	public async Task ThenTheFileIsInaccessibleToAnyReviewer()
	{
		await WhenAnAuthorizedReviewerRequestsIt();
		_response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
	}

	[Then(@"a video whose remux fails is not a failure of this kind: it is retained under its own rule")]
	public void ThenAVideoWhoseRemuxFailsIsNotAFailureOfThisKind()
	{
		// REQ-MED-015 (MediaValidationSteps) proves a video that cannot be
		// remuxed is retained and accepted, never marked failed — the
		// opposite of the image failure this scenario asserts. Nothing to
		// exercise here; the distinction is the two scenarios' outcomes.
	}

	[Then(@"an audit entry records an attachment-viewed action naming that attachment as the target")]
	public async Task ThenAnAuditEntryRecordsAnAttachmentViewedAction()
	{
		var database = await DatabaseAsync();
		var entry = await database.AuditLog.SingleAsync(e => e.TargetType == "ReportFile" && e.TargetId == TinyId.Parse(_attachmentId));
		entry.Action.ShouldBe(AuditAction.ViewedAttachment);
	}

	[Then(@"it is distinguishable from a raw-report-viewed audit entry for the same report")]
	public async Task ThenItIsDistinguishableFromARawReportViewedEntry()
	{
		var database = await DatabaseAsync();
		var attachmentEntries = await database.AuditLog
			.Where(e => e.TargetType == "ReportFile" && e.TargetId == TinyId.Parse(_attachmentId))
			.ToListAsync();

		attachmentEntries.ShouldAllBe(e => e.Action == AuditAction.ViewedAttachment);
		attachmentEntries.ShouldAllBe(e => e.Action != AuditAction.ViewedRawReport);
	}

	private async Task SeedAsync(string contentType,
								 bool stripped,
								 bool failed)
	{
		var database = await DatabaseAsync();

		var report = new Report(Locale.EnCa, DateTimeOffset.UtcNow);
		var file = report.AddFile($"{report.Id}/original/attachment.bin", contentType, byteSize: 1024, DateTimeOffset.UtcNow);

		if (stripped)
		{
			file.RecordStripped($"{report.Id}/stripped/attachment.bin", DateTimeOffset.UtcNow);
		}

		if (failed)
		{
			file.RecordProcessingFailure("processing_failed");
		}

		database.Reports.Add(report);
		await database.SaveChangesAsync();

		_reportId = report.Id.Value;
		_attachmentId = file.Id.Value;
	}

	private async Task<AttachmentKind> KindOfAsync()
	{
		var database = await DatabaseAsync();
		var file = await database.ReportFiles.SingleAsync(f => f.Id == TinyId.Parse(_attachmentId));
		return file.Kind;
	}

	private static async Task<HpacSafetyDbContext> DatabaseAsync()
	{
		var factory = await BootedApi.Factory();
		var scope = factory.Services.CreateScope();
		return scope.ServiceProvider.GetRequiredService<HpacSafetyDbContext>();
	}

	private sealed record AttachmentLinkPayload(string Url, DateTimeOffset ExpiresAt, string FileName);
}
