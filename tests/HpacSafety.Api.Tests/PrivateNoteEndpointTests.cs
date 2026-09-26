using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using HpacSafety.Core;
using HpacSafety.Core.Features.Moderation;
using HpacSafety.Core.Features.Reporting;
using HpacSafety.Infrastructure.Persistence;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;

namespace HpacSafety.Api.Tests;

/// <summary>
///     The edges of the private-note endpoints (ADR-0133), against a real
///     PostgreSQL container. Who may keep notes and what a note records are the
///     REQ-MOD-098..104 acceptance scenarios; these cover malformed input, notes
///     and reports that are not there, and two reviewers editing at once.
/// </summary>
[Trait("Category", "Integration")]
[Collection(SharedApiPostgres.Name)]
public class PrivateNoteEndpointTests(ApiPostgresFixture fixture)
{
	private readonly WebApplicationFactory<Program> _factory = fixture.Factory;

	[Theory]
	[InlineData("Synthetic: an edit with no revision.", null)]
	[InlineData("   ", 1)]
	public async Task GivenInvalidEdit_WhenSaving_ThenBadRequestAndTextUnchanged(string text,
																					int? revision)
	{
		// Given
		var (notes, noteId, officer) = await Noted();

		// When
		using var response = await officer.PutAsJsonAsync($"{notes}/{noteId}", new { text, revision });

		// Then
		response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
		var listed = await officer.GetFromJsonAsync<JsonElement>(notes);
		listed[0].GetProperty("text").GetString().ShouldBe("Synthetic original.");
		listed[0].GetProperty("revision").GetInt32().ShouldBe(1);
		officer.Dispose();
	}

	[Theory]
	[InlineData("not-an-id")]
	[InlineData("AAAAAAAAAAA")]
	public async Task GivenNoteThatIsNotThere_WhenEditingRemovingOrReadingHistory_ThenNotFound(string noteId)
	{
		// Given
		var (notes, _, officer) = await Noted();

		// When
		using var edited = await officer.PutAsJsonAsync($"{notes}/{noteId}", new { text = "Synthetic.", revision = 1 });
		using var removed = await officer.DeleteAsync($"{notes}/{noteId}");
		using var history = await officer.GetAsync($"{notes}/{noteId}/revisions");

		// Then
		edited.StatusCode.ShouldBe(HttpStatusCode.NotFound);
		removed.StatusCode.ShouldBe(HttpStatusCode.NotFound);
		history.StatusCode.ShouldBe(HttpStatusCode.NotFound);
		officer.Dispose();
	}

	[Theory]
	[InlineData("not-an-id")]
	[InlineData("AAAAAAAAAAA")]
	public async Task GivenReportThatIsNotThere_WhenListingOrAdding_ThenNotFound(string reportId)
	{
		// Given
		using var officer = await SignedInClient.As(_factory, MemberRole.SafetyOfficer);

		// When
		using var listed = await officer.GetAsync($"/api/admin/reports/{reportId}/private-notes");
		using var added = await officer.PostAsJsonAsync($"/api/admin/reports/{reportId}/private-notes", new { text = "Synthetic." });

		// Then
		listed.StatusCode.ShouldBe(HttpStatusCode.NotFound);
		added.StatusCode.ShouldBe(HttpStatusCode.NotFound);
	}

	[Fact]
	public async Task GivenNoteOnAnotherReport_WhenEditedThroughThisReport_ThenNotFound()
	{
		// Given
		var (_, noteId, officer) = await Noted();
		var (otherNotes, _, _) = await Noted();

		// When
		using var edited = await officer.PutAsJsonAsync($"{otherNotes}/{noteId}", new { text = "Synthetic.", revision = 1 });

		// Then
		edited.StatusCode.ShouldBe(HttpStatusCode.NotFound);
		officer.Dispose();
	}

	[Fact]
	public async Task GivenConcurrentEditsOfSameRevision_WhenSaved_ThenOneWinsAndRestConflict()
	{
		// Given
		var (notes, noteId, officer) = await Noted();

		// When
		var responses = await Task.WhenAll(Enumerable.Range(0, 8).Select(index =>
			officer.PutAsJsonAsync($"{notes}/{noteId}", new { text = $"Synthetic edit {index}.", revision = 1 })));

		// Then
		responses.Count(response => response.StatusCode == HttpStatusCode.OK).ShouldBe(1);
		responses.Count(response => response.StatusCode == HttpStatusCode.Conflict).ShouldBe(7);

		await using var scope = _factory.Services.CreateAsyncScope();
		var database = scope.ServiceProvider.GetRequiredService<HpacSafetyDbContext>();
		var id = TinyId.Parse(noteId);
		(await database.PrivateNoteRevisions.CountAsync(revision => revision.NoteId == id)).ShouldBe(2);

		foreach (var response in responses)
		{
			response.Dispose();
		}

		officer.Dispose();
	}

	private async Task<(string Notes, string NoteId, HttpClient Officer)> Noted()
	{
		var reportId = await Pending();
		var notes = $"/api/admin/reports/{reportId}/private-notes";
		var officer = await SignedInClient.As(_factory, MemberRole.SafetyOfficer);
		using var added = await officer.PostAsJsonAsync(notes, new { text = "Synthetic original." });
		added.StatusCode.ShouldBe(HttpStatusCode.Created);
		var noteId = (await added.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetString()!;
		return (notes, noteId, officer);
	}

	private async Task<string> Pending()
	{
		await using var scope = _factory.Services.CreateAsyncScope();
		var database = scope.ServiceProvider.GetRequiredService<HpacSafetyDbContext>();
		var report = new Report(Locale.EnCa, DateTimeOffset.UtcNow);
		database.Reports.Add(report);
		await database.SaveChangesAsync();
		return report.Id.Value;
	}
}
