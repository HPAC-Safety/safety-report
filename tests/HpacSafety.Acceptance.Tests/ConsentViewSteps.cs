using System.Net.Http.Json;
using System.Text.Json;
using HpacSafety.Core.Features.Moderation;
using HpacSafety.Core.Features.Reporting;
using Reqnroll;
using Shouldly;

namespace HpacSafety.Acceptance.Tests;

/// <summary>
///     REQ-MOD-096: the admin view gives a report's publication and media consent as
///     a JSON <c>true</c>, <c>false</c>, or <c>null</c>, never a word (ADR-0130, #495).
///     Reports are synthetic and seeded through the lifecycle.
/// </summary>
[Binding]
public sealed class ConsentViewSteps
{
	private string? _reportId;
	private JsonElement _row;
	private JsonElement _detail;

	[Given(@"^a report whose publication consent is (given|refused) and whose media consent is (given|refused|not asked)$")]
	public async Task GivenAReportWithConsent(string publication,
											  string media)
	{
		var consent = publication == "given";
		bool? mediaConsent = media switch
		{
			"given" => true,
			"refused" => false,
			_ => null,
		};

		_reportId = await BootedReports.Seed(
			consent ? ReportStatus.Pending : ReportStatus.Unpublished, consent, mediaConsent: mediaConsent);
	}

	[When(@"a safety officer reads the report list and the report's detail")]
	public async Task WhenASafetyOfficerReadsTheReport()
	{
		using var client = await BootedApi.SignedInAs(MemberRole.SafetyOfficer);
		_row = await FindInList(client, _reportId!);
		_detail = await client.GetFromJsonAsync<JsonElement>(new Uri($"/api/admin/reports/{_reportId}", UriKind.Relative));
	}

	/// <summary>
	///     Follows every keyset page of the admin list (REQ-MOD-129) until it
	///     finds this report — it is seeded at "now," not a far-future instant,
	///     so it can sit well past the first page in this suite's shared,
	///     ever-growing database.
	/// </summary>
	private static async Task<JsonElement> FindInList(HttpClient client,
													   string reportId)
	{
		string? after = null;

		do
		{
			var query = after is null ? string.Empty : $"?after={Uri.EscapeDataString(after)}";
			var page = await client.GetFromJsonAsync<JsonElement>(new Uri($"/api/admin/reports{query}", UriKind.Relative));
			var found = page.GetProperty("items").EnumerateArray()
				.FirstOrDefault(item => item.GetProperty("id").GetString() == reportId);

			if (found.ValueKind != JsonValueKind.Undefined)
			{
				return found;
			}

			after = page.GetProperty("next").ValueKind == JsonValueKind.String ? page.GetProperty("next").GetString() : null;
		} while (after is not null);

		throw new ShouldAssertException($"Report {reportId} was not found on any page of the admin list.");
	}

	[Then(@"^the list row and the detail give consent as (true|false|null)$")]
	public void ThenConsentIs(string expected)
	{
		Json(_row.GetProperty("consent")).ShouldBe(expected);
		Json(_detail.GetProperty("consent")).ShouldBe(expected);
	}

	[Then(@"^the detail gives media consent as (true|false|null)$")]
	public void ThenMediaConsentIs(string expected)
	{
		Json(_detail.GetProperty("mediaConsent")).ShouldBe(expected);
	}

	/// <summary>The raw JSON token, so a word such as <c>"yes"</c> can never pass for <c>true</c>.</summary>
	private static string Json(JsonElement element)
	{
		return element.GetRawText();
	}
}
