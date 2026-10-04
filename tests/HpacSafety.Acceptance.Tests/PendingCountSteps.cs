using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using HpacSafety.Core.Features.Moderation;
using HpacSafety.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Reqnroll;
using Shouldly;

namespace HpacSafety.Acceptance.Tests;

/// <summary>
///     The Admin menu's pending counts, through the booted API (REQ-MOD-084..086).
///     Scenarios share one booted database and run in parallel, so a count is
///     compared with the list or queue it summarizes, read between two agreeing
///     reads of the count, not with a fixed number.
/// </summary>
[Binding]
public sealed class PendingCountSteps
{
	private static readonly Uri Counts = new("/api/admin/counts", UriKind.Relative);
	private static readonly Uri NeedsAction = new("/api/admin/reports?filter=needs-action", UriKind.Relative);
	private static readonly Uri Submit = new("/api/v1/reports", UriKind.Relative);
	private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

	private HttpClient? _client;
	private HttpResponseMessage? _response;
	private JsonElement _counts;

#pragma warning disable CA1822 // Reqnroll step bindings must be instance methods to be discovered.
	[Given(@"an answer is awaiting machine translation")]
	public async Task GivenAnAnswerAwaitingTranslation()
	{
		// A long-text answer is marked for machine translation off the
		// submission path (ADR-0112), and nothing in the booted host runs the
		// Worker, so it stays waiting. A choice answer never waits: it reads its
		// second language from its choice (ADR-0128).
		using var admin = await BootedApi.SignedInAs(MemberRole.Administrator);
		using var created = await admin.PostAsJsonAsync(new Uri("/api/admin/questions", UriKind.Relative), new
		{
			type = "long_text",
			labelEn = $"What happened? {Guid.NewGuid():N}",
			labelFr = $"Que s'est-il passé? {Guid.NewGuid():N}",
			isRequired = false,
			isPrivate = false,
			isActive = true,
		});
		created.StatusCode.ShouldBe(HttpStatusCode.Created, await created.Content.ReadAsStringAsync());
		var revisionId = (await created.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("revisionId").GetString();

		using var reporter = await BootedApi.SignedInAs(MemberRole.User);
		var dto = new
		{
			language = "fr-CA",
			answers = new object[]
			{
				new { questionRevisionId = await ReportSubmissionEndpointSteps.ConsentRevisionId(), value = (bool?)true },
				new { questionRevisionId = revisionId, value = (string?)"Le vent s'est levé en finale." },
			},
		};

		using var content = new StringContent(JsonSerializer.Serialize(dto, JsonOptions), System.Text.Encoding.UTF8, "application/json");
		using var response = await reporter.PostAsync(Submit, content);
		response.StatusCode.ShouldBe(HttpStatusCode.Accepted, await response.Content.ReadAsStringAsync());
	}
#pragma warning restore CA1822

	[When(@"^an? (User|Safety Officer|Administrator) reads the pending counts$")]
	public async Task WhenAMemberReadsTheCounts(string role)
	{
		_client = await BootedApi.SignedInAs(GlossaryNames.Role(role));
		_response = await _client.GetAsync(Counts);

		if (_response.IsSuccessStatusCode)
		{
			_counts = await _response.Content.ReadFromJsonAsync<JsonElement>();
		}
	}

	[Then(@"the reports count equals the number of reports the Needs action filter lists")]
	public async Task ThenTheReportsCountMatchesTheList()
	{
		await ShouldAgree("reportsNeedingAction", CountNeedsAction);
	}

	[Then(@"the counts carry no answers-awaiting-translation count")]
	public void ThenNoTranslationCount()
	{
		_counts.GetProperty("answersAwaitingTranslation").ValueKind.ShouldBe(JsonValueKind.Null);
	}

	[Then(@"the translation count equals the number of answers awaiting translation")]
	public async Task ThenTheTranslationCountMatchesTheQueue()
	{
		_counts.GetProperty("answersAwaitingTranslation").GetInt32().ShouldBeGreaterThan(0);
		await ShouldAgree("answersAwaitingTranslation", CountAwaitingTranslation);
	}

	/// <summary>
	///     There is no admin endpoint or page for this queue anymore (ADR-0174):
	///     only the Worker ever fills an answer's second language. This reads the
	///     same <c>answers_awaiting_translation</c> view the count itself reads,
	///     straight from the database, as the removed queue endpoint once did.
	/// </summary>
	private static async Task<int> CountAwaitingTranslation(HttpClient client)
	{
		_ = client;
		await using var scope = (await BootedApi.Factory()).Services.CreateAsyncScope();
		var database = scope.ServiceProvider.GetRequiredService<HpacSafetyDbContext>();
		return await database.AnswersAwaitingTranslation.CountAsync();
	}

	/// <summary>
	///     The full count of reports the Needs action filter lists, following
	///     every keyset page rather than reading only the first (REQ-MOD-129) —
	///     the shared database this suite runs against can easily hold more than
	///     one page's worth.
	/// </summary>
	private static async Task<int> CountNeedsAction(HttpClient client)
	{
		var total = 0;
		string? after = null;

		do
		{
			var query = after is null ? string.Empty : $"&after={Uri.EscapeDataString(after)}";
			var page = await client.GetFromJsonAsync<JsonElement>(new Uri($"{NeedsAction}{query}", UriKind.Relative));
			total += page.GetProperty("items").GetArrayLength();
			after = page.GetProperty("next").ValueKind == JsonValueKind.String ? page.GetProperty("next").GetString() : null;
		} while (after is not null);

		return total;
	}

	[Then(@"the pending counts are refused as forbidden")]
	public void ThenRefused()
	{
		_response!.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
	}

	/// <summary>
	///     Reads the count, the thing it counts, and the count again, and accepts
	///     only when all three agree. A parallel scenario may add a queue entry and
	///     remove it again between the reads, leaving the two counts equal while
	///     the middle read differs, so any disagreement retries after a short
	///     delay until the attempts run out.
	/// </summary>
	private async Task ShouldAgree(string property,
								   Func<HttpClient, Task<int>> count)
	{
		const int attempts = 10;
		var delay = TimeSpan.FromMilliseconds(200);
		var client = _client.ShouldNotBeNull();
		var (before, counted, after) = (0, 0, 0);

		for (var attempt = 1; attempt <= attempts; attempt++)
		{
			before = (await client.GetFromJsonAsync<JsonElement>(Counts)).GetProperty(property).GetInt32();
			counted = await count(client);
			after = (await client.GetFromJsonAsync<JsonElement>(Counts)).GetProperty(property).GetInt32();

			if (before == counted && counted == after)
			{
				return;
			}

			await Task.Delay(delay);
		}

		throw new ShouldAssertException(
			$"{property} never agreed with its own count across {attempts} attempts; last reads: count before {before}, listed {counted}, count after {after}");
	}
}
