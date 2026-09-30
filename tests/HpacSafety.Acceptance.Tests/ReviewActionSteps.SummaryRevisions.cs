using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using HpacSafety.Api.Authentication;
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
///     The summary's append-only revisions through the booted API: an edit adds
///     one with its author, a rollback adds one that copies an earlier one, a live
///     report publishes what is saved, and the public reads only the latest
///     approved revision (REQ-MOD-032, REQ-MOD-194..201, REQ-MOD-205, ADR-0177).
/// </summary>
public sealed partial class ReviewActionSteps
{
	private const string SeededEn = "The pilot landed in a field.";
	private const string SeededFr = "Le pilote s'est posé dans un champ.";
	private const string EditedEn = "The pilot landed firmly in a field.";
	private const string EditedFr = "Le pilote s'est posé fermement dans un champ.";

	private DateTimeOffset? _publishedAtBefore;
	private string _draftEn = string.Empty;
	private JsonElement _publicView;
	private HttpStatusCode _publicStatus;

	// ── Given ───────────────────────────────────────────────────────────────

	[Given(@"a reviewer has edited either summary language")]
	public async Task GivenAReviewerHasEdited()
	{
		await EditThroughTheApi();
	}

	[Given(@"a reviewer has restored the first revision")]
	public async Task GivenAReviewerHasRestoredTheFirstRevision()
	{
		await Restore(FirstRevisionId(await ReadDetail()));
		_response!.StatusCode.ShouldBe(HttpStatusCode.OK);
	}

	[Given(@"a newer revision that nobody has approved exists")]
	public async Task GivenANewerUnapprovedRevisionExists()
	{
		// Written past the domain, which never leaves a Published report's latest
		// revision unapproved: the public read is what must hold regardless.
		_draftEn = "An unapproved draft nobody has read.";
		var factory = await BootedApi.Factory();
		await using var scope = factory.Services.CreateAsyncScope();
		var database = scope.ServiceProvider.GetRequiredService<HpacSafetyDbContext>();

		await database.Database.ExecuteSqlInterpolatedAsync(
			$"""
			 INSERT INTO summary_revisions
			     (id, summary_id, sequence, ai_summary_en, ai_summary_fr, source_en, source_fr, model, prompt_version, author_subject, created_at)
			 SELECT {TinyId.New().Value}, revision.summary_id, revision.sequence + 1, {_draftEn}, 'Un brouillon non approuvé.',
			        'human', 'human', revision.model, revision.prompt_version, 'synthetic-editor', now()
			 FROM summary_revisions AS revision
			          JOIN summaries AS summary ON summary.id = revision.summary_id
			 WHERE summary.report_id = {_reportId}
			 ORDER BY revision.sequence DESC
			 LIMIT 1
			 """);
	}

	// ── When ────────────────────────────────────────────────────────────────

	[When(@"a reviewer edits either summary language")]
	public async Task WhenAReviewerEdits()
	{
		await EditThroughTheApi();
		_response!.StatusCode.ShouldBe(HttpStatusCode.OK);
	}

	[When(@"a reviewer restores the first revision")]
	public async Task WhenAReviewerRestoresTheFirstRevision()
	{
		await Restore(FirstRevisionId(await ReadDetail()));
		_response!.StatusCode.ShouldBe(HttpStatusCode.OK);
	}

	[When(@"a reviewer saves the summary pair unchanged")]
	public async Task WhenAReviewerSavesUnchanged()
	{
		await Send("summary", new { version = _version, aiSummaryEn = SeededEn, aiSummaryFr = SeededFr });
	}

	[When(@"a User edits the summary pair")]
	public async Task WhenAUserEdits()
	{
		using var client = await BootedApi.SignedInAs(MemberRole.User);
		_response = await client.PutAsJsonAsync(
			$"/api/admin/reports/{_reportId}/summary",
			new { version = _version, aiSummaryEn = EditedEn, aiSummaryFr = EditedFr });
	}

	[When(@"a User restores the first revision")]
	public async Task WhenAUserRestores()
	{
		var first = FirstRevisionId(await ReadDetail());
		using var client = await BootedApi.SignedInAs(MemberRole.User);
		_response = await client.PostAsJsonAsync(RestoreUrl(first), new { version = _version });
	}

	[When(@"a reviewer restores the current revision")]
	public async Task WhenAReviewerRestoresTheCurrentRevision()
	{
		var detail = await ReadDetail();
		await Restore(detail.GetProperty("summaryRevisions")[0].GetProperty("id").GetString()!);
	}

	[When(@"a reviewer restores a revision that does not exist")]
	public async Task WhenAReviewerRestoresAMissingRevision()
	{
		await Restore(TinyId.New().Value);
	}

	[When(@"a visitor reads that report from the public feed")]
	public async Task WhenAVisitorReadsTheReport()
	{
		await ReadPublic();
	}

	[When(@"a reviewer reads the summary history")]
	public async Task WhenAReviewerReadsTheSummaryHistory()
	{
		_result = await ReadDetail();
	}

	// ── Then ────────────────────────────────────────────────────────────────

	[Then(@"the new revision is not approved")]
	public void ThenTheNewRevisionIsNotApproved()
	{
		_result.GetProperty("summary").GetProperty("approvedAt").ValueKind.ShouldBe(JsonValueKind.Null);
		_result.GetProperty("summaryRevisions")[0].GetProperty("approvedAt").ValueKind.ShouldBe(JsonValueKind.Null);
	}

	[Then(@"the report is Pending and not on the public feed")]
	public async Task ThenPendingAndNotPublic()
	{
		_result.GetProperty("status").GetString().ShouldBe("pending");
		_result.GetProperty("publishedAt").ValueKind.ShouldBe(JsonValueKind.Null);
		await ThenTheReportIsNotOnThePublicFeed();
	}

	[Then(@"the report is not on the public feed")]
	public async Task ThenTheReportIsNotOnThePublicFeed()
	{
		await ReadPublic();
		_publicStatus.ShouldBe(HttpStatusCode.NotFound);
	}

	[Then(@"^the summary has (\d+) revisions$")]
	public async Task ThenTheSummaryHasRevisions(int count)
	{
		(await ReadDetail()).GetProperty("summaryRevisions").GetArrayLength().ShouldBe(count);
	}

	[Then(@"the new revision records the reviewer's token subject and that its English text was written by a human")]
	public void ThenTheNewRevisionRecordsItsAuthor()
	{
		var latest = _result.GetProperty("summaryRevisions")[0];
		latest.GetProperty("authorSubject").GetString().ShouldNotBeNullOrWhiteSpace();
		latest.GetProperty("sourceEn").GetString().ShouldBe("human");
		latest.GetProperty("aiSummaryEn").GetString().ShouldBe(EditedEn);
		latest.GetProperty("createdAt").ValueKind.ShouldBe(JsonValueKind.String);
	}

	[Then(@"the first revision is unchanged")]
	public async Task ThenTheFirstRevisionIsUnchanged()
	{
		var first = (await ReadDetail()).GetProperty("summaryRevisions").EnumerateArray().Single(revision => revision.GetProperty("sequence").GetInt32() == 1);
		first.GetProperty("aiSummaryEn").GetString().ShouldBe(SeededEn);
		first.GetProperty("aiSummaryFr").GetString().ShouldBe(SeededFr);
		first.GetProperty("sourceEn").GetString().ShouldBe("generated");
		first.GetProperty("authorSubject").ValueKind.ShouldBe(JsonValueKind.Null);
	}

	[Then(@"the report is still Published with the same publish date")]
	public void ThenStillPublishedWithTheSameDate()
	{
		_result.GetProperty("status").GetString().ShouldBe("published");
		_result.GetProperty("publishedAt").GetDateTimeOffset().ShouldBe(_publishedAtBefore!.Value);
	}

	[Then(@"the new revision is approved by the reviewer who saved it")]
	public void ThenTheNewRevisionIsApprovedByItsAuthor()
	{
		var latest = _result.GetProperty("summaryRevisions")[0];
		latest.GetProperty("approvedAt").ValueKind.ShouldBe(JsonValueKind.String);
		latest.GetProperty("approvedBySubject").GetString().ShouldBe(latest.GetProperty("authorSubject").GetString());
		latest.GetProperty("approvedBySubject").GetString().ShouldNotBe("synthetic-approver");
	}

	[Then(@"the public feed shows the edited text")]
	public async Task ThenThePublicFeedShowsTheEditedText()
	{
		await ReadPublic();
		_publicStatus.ShouldBe(HttpStatusCode.OK);
		_publicView.GetProperty("aiSummaryEn").GetString().ShouldBe(EditedEn);
		_publicView.GetProperty("aiSummaryFr").GetString().ShouldBe(EditedFr);
	}

	[Then(@"the public feed shows the first revision's text")]
	public async Task ThenThePublicFeedShowsTheFirstRevisionsText()
	{
		await ReadPublic();
		_publicStatus.ShouldBe(HttpStatusCode.OK);
		_publicView.GetProperty("aiSummaryEn").GetString().ShouldBe(SeededEn);
		_publicView.GetProperty("aiSummaryFr").GetString().ShouldBe(SeededFr);
	}

	[Then(@"the new revision has the first revision's text and sources and names it as restored from")]
	public void ThenTheNewRevisionCopiesTheFirst()
	{
		var revisions = _result.GetProperty("summaryRevisions");
		var restored = revisions[0];
		var first = revisions[2];
		restored.GetProperty("sequence").GetInt32().ShouldBe(3);
		restored.GetProperty("aiSummaryEn").GetString().ShouldBe(first.GetProperty("aiSummaryEn").GetString());
		restored.GetProperty("aiSummaryFr").GetString().ShouldBe(first.GetProperty("aiSummaryFr").GetString());
		restored.GetProperty("sourceEn").GetString().ShouldBe(first.GetProperty("sourceEn").GetString());
		restored.GetProperty("sourceFr").GetString().ShouldBe(first.GetProperty("sourceFr").GetString());
		restored.GetProperty("restoredFromSequence").GetInt32().ShouldBe(1);
		restored.GetProperty("authorSubject").GetString().ShouldNotBeNullOrWhiteSpace();
	}

	[Then(@"the first and second revisions are unchanged")]
	public void ThenTheEarlierRevisionsAreUnchanged()
	{
		var revisions = _result.GetProperty("summaryRevisions");
		revisions[2].GetProperty("aiSummaryEn").GetString().ShouldBe(SeededEn);
		revisions[2].GetProperty("authorSubject").ValueKind.ShouldBe(JsonValueKind.Null);
		revisions[1].GetProperty("aiSummaryEn").GetString().ShouldBe(EditedEn);
		revisions[1].GetProperty("aiSummaryFr").GetString().ShouldBe(EditedFr);
		revisions[1].GetProperty("restoredFromSequence").ValueKind.ShouldBe(JsonValueKind.Null);
	}

	[Then(@"the restoring is audited as RolledBackSummary without any text")]
	public async Task ThenTheRestoringIsAudited()
	{
		var entry = (await AuditEntries(AuditAction.RolledBackSummary)).ShouldHaveSingleItem();
		entry.ActorSubject.ShouldNotBeNullOrWhiteSpace();
		(entry.Detail ?? string.Empty).ShouldNotContain("pilot", Case.Insensitive);
	}

	[Then(@"the latest revision is approved by that reviewer")]
	public async Task ThenTheLatestRevisionIsApprovedByThatReviewer()
	{
		var latest = (await ReadDetail()).GetProperty("summaryRevisions")[0];
		latest.GetProperty("approvedBySubject").GetString().ShouldNotBeNullOrWhiteSpace();
		latest.GetProperty("aiSummaryEn").GetString().ShouldBe(EditedEn);
	}

	[Then(@"the visitor sees the latest approved revision's text and never the draft")]
	public void ThenTheVisitorSeesTheApprovedText()
	{
		_publicStatus.ShouldBe(HttpStatusCode.OK);
		_publicView.GetProperty("aiSummaryEn").GetString().ShouldBe(SeededEn);
		_publicView.GetRawText().ShouldNotContain(_draftEn);
	}

	[Then(@"^the history lists (\d+) revisions, newest first$")]
	public void ThenTheHistoryListsRevisions(int count)
	{
		var revisions = _result.GetProperty("summaryRevisions");
		revisions.GetArrayLength().ShouldBe(count);
		revisions.EnumerateArray().Select(revision => revision.GetProperty("sequence").GetInt32()).ShouldBe([3, 2, 1]);
	}

	[Then(@"each carries its author, its time, and how each language was written")]
	public void ThenEachCarriesItsProvenance()
	{
		foreach (var revision in _result.GetProperty("summaryRevisions").EnumerateArray())
		{
			revision.GetProperty("createdAt").ValueKind.ShouldBe(JsonValueKind.String);
			revision.GetProperty("sourceEn").GetString().ShouldBeOneOf("generated", "human", "machine");
			revision.GetProperty("sourceFr").GetString().ShouldBeOneOf("generated", "human", "machine");
			revision.TryGetProperty("authorSubject", out _).ShouldBeTrue();
		}

		var revisions = _result.GetProperty("summaryRevisions");
		revisions[0].GetProperty("authorSubject").GetString().ShouldNotBeNullOrWhiteSpace();
		revisions[1].GetProperty("authorSubject").GetString().ShouldNotBeNullOrWhiteSpace();
		revisions[2].GetProperty("authorSubject").ValueKind.ShouldBe(JsonValueKind.Null);
		revisions[1].GetProperty("sourceEn").GetString().ShouldBe("human");
		revisions[1].GetProperty("sourceFr").GetString().ShouldBe("human");
		revisions[2].GetProperty("sourceEn").GetString().ShouldBe("generated");
	}

	[Then(@"the restored one names the revision it was restored from")]
	public void ThenTheRestoredOneNamesItsSource()
	{
		_result.GetProperty("summaryRevisions")[0].GetProperty("restoredFromSequence").GetInt32().ShouldBe(1);
		_result.GetProperty("summaryRevisions")[1].GetProperty("restoredFromSequence").ValueKind.ShouldBe(JsonValueKind.Null);
	}

	[Then(@"^the request is refused with (\d+) and saves nothing$")]
	public async Task ThenTheRequestIsRefused(int status)
	{
		((int)_response!.StatusCode).ShouldBe(status);

		var detail = await ReadDetail();
		detail.GetProperty("summaryRevisions").GetArrayLength().ShouldBe(1);
		(await AuditEntries(null)).ShouldBeEmpty();
	}

	// ── Helpers ─────────────────────────────────────────────────────────────

	/// <summary>Saves the edited pair as the officer, and keeps the version it answers with.</summary>
	private async Task EditThroughTheApi()
	{
		var before = await ReadDetail();
		_publishedAtBefore = before.GetProperty("publishedAt").ValueKind == JsonValueKind.String
			? before.GetProperty("publishedAt").GetDateTimeOffset()
			: null;

		await Send("summary", new { version = _version, aiSummaryEn = EditedEn, aiSummaryFr = EditedFr });
		_version = _result.GetProperty("version").GetString()!;
	}

	private async Task Restore(string revisionId)
	{
		var before = await ReadDetail();
		_publishedAtBefore = before.GetProperty("publishedAt").ValueKind == JsonValueKind.String
			? before.GetProperty("publishedAt").GetDateTimeOffset()
			: null;

		using var client = await BootedApi.SignedInAs(MemberRole.SafetyOfficer);
		_response?.Dispose();
		_response = await client.PostAsJsonAsync(RestoreUrl(revisionId), new { version = before.GetProperty("version").GetString() });

		if (_response.StatusCode == HttpStatusCode.OK)
		{
			_result = await _response.Content.ReadFromJsonAsync<JsonElement>();
			_version = _result.GetProperty("version").GetString()!;
		}
	}

	private string RestoreUrl(string revisionId)
	{
		return $"/api/admin/reports/{_reportId}/summary/revisions/{Uri.EscapeDataString(revisionId)}/rollback";
	}

	private async Task<JsonElement> ReadDetail()
	{
		using var client = await BootedApi.SignedInAs(MemberRole.SafetyOfficer);
		return await client.GetFromJsonAsync<JsonElement>(new Uri($"/api/admin/reports/{_reportId}", UriKind.Relative));
	}

	/// <summary>The oldest revision's id: history is listed newest first, so it is last.</summary>
	private static string FirstRevisionId(JsonElement detail)
	{
		var revisions = detail.GetProperty("summaryRevisions");
		return revisions[revisions.GetArrayLength() - 1].GetProperty("id").GetString()!;
	}

	private async Task ReadPublic()
	{
		using var visitor = (await BootedApi.Factory()).CreateClient();
		using var response = await visitor.GetAsync(new Uri($"/api/v1/public/reports/{_reportId}", UriKind.Relative));
		_publicStatus = response.StatusCode;
		_publicView = response.StatusCode == HttpStatusCode.OK
			? await response.Content.ReadFromJsonAsync<JsonElement>()
			: default;
	}
}
