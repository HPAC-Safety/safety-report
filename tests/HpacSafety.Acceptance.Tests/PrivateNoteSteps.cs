using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using HpacSafety.Core;
using HpacSafety.Core.Features.Moderation;
using HpacSafety.Core.Features.PrivateNotes;
using HpacSafety.Core.Features.Reporting;
using HpacSafety.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Reqnroll;
using Shouldly;

namespace HpacSafety.Acceptance.Tests;

/// <summary>
///     Staff-only private notes on a report, through the booted API
///     (REQ-MOD-098..104, ADR-0133).
/// </summary>
/// <remarks>
///     Each reviewer is a token for a fresh subject, so a scenario can tell who
///     wrote which revision. Every note is synthetic.
/// </remarks>
[Binding]
public sealed class PrivateNoteSteps
{
#pragma warning disable CA1822 // Reqnroll step bindings must be instance methods to be discovered.

	private const string NoteText = "Synthetic: called the reporter, follow up Monday.";

	private readonly string _officer = $"officer:{Guid.NewGuid():n}";
	private readonly string _administrator = $"admin:{Guid.NewGuid():n}";
	private readonly List<(string Text, string Writer)> _written = [];
	private readonly List<HttpStatusCode> _answers = [];

	private string _reportId = string.Empty;
	private string _noteId = string.Empty;
	private int _storedBefore;
	private int _outboxBefore;
	private HttpResponseMessage? _response;
	private readonly List<string> _publicBodies = [];

	// ── Given ───────────────────────────────────────────────────────────────

	[Given(@"a report carrying one private note")]
	public async Task GivenAReportCarryingOneNote()
	{
		_reportId = await BootedReports.Seed(ReportStatus.Pending, true);
		_noteId = await Add(_officer, "safety_officer", NoteText);
	}

	[Given(@"^a (pending|published|unpublished|summary-failed|no-consent) report that staff keep private notes on$")]
	public async Task GivenAReportInStatus(string status)
	{
		_reportId = status switch
		{
			"pending" => await BootedReports.Seed(ReportStatus.Pending, true),
			"published" => await BootedReports.Seed(ReportStatus.Published, true),
			"unpublished" => await BootedReports.Seed(ReportStatus.Unpublished, true),
			"summary-failed" => await BootedReports.Seed(ReportStatus.SummaryFailed, true),
			_ => await BootedReports.Seed(ReportStatus.Unpublished, false),
		};
	}

	[Given(@"a safety officer wrote a private note on a report")]
	public async Task GivenAnOfficerWroteANote()
	{
		await GivenAReportCarryingOneNote();
		_written.Add((NoteText, _officer));
	}

	[Given(@"a safety officer wrote a private note on a report and edited it once")]
	public async Task GivenAnOfficerWroteAndEditedANote()
	{
		await GivenAnOfficerWroteANote();
		(await EditAs(_officer, "safety_officer", "Synthetic: follow-up moved to Tuesday.", 1)).StatusCode.ShouldBe(HttpStatusCode.OK);
	}

	[Given(@"a published report whose reporter consented to publication and media carries one private note")]
	public async Task GivenAPublishedConsentedReportWithANote()
	{
		_reportId = await BootedReports.Seed(ReportStatus.Published, true, mediaConsent: true);
		_noteId = await Add(_officer, "safety_officer", NoteText);
	}

	// ── When ────────────────────────────────────────────────────────────────

	[When(@"^(an anonymous visitor|a User|a SafetyOfficer|an Administrator) adds, lists, edits, reads the history of, and removes private notes on it$")]
	public async Task WhenSomeoneUsesEveryRoute(string who)
	{
		using var client = await ClientFor(who);

		using (var added = await client.PostAsJsonAsync(NotesUri(), new { text = "Synthetic: another note." }))
		{
			_answers.Add(added.StatusCode);
		}

		using (var listed = await client.GetAsync(NotesUri()))
		{
			_answers.Add(listed.StatusCode);
		}

		using (var edited = await client.PutAsJsonAsync(NoteUri(), new { text = "Synthetic: an edit.", revision = 1 }))
		{
			_answers.Add(edited.StatusCode);
		}

		using (var history = await client.GetAsync(new Uri($"{NoteUri()}/revisions", UriKind.Relative)))
		{
			_answers.Add(history.StatusCode);
		}

		using (var removed = await client.DeleteAsync(NoteUri()))
		{
			_answers.Add(removed.StatusCode);
		}
	}

	[When(@"a safety officer adds two private notes and an administrator adds a third")]
	public async Task WhenStaffAddThreeNotes()
	{
		_outboxBefore = await OutboxCount();

		foreach (var (text, subject, role) in new[]
				 {
					 ("Synthetic: first note.", _officer, "safety_officer"),
					 ("Synthetic: second note.", _officer, "safety_officer"),
					 ("Synthetic: third note.", _administrator, "administrator"),
				 })
		{
			await Add(subject, role, text);
			_written.Add((text, subject));

			// Newest first is by when each was written; keep them apart.
			await Task.Delay(TimeSpan.FromMilliseconds(5));
		}
	}

	[When(@"an administrator edits that private note twice")]
	public async Task WhenAnAdministratorEditsTwice()
	{
		foreach (var (text, basedOn) in new[] { ("Synthetic: investigator called back.", 1), ("Synthetic: investigator report received.", 2) })
		{
			using var edited = await EditAs(_administrator, "administrator", text, basedOn);
			edited.StatusCode.ShouldBe(HttpStatusCode.OK);
			_written.Add((text, _administrator));
		}
	}

	[When(@"an administrator removes that private note")]
	public async Task WhenAnAdministratorRemoves()
	{
		using var admin = await Reviewer(_administrator, "administrator");
		using var removed = await admin.DeleteAsync(NoteUri());
		removed.StatusCode.ShouldBe(HttpStatusCode.NoContent);
	}

	[When(@"^a safety officer adds a private note whose text is (.+)$")]
	public async Task WhenAnOfficerAddsText(string text)
	{
		_storedBefore = await StoredCount();
		var body = text switch
		{
			"empty" => string.Empty,
			"only whitespace" => "   \n\t ",
			"4001 characters long" => new string('a', PrivateNote.MaxLength + 1),
			_ => throw new ArgumentOutOfRangeException(nameof(text), text, "Not an example this step knows."),
		};

		using var officer = await Reviewer(_officer, "safety_officer");
		_response = await officer.PostAsJsonAsync(NotesUri(), new { text = body });
	}

	[When(@"a safety officer deletes that report")]
	public async Task WhenAnOfficerDeletesTheReport()
	{
		using var officer = await Reviewer(_officer, "safety_officer");
		using var deleted = await officer.DeleteAsync(new Uri($"/api/admin/reports/{_reportId}", UriKind.Relative));
		deleted.StatusCode.ShouldBe(HttpStatusCode.NoContent);
	}

	[When(@"an anonymous visitor and a User read the public feed, that report's public page, and its comments")]
	public async Task WhenPublicReadersRead()
	{
		using var anonymous = (await BootedApi.Factory()).CreateClient();
		using var user = await BootedApi.SignedInAs(MemberRole.User);

		foreach (var client in new[] { anonymous, user })
		{
			foreach (var path in new[] { "/api/v1/public/reports", $"/api/v1/public/reports/{_reportId}", $"/api/v1/public/reports/{_reportId}/comments" })
			{
				using var response = await client.GetAsync(new Uri(path, UriKind.Relative));
				response.StatusCode.ShouldBe(HttpStatusCode.OK, path);
				_publicBodies.Add(await response.Content.ReadAsStringAsync());
			}
		}
	}

	// ── Then ────────────────────────────────────────────────────────────────

	[Then(@"^the API answers (401|403|with success) to every one of those requests$")]
	public void ThenEveryRequestAnswers(string outcome)
	{
		HttpStatusCode[] expected = outcome switch
		{
			"401" => [.. Enumerable.Repeat(HttpStatusCode.Unauthorized, 5)],
			"403" => [.. Enumerable.Repeat(HttpStatusCode.Forbidden, 5)],
			_ => [HttpStatusCode.Created, HttpStatusCode.OK, HttpStatusCode.OK, HttpStatusCode.OK, HttpStatusCode.NoContent],
		};

		_answers.ShouldBe(expected);
	}

	[Then(@"all three private notes are listed, newest first")]
	public async Task ThenAllThreeListedNewestFirst()
	{
		var texts = (await Listed()).Select(note => note.GetProperty("text").GetString()!).ToList();
		texts.ShouldBe(_written.Select(written => written.Text).Reverse().ToList());
	}

	[Then(@"each lists its text, its writer's token subject, and when it was written")]
	public async Task ThenEachListsTextWriterAndTime()
	{
		foreach (var note in await Listed())
		{
			var text = note.GetProperty("text").GetString();
			note.GetProperty("writtenBy").GetString().ShouldBe(_written.Single(written => written.Text == text).Writer);
			note.GetProperty("writtenAt").GetDateTimeOffset().ShouldBeGreaterThan(DateTimeOffset.UtcNow.AddMinutes(-5));
			note.GetProperty("edited").GetBoolean().ShouldBeFalse();
		}
	}

	[Then(@"writing them queued no work for the Worker")]
	public async Task ThenNoWorkQueued()
	{
		(await OutboxCount()).ShouldBe(_outboxBefore);
	}

	[Then(@"the private note lists the latest text, written by the administrator, marked as edited")]
	public async Task ThenTheLatestTextIsListed()
	{
		var note = (await Listed()).Single(candidate => candidate.GetProperty("id").GetString() == _noteId);
		note.GetProperty("text").GetString().ShouldBe(_written[^1].Text);
		note.GetProperty("writtenBy").GetString().ShouldBe(_administrator);
		note.GetProperty("revision").GetInt32().ShouldBe(3);
		note.GetProperty("edited").GetBoolean().ShouldBeTrue();
	}

	[Then(@"its history lists all three revisions oldest first, each unchanged with its own text, writer, and time")]
	public async Task ThenTheHistoryListsAllThree()
	{
		using var officer = await Reviewer(_officer, "safety_officer");
		var history = await officer.GetFromJsonAsync<JsonElement>(new Uri($"{NoteUri()}/revisions", UriKind.Relative));
		var revisions = history.EnumerateArray().ToList();

		revisions.Select(revision => revision.GetProperty("number").GetInt32()).ShouldBe([1, 2, 3]);
		revisions.Select(revision => revision.GetProperty("text").GetString()).ShouldBe(_written.Select(written => written.Text));
		revisions.Select(revision => revision.GetProperty("writtenBy").GetString()).ShouldBe(_written.Select(written => written.Writer));
		revisions.Select(revision => revision.GetProperty("writtenAt").GetDateTimeOffset()).ShouldBeInOrder();

		// Unchanged in storage too: the first revision is exactly as the officer wrote it.
		var stored = await Stored();
		stored.Revisions.Single(revision => revision.Number == 1).Text.ShouldBe(NoteText);
		stored.Revisions.Single(revision => revision.Number == 1).AuthorSubject.ShouldBe(_officer);
	}

	[Then(@"an edit based on an earlier revision is refused with 409 and saves nothing")]
	public async Task ThenAStaleEditIsRefused()
	{
		using var stale = await EditAs(_officer, "safety_officer", "Synthetic: an edit from an old view.", 2);
		stale.StatusCode.ShouldBe(HttpStatusCode.Conflict);
		(await Stored()).Revisions.Count.ShouldBe(3);
	}

	[Then(@"the private note is no longer listed, and editing it or reading its history answers 404")]
	public async Task ThenNoLongerListed()
	{
		(await Listed()).Select(note => note.GetProperty("id").GetString()).ShouldNotContain(_noteId);

		using var edited = await EditAs(_officer, "safety_officer", "Synthetic: too late.", 2);
		edited.StatusCode.ShouldBe(HttpStatusCode.NotFound);

		using var officer = await Reviewer(_officer, "safety_officer");
		using var history = await officer.GetAsync(new Uri($"{NoteUri()}/revisions", UriKind.Relative));
		history.StatusCode.ShouldBe(HttpStatusCode.NotFound);
	}

	[Then(@"the private note and both its revisions are stamped deleted at one time, and nothing is erased")]
	public async Task ThenStampedDeleted()
	{
		var note = await Stored();
		note.Deleted.ShouldNotBeNull();
		note.Revisions.Count.ShouldBe(2);
		note.Revisions.ShouldAllBe(revision => revision.Deleted == note.Deleted);
	}

	[Then(@"one audit entry records the administrator's token subject, RemovedPrivateNote, the note, and the time, without its text")]
	public async Task ThenTheRemovalIsAudited()
	{
		var note = await Stored();
		await using var scope = (await BootedApi.Factory()).Services.CreateAsyncScope();
		var database = scope.ServiceProvider.GetRequiredService<HpacSafetyDbContext>();
		var entries = await database.AuditLog
			.Where(entry => entry.TargetId == note.Id && entry.Action == AuditAction.RemovedPrivateNote)
			.ToListAsync();

		entries.Count.ShouldBe(1);
		entries[0].ActorSubject.ShouldBe(_administrator);
		entries[0].TargetType.ShouldBe("PrivateNote");
		entries[0].OccurredAt.ShouldBe(note.Deleted!.Value);
		(entries[0].Detail ?? string.Empty).ShouldNotContain("Synthetic");
	}

	[Then(@"the API answers 400 and no private note is stored")]
	public async Task ThenRefusedAndNothingStored()
	{
		_response!.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
		(await StoredCount()).ShouldBe(_storedBefore);
	}

	[Then(@"the private note and its revision are stamped deleted at the report's deletion time")]
	public async Task ThenTheNoteWentWithTheReport()
	{
		await using var scope = (await BootedApi.Factory()).Services.CreateAsyncScope();
		var database = scope.ServiceProvider.GetRequiredService<HpacSafetyDbContext>();
		var reportId = TinyId.Parse(_reportId);
		var report = await database.Reports.IgnoreQueryFilters().AsNoTracking().SingleAsync(candidate => candidate.Id == reportId);
		var note = await Stored();

		report.Deleted.ShouldNotBeNull();
		note.Deleted.ShouldBe(report.Deleted);
		note.Revisions.ShouldAllBe(revision => revision.Deleted == report.Deleted);
	}

	[Then(@"adding, listing, or editing private notes on that report answers 404")]
	public async Task ThenEveryNoteRouteIsNotFound()
	{
		using var officer = await Reviewer(_officer, "safety_officer");
		using var added = await officer.PostAsJsonAsync(NotesUri(), new { text = "Synthetic: after deletion." });
		using var listed = await officer.GetAsync(NotesUri());
		using var edited = await officer.PutAsJsonAsync(NoteUri(), new { text = "Synthetic: after deletion.", revision = 1 });

		new[] { added.StatusCode, listed.StatusCode, edited.StatusCode }.ShouldAllBe(status => status == HttpStatusCode.NotFound);
	}

	[Then(@"no response carries the private note's text or identifier, or any count of private notes")]
	public void ThenNoPublicResponseCarriesTheNote()
	{
		_publicBodies.Count.ShouldBe(6);

		foreach (var body in _publicBodies)
		{
			body.ShouldNotContain(NoteText);
			body.ShouldNotContain(_noteId);
			using var document = JsonDocument.Parse(body);
			PropertyNames(document.RootElement)
				.ShouldNotContain(name => name.Contains("note", StringComparison.OrdinalIgnoreCase));
		}

		// The report itself is public, so the reads above saw it.
		_publicBodies.ShouldContain(body => body.Contains(_reportId, StringComparison.Ordinal));
	}

	[Then(@"no database view reads a private-note table")]
	public async Task ThenNoViewReadsTheNotes()
	{
		await using var scope = (await BootedApi.Factory()).Services.CreateAsyncScope();
		var database = scope.ServiceProvider.GetRequiredService<HpacSafetyDbContext>();

		var views = await database.Database
			.SqlQueryRaw<string>("SELECT viewname AS \"Value\" FROM pg_views WHERE schemaname = 'public'")
			.ToListAsync();
		var readers = await database.Database
			.SqlQueryRaw<string>("SELECT viewname AS \"Value\" FROM pg_views WHERE schemaname = 'public' AND definition ILIKE '%private_note%'")
			.ToListAsync();

		views.ShouldContain("public_reports");
		readers.ShouldBeEmpty();
	}

	// ── Helpers ─────────────────────────────────────────────────────────────

	/// <summary>Every property name anywhere in a JSON document.</summary>
	private static IEnumerable<string> PropertyNames(JsonElement element)
	{
		return element.ValueKind switch
		{
			JsonValueKind.Object => element.EnumerateObject()
				.SelectMany(property => PropertyNames(property.Value).Prepend(property.Name)),
			JsonValueKind.Array => element.EnumerateArray().SelectMany(PropertyNames),
			_ => [],
		};
	}

	private Uri NotesUri()
	{
		return new Uri($"/api/admin/reports/{_reportId}/private-notes", UriKind.Relative);
	}

	private Uri NoteUri()
	{
		return new Uri($"/api/admin/reports/{_reportId}/private-notes/{_noteId}", UriKind.Relative);
	}

	private static async Task<HttpClient> Reviewer(string subject,
												   string role)
	{
		return BootedApi.SignedInAsMember(await BootedApi.Factory(), subject, role);
	}

	private async Task<HttpClient> ClientFor(string who)
	{
		return who switch
		{
			"an anonymous visitor" => (await BootedApi.Factory()).CreateClient(),
			"a User" => await BootedApi.SignedInAs(MemberRole.User),
			"a SafetyOfficer" => await Reviewer(_officer, "safety_officer"),
			_ => await Reviewer(_administrator, "administrator"),
		};
	}

	private async Task<string> Add(string subject,
								   string role,
								   string text)
	{
		using var client = await Reviewer(subject, role);
		using var response = await client.PostAsJsonAsync(NotesUri(), new { text });
		response.StatusCode.ShouldBe(HttpStatusCode.Created);
		return (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetString()!;
	}

	private async Task<HttpResponseMessage> EditAs(string subject,
												   string role,
												   string text,
												   int basedOn)
	{
		using var client = await Reviewer(subject, role);
		return await client.PutAsJsonAsync(NoteUri(), new { text, revision = basedOn });
	}

	private async Task<List<JsonElement>> Listed()
	{
		using var officer = await Reviewer(_officer, "safety_officer");
		var listed = await officer.GetFromJsonAsync<JsonElement>(NotesUri());
		return [.. listed.EnumerateArray()];
	}

	private async Task<int> StoredCount()
	{
		await using var scope = (await BootedApi.Factory()).Services.CreateAsyncScope();
		var database = scope.ServiceProvider.GetRequiredService<HpacSafetyDbContext>();
		var reportId = TinyId.Parse(_reportId);
		return await database.PrivateNotes.IgnoreQueryFilters().CountAsync(note => note.ReportId == reportId);
	}

	/// <summary>
	///     Outbox messages naming this report, or any of its notes or their
	///     revisions. Scenarios run in parallel against one database, so nothing
	///     wider can be counted.
	/// </summary>
	private async Task<int> OutboxCount()
	{
		await using var scope = (await BootedApi.Factory()).Services.CreateAsyncScope();
		var database = scope.ServiceProvider.GetRequiredService<HpacSafetyDbContext>();
		var reportId = TinyId.Parse(_reportId);
		var notes = await database.PrivateNotes.IgnoreQueryFilters()
			.Where(note => note.ReportId == reportId)
			.Select(note => note.Id)
			.ToListAsync();
		var revisions = await database.PrivateNoteRevisions.IgnoreQueryFilters()
			.Where(revision => notes.Contains(revision.NoteId))
			.Select(revision => revision.Id)
			.ToListAsync();
		List<TinyId> named = [reportId, .. notes, .. revisions];
		var payloads = named.ConvertAll(id => id.Value);

		return await database.OutboxMessages.IgnoreQueryFilters()
			.CountAsync(message => named.Contains(message.AggregateId) || payloads.Contains(message.Payload));
	}

	private async Task<PrivateNote> Stored()
	{
		await using var scope = (await BootedApi.Factory()).Services.CreateAsyncScope();
		var database = scope.ServiceProvider.GetRequiredService<HpacSafetyDbContext>();
		var id = TinyId.Parse(_noteId);
		return await database.PrivateNotes
			.IgnoreQueryFilters()
			.Include(note => note.Revisions)
			.AsNoTracking()
			.SingleAsync(note => note.Id == id);
	}
}
