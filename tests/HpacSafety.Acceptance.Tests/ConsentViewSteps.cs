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
		var listed = await client.GetFromJsonAsync<JsonElement>(new Uri("/api/admin/reports", UriKind.Relative));
		_row = listed.EnumerateArray().Single(item => item.GetProperty("id").GetString() == _reportId);
		_detail = await client.GetFromJsonAsync<JsonElement>(new Uri($"/api/admin/reports/{_reportId}", UriKind.Relative));
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
