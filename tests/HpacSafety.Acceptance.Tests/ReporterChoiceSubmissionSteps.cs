using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Shouldly;

namespace HpacSafety.Acceptance.Tests;

/// <summary>
///     A type-ahead question created through the admin API, for the scenarios that
///     submit a reporter's typed value over HTTP (ADR-0129). What the domain
///     decides on its own is covered by <see cref="ReporterAddedChoiceSteps" />.
/// </summary>
public static class ReporterChoiceSubmissionSteps
{
	private static readonly Uri AdminQuestions = new("/api/admin/questions", UriKind.Relative);

	/// <summary>Creates a type-ahead offering one written choice, returning its current revision.</summary>
	internal static async Task<string> CreateTypeAhead(HttpClient admin)
	{
		return (await CreateTypeAheadQuestion(admin)).RevisionId;
	}

	private static async Task<TypeAhead> CreateTypeAheadQuestion(HttpClient admin)
	{
		var key = $"launch_site_{Guid.NewGuid().ToString("N")[..12]}";
		using var createdQuestion = await admin.PostAsJsonAsync(AdminQuestions, new
		{
			key,
			type = "autocomplete",
			labelEn = "Where did you launch?",
			labelFr = "D'où avez-vous décollé?",
			isRequired = false,
			isPrivate = false,
			isActive = true,
			options = new[] { new { code = (string?)null, labelEn = "Mount 7", labelFr = "Mont 7" } },
		});
		createdQuestion.StatusCode.ShouldBe(HttpStatusCode.Created, await createdQuestion.Content.ReadAsStringAsync());
		var created = await createdQuestion.Content.ReadFromJsonAsync<JsonElement>();

		return new TypeAhead(key, created.GetProperty("id").GetString()!, created.GetProperty("revisionId").GetString()!);
	}

	private sealed record TypeAhead(string Key, string Id, string RevisionId);
}
