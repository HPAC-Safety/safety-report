using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using HpacSafety.Core;
using HpacSafety.Core.Features.Moderation;
using HpacSafety.Core.Features.QuestionBank;
using HpacSafety.Core.Features.Reporting;
using HpacSafety.Infrastructure.Persistence;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;

namespace HpacSafety.Api.Tests;

/// <summary>
///     The edges of the comment endpoints (ADR-0114), against a real PostgreSQL
///     container. What members, authors, and reviewers may do is proven by the
///     REQ-COM acceptance scenarios; these cover malformed input and comments
///     that are not there.
/// </summary>
[Trait("Category", "Integration")]
[Collection(SharedApiPostgres.Name)]
public class CommentEndpointTests(ApiPostgresFixture fixture)
{
	private readonly WebApplicationFactory<Program> _factory = fixture.Factory;

	[Fact]
	public async Task GivenUnknownLanguage_WhenPosting_ThenBadRequest()
	{
		// Given
		var reportId = await Published();
		using var member = await SignedInClient.As(_factory, MemberRole.User);

		// When
		using var response = await member.PostAsJsonAsync($"/api/v1/public/reports/{reportId}/comments", new { text = "Synthetic.", locale = "de-DE" });

		// Then
		response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
	}

	[Theory]
	[InlineData("   ", "en-CA")]
	[InlineData("Synthetic.", "xx")]
	public async Task GivenInvalidEdit_WhenSaving_ThenBadRequestAndTextUnchanged(string text,
																					string locale)
	{
		// Given
		var (reportId, commentId, member) = await Commented();

		// When
		using var response = await member.PutAsJsonAsync($"/api/v1/public/reports/{reportId}/comments/{commentId}", new { text, locale });

		// Then
		response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
		var listed = await member.GetFromJsonAsync<JsonElement>($"/api/v1/public/reports/{reportId}/comments");
		listed[0].GetProperty("text").GetString().ShouldBe("Synthetic original.");
		member.Dispose();
	}

	[Theory]
	[InlineData("not-an-id")]
	[InlineData("AAAAAAAAAAA")]
	public async Task GivenCommentThatIsNotThere_WhenEditingDeletingOrHiding_ThenNotFound(string commentId)
	{
		// Given
		var reportId = await Published();
		using var member = await SignedInClient.As(_factory, MemberRole.User);
		using var officer = await SignedInClient.As(_factory, MemberRole.SafetyOfficer);

		// When
		using var edited = await member.PutAsJsonAsync($"/api/v1/public/reports/{reportId}/comments/{commentId}", new { text = "Synthetic.", locale = "en-CA" });
		using var deleted = await member.DeleteAsync($"/api/v1/public/reports/{reportId}/comments/{commentId}");
		using var hidden = await officer.PostAsync($"/api/admin/comments/{commentId}/hide", null);

		// Then
		edited.StatusCode.ShouldBe(HttpStatusCode.NotFound);
		deleted.StatusCode.ShouldBe(HttpStatusCode.NotFound);
		hidden.StatusCode.ShouldBe(HttpStatusCode.NotFound);
	}

	[Fact]
	public async Task GivenHiddenComment_WhenHiddenOrEditedAgain_ThenNotFound()
	{
		// Given
		var (reportId, commentId, member) = await Commented();
		using var officer = await SignedInClient.As(_factory, MemberRole.SafetyOfficer);
		(await officer.PostAsync($"/api/admin/comments/{commentId}/hide", null)).StatusCode.ShouldBe(HttpStatusCode.NoContent);

		// When
		using var again = await officer.PostAsync($"/api/admin/comments/{commentId}/hide", null);
		using var edited = await member.PutAsJsonAsync($"/api/v1/public/reports/{reportId}/comments/{commentId}", new { text = "Synthetic.", locale = "en-CA" });

		// Then
		again.StatusCode.ShouldBe(HttpStatusCode.NotFound);
		edited.StatusCode.ShouldBe(HttpStatusCode.NotFound);
		member.Dispose();
	}

	[Fact]
	public async Task GivenReportThatIsNotPublic_WhenListingComments_ThenNotFound()
	{
		// Given
		using var visitor = _factory.CreateClient();

		// When
		using var response = await visitor.GetAsync($"/api/v1/public/reports/{TinyId.New()}/comments");

		// Then
		response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
	}

	private async Task<(string ReportId, string CommentId, HttpClient Member)> Commented()
	{
		var reportId = await Published();
		var member = await SignedInClient.As(_factory, MemberRole.User);
		using var posted = await member.PostAsJsonAsync($"/api/v1/public/reports/{reportId}/comments", new { text = "Synthetic original.", locale = "en-CA" });
		posted.StatusCode.ShouldBe(HttpStatusCode.Created);
		var commentId = (await posted.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetString()!;
		return (reportId, commentId, member);
	}

	private async Task<string> Published()
	{
		await using var scope = _factory.Services.CreateAsyncScope();
		var database = scope.ServiceProvider.GetRequiredService<HpacSafetyDbContext>();
		var now = DateTimeOffset.UtcNow;
		// The shared database may not have the consent question yet; this is
		// the same find-or-create ReportReviewEndpointTests uses.
		var consent = await database.Questions
						  .Include(question => question.Revisions)
						  .SingleOrDefaultAsync(question => question.Key == QuestionKey.ConsentPublish)
					  ?? database.Questions.Add(Question.CreateConsentPublish(
						  "May we publish a de-identified version of your report?",
						  "Pouvons-nous publier une version anonymisée de votre rapport ?",
						  now)).Entity;

		var report = new Report(Locale.EnCa, now);
		report.Answer(consent, "yes", now);
		report.BeginSummarizing();
		report.AttachSummary(Summary.Generate(report.Id, "The pilot landed.", "Le pilote s'est posé.", "synthetic-model", "synthetic.v1", now));
		report.AwaitReview();
		report.Publish("synthetic-approver", now);
		database.Reports.Add(report);
		await database.SaveChangesAsync();
		return report.Id.Value;
	}
}
