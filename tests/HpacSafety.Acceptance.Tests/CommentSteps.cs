using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using HpacSafety.Core;
using HpacSafety.Core.Features.Comments;
using HpacSafety.Core.Features.Moderation;
using HpacSafety.Core.Features.Outbox;
using HpacSafety.Core.Features.Reporting;
using HpacSafety.Infrastructure.Persistence;
using HpacSafety.Worker.Outbox;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Reqnroll;
using Shouldly;

namespace HpacSafety.Acceptance.Tests;

/// <summary>
///     Members' comments on a published report through the booted API and the
///     real Worker processor (REQ-COM-001..014, ADR-0114).
/// </summary>
/// <remarks>
///     Each member is a token for a fresh subject, so scenarios never see each
///     other's comments as their own. Every comment is synthetic.
/// </remarks>
[Binding]
[Scope(Feature = "Comments")]
public sealed class CommentSteps
{
#pragma warning disable CA1822 // Reqnroll step bindings must be instance methods to be discovered.

	private const string CommentText = "Synthetic: good reminder to pick the landing field early.";
	private const string FrenchText = "Synthétique : bon rappel de choisir le champ tôt.";

	private readonly string _author = $"member:{Guid.NewGuid():n}";
	private readonly string _other = $"member:{Guid.NewGuid():n}";

	private WebApplicationFactory<Program>? _host;
	private string _reportId = string.Empty;
	private string _commentId = string.Empty;
	private string _otherCommentId = string.Empty;
	private int _countBefore;
	private HttpResponseMessage? _response;
	private JsonElement _listed;

	// ── Given ───────────────────────────────────────────────────────────────

	[Given(@"a report is published")]
	public async Task GivenAReportIsPublished()
	{
		_reportId = await BootedReports.Seed(ReportStatus.Published, "yes");
	}

	[Given(@"a report is not public")]
	public async Task GivenAReportIsNotPublic()
	{
		_reportId = await BootedReports.Seed(ReportStatus.Pending, "yes");
	}

	[Given(@"a member is signed in")]
	public void GivenAMemberIsSignedIn()
	{
		// Contextual — the member's token is minted per request below.
	}

	[Given(@"another member is signed in")]
	public void GivenAnotherMemberIsSignedIn()
	{
		// Contextual — a second, non-reviewer member with a subject of their own.
	}

	[Given(@"no translation provider is reachable")]
	public async Task GivenNoTranslationProviderIsReachable()
	{
		_host = (await BootedApi.Factory()).WithWebHostBuilder(builder =>
			builder.ConfigureTestServices(services => services.AddSingleton<ITranslator, UnreachableTranslator>()));
	}

	[Given(@"a member commented on a published report")]
	public async Task GivenAMemberCommented()
	{
		await GivenAReportIsPublished();
		_countBefore = await CommentCount();
		_commentId = await PostAs(_author, CommentText, "en-CA");
	}

	[Given(@"a member posted a comment in French")]
	public async Task GivenAMemberPostedInFrench()
	{
		await GivenAReportIsPublished();
		_commentId = await PostAs(_author, FrenchText, "fr-CA");
	}

	[Given(@"two members have each commented on a published report")]
	public async Task GivenTwoMembersCommented()
	{
		await GivenAMemberCommented();
		_otherCommentId = await PostAs(_other, "Synthetic: a second member's view.", "en-CA");
	}

	[Given(@"a published report has two visible comments and one hidden comment")]
	public async Task GivenTwoVisibleAndOneHidden()
	{
		await GivenAReportIsPublished();
		await PostAs(_author, "Synthetic one.", "en-CA");
		await PostAs(_other, "Synthetic two.", "en-CA");
		_commentId = await PostAs(_author, "Synthetic three, to be hidden.", "en-CA");
		await Hide(_commentId);
	}

	// ── When ────────────────────────────────────────────────────────────────

	[When(@"the member posts a comment on it")]
	public async Task WhenTheMemberPosts()
	{
		_countBefore = await StoredCount();
		using var client = Member(_author, _host ?? await BootedApi.Factory());
		_response = await client.PostAsJsonAsync(CommentsUri(), new { text = CommentText, locale = "en-CA" });
	}

	[When(@"a request without a member token posts a comment on it")]
	public async Task WhenAnAnonymousRequestPosts()
	{
		_countBefore = await StoredCount();
		using var client = (await BootedApi.Factory()).CreateClient();
		_response = await client.PostAsJsonAsync(CommentsUri(), new { text = CommentText, locale = "en-CA" });
	}

	[When(@"^the member posts a comment whose text is (.+)$")]
	public async Task WhenTheMemberPostsText(string text)
	{
		_countBefore = await StoredCount();
		var body = text switch
		{
			"empty" => string.Empty,
			"only whitespace" => "   \n\t ",
			"2001 characters long" => new string('a', ReportComment.MaxLength + 1),
			_ => throw new ArgumentOutOfRangeException(nameof(text), text, "Not an example this step knows."),
		};

		using var client = Member(_author, await BootedApi.Factory());
		_response = await client.PostAsJsonAsync(CommentsUri(), new { text = body, locale = "en-CA" });
	}

	[When(@"the Worker processes the comment's translation work")]
	public async Task WhenTheWorkerTranslates()
	{
		await using var scope = (await BootedApi.Factory()).Services.CreateAsyncScope();
		var database = scope.ServiceProvider.GetRequiredService<HpacSafetyDbContext>();
		var message = await database.OutboxMessages.SingleAsync(candidate =>
			candidate.Type == OutboxMessageType.TranslateComment && candidate.AggregateId == TinyId.Parse(_commentId));

		await new TranslateCommentProcessor(database, new LabellingTranslator()).Process(message, CancellationToken.None);
		await database.SaveChangesAsync();
	}

	[When(@"the first member reads the report's comments")]
	public async Task WhenTheFirstMemberReads()
	{
		using var client = Member(_author, await BootedApi.Factory());
		_listed = await client.GetFromJsonAsync<JsonElement>(CommentsUri());
	}

	[When(@"the member edits the comment")]
	public async Task WhenTheMemberEdits()
	{
		using var client = Member(_author, await BootedApi.Factory());
		_response = await client.PutAsJsonAsync(CommentUri(), new { text = "Synthetic, edited: pick the field before launch.", locale = "en-CA" });
		_response.StatusCode.ShouldBe(HttpStatusCode.OK);
	}

	[When(@"the member deletes the comment")]
	public async Task WhenTheMemberDeletes()
	{
		using var client = Member(_author, await BootedApi.Factory());
		_response = await client.DeleteAsync(CommentUri());
		_response.StatusCode.ShouldBe(HttpStatusCode.NoContent);
	}

	[When(@"^the other member tries to (edit|delete|hide) the comment$")]
	public async Task WhenTheOtherMemberTries(string action)
	{
		using var client = Member(_other, await BootedApi.Factory());
		_response = action switch
		{
			"edit" => await client.PutAsJsonAsync(CommentUri(), new { text = "Synthetic takeover.", locale = "en-CA" }),
			"delete" => await client.DeleteAsync(CommentUri()),
			_ => await client.PostAsync(new Uri($"/api/admin/comments/{_commentId}/hide", UriKind.Relative), null),
		};
	}

	[When(@"a safety officer hides the comment")]
	public async Task WhenASafetyOfficerHides()
	{
		await Hide(_commentId);
	}

	[When(@"a reviewer unpublishes the report")]
	public async Task WhenAReviewerUnpublishes()
	{
		await Review("unpublish");
	}

	[When(@"a reviewer publishes the report again")]
	public async Task WhenAReviewerPublishesAgain()
	{
		await Review("publish");
	}

	[When(@"the public feed is read")]
	public async Task WhenThePublicFeedIsRead()
	{
		using var client = (await BootedApi.Factory()).CreateClient();
		_listed = await client.GetFromJsonAsync<JsonElement>(new Uri($"/api/v1/public/reports/{_reportId}", UriKind.Relative));
	}

	// ── Then ────────────────────────────────────────────────────────────────

	[Then(@"the API answers 201 with the comment")]
	public async Task ThenCreatedWithTheComment()
	{
		_response!.StatusCode.ShouldBe(HttpStatusCode.Created);
		var body = await _response.Content.ReadFromJsonAsync<JsonElement>();
		body.GetProperty("text").GetString().ShouldBe(CommentText);
		body.GetProperty("isMine").GetBoolean().ShouldBeTrue();
		_commentId = body.GetProperty("id").GetString()!;
	}

	[Then(@"the comment is listed on that report for every reader, signed in or not")]
	public async Task ThenListedForEveryone()
	{
		(await Listed(Member(_other, await BootedApi.Factory()))).ShouldContain(_commentId);
		(await Listed((await BootedApi.Factory()).CreateClient())).ShouldContain(_commentId);
	}

	[Then(@"^the API answers (201|400|401|403|404)$")]
	public void ThenTheApiAnswers(int status)
	{
		((int)_response!.StatusCode).ShouldBe(status);
	}

	[Then(@"no comment is stored")]
	public async Task ThenNoCommentIsStored()
	{
		(await StoredCount()).ShouldBe(_countBefore);
	}

	[Then(@"one translation job for the comment is waiting for the Worker")]
	public async Task ThenOneTranslationJobWaits()
	{
		var id = (await _response!.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetString()!;
		await using var scope = (await BootedApi.Factory()).Services.CreateAsyncScope();
		var database = scope.ServiceProvider.GetRequiredService<HpacSafetyDbContext>();
		var jobs = await database.OutboxMessages
			.Where(message => message.Type == OutboxMessageType.TranslateComment && message.AggregateId == TinyId.Parse(id))
			.ToListAsync();

		jobs.Count.ShouldBe(1);
		jobs[0].ProcessedAt.ShouldBeNull();
	}

	[Then(@"the comment keeps its French text")]
	public async Task ThenTheCommentKeepsItsFrenchText()
	{
		var comment = await OnlyComment();
		comment.GetProperty("text").GetString().ShouldBe(FrenchText);
		comment.GetProperty("locale").GetString().ShouldBe("fr-CA");
	}

	[Then(@"its English text is the machine translation, recorded as ""auto""")]
	public async Task ThenItsEnglishIsAuto()
	{
		(await OnlyComment()).GetProperty("translatedText").GetString().ShouldBe($"[en-CA] {FrenchText}");

		var revision = (await Stored()).Revisions.Single();
		revision.TranslationSource.ShouldBe(TranslationSource.Auto);
	}

	[Then(@"only the first member's comment is marked as theirs")]
	public void ThenOnlyTheFirstIsMine()
	{
		var mine = _listed.EnumerateArray().Where(comment => comment.GetProperty("isMine").GetBoolean()).Select(Id).ToList();
		mine.ShouldBe([_commentId]);
		_listed.EnumerateArray().Select(Id).ShouldContain(_otherCommentId);
	}

	[Then(@"no comment carries its author's subject or any other identity")]
	public void ThenNoIdentity()
	{
		string[] allowed = ["id", "text", "locale", "translatedText", "createdAt", "updatedAt", "edited", "isMine"];

		foreach (var comment in _listed.EnumerateArray())
		{
			comment.EnumerateObject().Select(property => property.Name).ShouldBe(allowed, ignoreOrder: true);
		}

		_listed.GetRawText().ShouldNotContain(_author);
		_listed.GetRawText().ShouldNotContain(_other);
	}

	[Then(@"an anonymous reader sees no comment marked as theirs")]
	public async Task ThenAnonymousSeesNoneAsTheirs()
	{
		using var client = (await BootedApi.Factory()).CreateClient();
		var listed = await client.GetFromJsonAsync<JsonElement>(CommentsUri());
		listed.EnumerateArray().ShouldAllBe(comment => !comment.GetProperty("isMine").GetBoolean());
	}

	[Then(@"the comment shows the new text, marked as edited")]
	public async Task ThenTheCommentShowsTheNewText()
	{
		var comment = await OnlyComment();
		comment.GetProperty("text").GetString().ShouldBe("Synthetic, edited: pick the field before launch.");
		comment.GetProperty("edited").GetBoolean().ShouldBeTrue();
	}

	[Then(@"the earlier text is kept as an earlier revision")]
	public async Task ThenTheEarlierTextIsKept()
	{
		var revisions = (await Stored()).Revisions.OrderBy(revision => revision.Number).ToList();
		revisions.Count.ShouldBe(2);
		revisions[0].Text.ShouldBe(CommentText);
	}

	[Then(@"the new text waits for its own translation")]
	public async Task ThenTheNewTextWaits()
	{
		var latest = (await Stored()).Current;
		await using var scope = (await BootedApi.Factory()).Services.CreateAsyncScope();
		var database = scope.ServiceProvider.GetRequiredService<HpacSafetyDbContext>();
		(await database.OutboxMessages.CountAsync(message =>
			message.Type == OutboxMessageType.TranslateComment && message.Payload == latest.Id.Value)).ShouldBe(1);
	}

	[Then(@"the comment is no longer listed and the report's comment count drops by one")]
	public async Task ThenNoLongerListed()
	{
		(await Listed((await BootedApi.Factory()).CreateClient())).ShouldNotContain(_commentId);
		(await CommentCount()).ShouldBe(_countBefore);
	}

	[Then(@"the comment and its revisions are soft-deleted, not removed from the database")]
	public async Task ThenSoftDeleted()
	{
		var comment = await Stored();
		comment.Deleted.ShouldNotBeNull();
		comment.Revisions.ShouldAllBe(revision => revision.Deleted != null);
	}

	[Then(@"the comment is unchanged")]
	public async Task ThenUnchanged()
	{
		var comment = await Stored();
		comment.Deleted.ShouldBeNull();
		comment.Revisions.Count.ShouldBe(1);
		comment.Current.Text.ShouldBe(CommentText);
	}

	[Then(@"the audit log records who hid it, without its text")]
	public async Task ThenTheHideIsAudited()
	{
		await using var scope = (await BootedApi.Factory()).Services.CreateAsyncScope();
		var database = scope.ServiceProvider.GetRequiredService<HpacSafetyDbContext>();
		var entry = await database.AuditLog.SingleAsync(candidate =>
			candidate.Action == AuditAction.HidComment && candidate.TargetId == TinyId.Parse(_commentId));

		entry.ActorSubject.ShouldBe((await Stored()).HiddenBySubject);
		(entry.Detail ?? string.Empty).ShouldNotContain(CommentText);
	}

	[Then(@"the comment is kept in the database")]
	public async Task ThenKept()
	{
		(await Stored()).Current.Text.ShouldBe(CommentText);
	}

	[Then(@"the comment is still listed")]
	public async Task ThenStillListed()
	{
		(await Listed((await BootedApi.Factory()).CreateClient())).ShouldContain(_commentId);
	}

	[Then(@"the public API lists no comments for it and the report is not in the feed")]
	public async Task ThenNothingPublic()
	{
		using var client = (await BootedApi.Factory()).CreateClient();
		(await client.GetAsync(CommentsUri())).StatusCode.ShouldBe(HttpStatusCode.NotFound);
		(await client.GetAsync(new Uri($"/api/v1/public/reports/{_reportId}", UriKind.Relative))).StatusCode.ShouldBe(HttpStatusCode.NotFound);
	}

	[Then(@"the comment is listed again")]
	public async Task ThenListedAgain()
	{
		(await Listed((await BootedApi.Factory()).CreateClient())).ShouldContain(_commentId);
	}

	[Then(@"that report's comment count is (\d+)")]
	public void ThenTheCountIs(int count)
	{
		_listed.GetProperty("commentCount").GetInt32().ShouldBe(count);
	}

	// ── Helpers ─────────────────────────────────────────────────────────────

	private Uri CommentsUri()
	{
		return new Uri($"/api/v1/public/reports/{_reportId}/comments", UriKind.Relative);
	}

	private Uri CommentUri()
	{
		return new Uri($"/api/v1/public/reports/{_reportId}/comments/{_commentId}", UriKind.Relative);
	}

	private static HttpClient Member(string subject,
									 WebApplicationFactory<Program> host)
	{
		return BootedApi.SignedInAsMember(host, subject);
	}

	private static string Id(JsonElement comment)
	{
		return comment.GetProperty("id").GetString()!;
	}

	private async Task<string> PostAs(string subject,
									  string text,
									  string locale)
	{
		using var client = Member(subject, await BootedApi.Factory());
		using var response = await client.PostAsJsonAsync(CommentsUri(), new { text, locale });
		response.StatusCode.ShouldBe(HttpStatusCode.Created);
		return Id(await response.Content.ReadFromJsonAsync<JsonElement>());
	}

	private static async Task Hide(string commentId)
	{
		using var officer = await BootedApi.SignedInAs(MemberRole.SafetyOfficer);
		using var hidden = await officer.PostAsync(new Uri($"/api/admin/comments/{commentId}/hide", UriKind.Relative), null);
		hidden.StatusCode.ShouldBe(HttpStatusCode.NoContent);
	}

	private async Task Review(string command)
	{
		using var officer = await BootedApi.SignedInAs(MemberRole.SafetyOfficer);
		var detail = await officer.GetFromJsonAsync<JsonElement>(new Uri($"/api/admin/reports/{_reportId}", UriKind.Relative));
		using var done = await officer.PostAsJsonAsync($"/api/admin/reports/{_reportId}/{command}", new { version = detail.GetProperty("version").GetString() });
		done.StatusCode.ShouldBe(HttpStatusCode.OK);
	}

	private async Task<List<string>> Listed(HttpClient client)
	{
		using (client)
		{
			var listed = await client.GetFromJsonAsync<JsonElement>(CommentsUri());
			return [.. listed.EnumerateArray().Select(Id)];
		}
	}

	private async Task<JsonElement> OnlyComment()
	{
		using var client = (await BootedApi.Factory()).CreateClient();
		var listed = await client.GetFromJsonAsync<JsonElement>(CommentsUri());
		return listed.EnumerateArray().Single(comment => Id(comment) == _commentId);
	}

	private async Task<int> CommentCount()
	{
		using var client = (await BootedApi.Factory()).CreateClient();
		var report = await client.GetFromJsonAsync<JsonElement>(new Uri($"/api/v1/public/reports/{_reportId}", UriKind.Relative));
		return report.GetProperty("commentCount").GetInt32();
	}

	private async Task<int> StoredCount()
	{
		await using var scope = (await BootedApi.Factory()).Services.CreateAsyncScope();
		var database = scope.ServiceProvider.GetRequiredService<HpacSafetyDbContext>();
		var reportId = TinyId.Parse(_reportId);
		return await database.ReportComments.IgnoreQueryFilters().CountAsync(comment => comment.ReportId == reportId);
	}

	private async Task<ReportComment> Stored()
	{
		await using var scope = (await BootedApi.Factory()).Services.CreateAsyncScope();
		var database = scope.ServiceProvider.GetRequiredService<HpacSafetyDbContext>();
		var id = TinyId.Parse(_commentId);
		return await database.ReportComments
			.IgnoreQueryFilters()
			.Include(comment => comment.Revisions)
			.AsNoTracking()
			.SingleAsync(comment => comment.Id == id);
	}

	/// <summary>A translator that labels its output with the target locale, so a test can recognize it.</summary>
	private sealed class LabellingTranslator : ITranslator
	{
		public bool IsConfigured => true;

		public Task<IReadOnlyList<string>> Translate(IReadOnlyList<string> texts,
													 Locale source,
													 Locale target,
													 CancellationToken cancellationToken)
		{
			return Task.FromResult<IReadOnlyList<string>>([.. texts.Select(text => $"[{target.Code}] {text}")]);
		}
	}

	/// <summary>A translator that is never reachable. Posting must not notice it.</summary>
	private sealed class UnreachableTranslator : ITranslator
	{
		public bool IsConfigured => true;

		public Task<IReadOnlyList<string>> Translate(IReadOnlyList<string> texts,
													 Locale source,
													 Locale target,
													 CancellationToken cancellationToken)
		{
			throw new TranslationUnavailableException("Synthetic: no provider is reachable.");
		}
	}
}
