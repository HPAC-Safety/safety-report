using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using HpacSafety.Core.Features.Moderation;
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
	private static readonly Uri AwaitingTranslation = new("/api/admin/answers/awaiting-translation", UriKind.Relative);
	private static readonly Uri Submit = new("/api/v1/reports", UriKind.Relative);
	private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

	private HttpClient? _client;
	private HttpResponseMessage? _response;
	private JsonElement _counts;

#pragma warning disable CA1822 // Reqnroll step bindings must be instance methods to be discovered.
	[Given(@"an answer is awaiting machine translation")]
	public async Task GivenAnAnswerAwaitingTranslation()
	{
		// A type-ahead value naming no bilingual choice is filled by machine
		// translation off the submission path (ADR-0112), and nothing in the
		// booted host runs the Worker, so it stays waiting.
		using var admin = await BootedApi.SignedInAs(MemberRole.Administrator);
		var revisionId = await ReporterChoiceSubmissionSteps.CreateTypeAhead(admin);

		using var reporter = await BootedApi.SignedInAs(MemberRole.User);
		var dto = new
		{
			language = "fr-CA",
			answers = new object[]
			{
				new { questionRevisionId = await ReportSubmissionEndpointSteps.ConsentRevisionId(), value = (string?)"oui" },
				new { questionRevisionId = revisionId, value = (string?)$"Site {Guid.NewGuid():N}"[..16] },
			},
		};

		using var content = new StringContent(JsonSerializer.Serialize(dto, JsonOptions), System.Text.Encoding.UTF8, "application/json");
		using var response = await reporter.PostAsync(Submit, content);
		response.StatusCode.ShouldBe(HttpStatusCode.Accepted, await response.Content.ReadAsStringAsync());
	}
#pragma warning restore CA1822

	[When(@"a {word} reads the pending counts")]
	[When(@"an {word} reads the pending counts")]
	public async Task WhenAMemberReadsTheCounts(string role)
	{
		_client = await BootedApi.SignedInAs(Enum.Parse<MemberRole>(role));
		_response = await _client.GetAsync(Counts);

		if (_response.IsSuccessStatusCode)
		{
			_counts = await _response.Content.ReadFromJsonAsync<JsonElement>();
		}
	}

	[Then(@"the reports count equals the number of reports the Needs action filter lists")]
	public async Task ThenTheReportsCountMatchesTheList()
	{
		await ShouldAgree("reportsNeedingAction", NeedsAction, listed => listed.GetArrayLength());
	}

	[Then(@"the counts carry no answers-awaiting-translation count")]
	public void ThenNoTranslationCount()
	{
		_counts.GetProperty("answersAwaitingTranslation").ValueKind.ShouldBe(JsonValueKind.Null);
	}

	[Then(@"the translation count equals the number of answers in the translation queue")]
	public async Task ThenTheTranslationCountMatchesTheQueue()
	{
		_counts.GetProperty("answersAwaitingTranslation").GetInt32().ShouldBeGreaterThan(0);
		await ShouldAgree("answersAwaitingTranslation", AwaitingTranslation, queue => queue.GetProperty("answers").GetArrayLength());
	}

	[Then(@"the API refuses the pending counts with 403")]
	public void ThenRefused()
	{
		_response!.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
	}

	/// <summary>
	///     Reads the count, the thing it counts, and the count again; when the two
	///     counts agree nothing changed in between, so the middle read must match.
	/// </summary>
	private async Task ShouldAgree(string property,
								   Uri source,
								   Func<JsonElement, int> size)
	{
		var client = _client.ShouldNotBeNull();

		for (var attempt = 1; ; attempt++)
		{
			var before = (await client.GetFromJsonAsync<JsonElement>(Counts)).GetProperty(property).GetInt32();
			var counted = size(await client.GetFromJsonAsync<JsonElement>(source));
			var after = (await client.GetFromJsonAsync<JsonElement>(Counts)).GetProperty(property).GetInt32();

			if (before == after || attempt == 5)
			{
				counted.ShouldBe(after);
				return;
			}
		}
	}
}
