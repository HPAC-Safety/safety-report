using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using HpacSafety.Core.Features.Moderation;
using HpacSafety.Core.Features.QuestionBank;
using HpacSafety.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Reqnroll;
using Shouldly;

namespace HpacSafety.Acceptance.Tests;

/// <summary>
///     REQ-QB-052 — a grouped question is ungrouped when its group is deleted or
///     retyped, through the booted API and its database: whether a child has been
///     answered is read from reports, and what the reporter's form shows is the
///     public endpoint's.
/// </summary>
/// <remarks>
///     Scenarios share one database, so every question here gets its own key and
///     every assertion reads back only the rows this scenario wrote. Every
///     question and answer is synthetic.
/// </remarks>
[Binding]
public sealed class QuestionUngroupingSteps
{
#pragma warning disable CA1822 // Reqnroll step bindings must be instance methods to be discovered.

	private static readonly Uri AdminQuestions = new("/api/admin/questions", UriKind.Relative);
	private static readonly Uri PublicQuestions = new("/api/v1/questions", UriKind.Relative);
	private static readonly Uri Submit = new("/api/v1/reports", UriKind.Relative);

	private HttpClient? _admin;
	private string? _groupId;
	private string? _groupKey;
	private string? _firstKey;
	private string? _secondKey;
	private string? _laterKey;
	private string? _firstId;
	private bool _groupDeleted;

	[Given(@"^a group question has two questions grouped under it, the first with (no answers|an answer)$")]
	public async Task GivenAGroupWithTwoChildren(string answers)
	{
		_groupKey = Key("heading");
		_firstKey = Key("first");
		_secondKey = Key("second");

		_groupId = (await Create(_groupKey, "group", null)).GetProperty("id").GetString();
		var first = await Create(_firstKey, "short_text", _groupId);
		_firstId = first.GetProperty("id").GetString();
		await Create(_secondKey, "short_text", _groupId);

		if (answers == "an answer")
		{
			await SubmitAnswering(first.GetProperty("revisionId").GetString()!);
		}
	}

	[Given(@"a later question follows the group on the form")]
	public async Task GivenALaterQuestionFollowsTheGroup()
	{
		_laterKey = Key("later");
		await Create(_laterKey, "short_text", null);
	}

	[When(@"an Administrator deletes the group")]
	public async Task WhenAnAdministratorDeletesTheGroup()
	{
		using var response = await (await Admin()).DeleteAsync(new Uri($"/api/admin/questions/{_groupId}", UriKind.Relative));
		response.StatusCode.ShouldBe(HttpStatusCode.NoContent, await response.Content.ReadAsStringAsync());
		_groupDeleted = true;
	}

	[When(@"an Administrator retypes the group to a type other than group")]
	public async Task WhenAnAdministratorRetypesTheGroup()
	{
		using var response = await (await Admin()).PutAsJsonAsync(
			new Uri($"/api/admin/questions/{_groupId}", UriKind.Relative), Request(_groupKey!, "short_text", null));
		response.StatusCode.ShouldBe(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
	}

	[Then(@"the first grouped question gets a new revision that is ungrouped")]
	public async Task ThenTheFirstGetsANewRevision()
	{
		var rows = await Rows(_firstKey!);
		var question = rows.ShouldHaveSingleItem();

		question.Id.Value.ShouldBe(_firstId);
		question.Revisions.Count.ShouldBe(2);
		question.GroupedUnderQuestionId.ShouldBeNull();
	}

	[Then(@"the first grouped question is retired and replaced by a new question with its key that is ungrouped")]
	public async Task ThenTheFirstIsReplaced()
	{
		var rows = await Rows(_firstKey!);
		rows.Count.ShouldBe(2);

		var retired = rows.Single(question => question.Deleted is not null);
		var live = rows.Single(question => question.Deleted is null);

		retired.Id.Value.ShouldBe(_firstId);
		live.Id.ShouldNotBe(retired.Id);
		live.GroupedUnderQuestionId.ShouldBeNull();

		// The answer keeps the question, and so the wording, it was given to.
		retired.GroupedUnderQuestionId.ShouldNotBeNull();
	}

	[Then(@"the second grouped question gets a new revision that is ungrouped")]
	public async Task ThenTheSecondGetsANewRevision()
	{
		var question = (await Rows(_secondKey!)).ShouldHaveSingleItem();

		question.Revisions.Count.ShouldBe(2);
		question.GroupedUnderQuestionId.ShouldBeNull();
	}

	[Then(@"the reporter's form lists both as entries of their own, in their former order, at the group's place")]
	public async Task ThenTheFormListsBoth()
	{
		var keys = await FormKeys();

		keys.ShouldContain(_firstKey!);
		keys.ShouldContain(_secondKey!);

		var first = keys.IndexOf(_firstKey!);
		keys.IndexOf(_secondKey!).ShouldBe(first + 1);

		if (_groupDeleted)
		{
			keys.ShouldNotContain(_groupKey!);
		}
		else
		{
			// A retyped group keeps its slot and its children follow it.
			keys.IndexOf(_groupKey!).ShouldBe(first - 1);
		}
	}

	[Then(@"the later question comes after them")]
	public async Task ThenTheLaterQuestionComesAfterThem()
	{
		var keys = await FormKeys();
		keys.IndexOf(_laterKey!).ShouldBe(keys.IndexOf(_secondKey!) + 1);
	}

	private async Task<HttpClient> Admin()
	{
		return _admin ??= await BootedApi.SignedInAs(MemberRole.Administrator);
	}

	private static string Key(string prefix)
	{
		var key = $"{prefix}_{Guid.NewGuid():N}";
		return key[..Math.Min(key.Length, 40)];
	}

	private static object Request(string key,
								  string type,
								  string? groupedUnderQuestionId)
	{
		return new
		{
			key,
			type,
			labelEn = "A synthetic question",
			labelFr = "Une question synthétique",
			isRequired = false,
			isPrivate = false,
			isActive = true,
			groupedUnderQuestionId,
		};
	}

	private async Task<JsonElement> Create(string key,
										   string type,
										   string? groupedUnderQuestionId)
	{
		using var response = await (await Admin()).PostAsJsonAsync(AdminQuestions, Request(key, type, groupedUnderQuestionId));
		response.StatusCode.ShouldBe(HttpStatusCode.Created, await response.Content.ReadAsStringAsync());
		return await response.Content.ReadFromJsonAsync<JsonElement>();
	}

	/// <summary>Files a report answering this revision.</summary>
	private static async Task SubmitAnswering(string revisionId)
	{
		var consent = await ReportSubmissionEndpointSteps.ConsentRevisionId();
		using var reporter = await BootedApi.SignedInAs(MemberRole.User);

		using var response = await reporter.PostAsJsonAsync(Submit, new
		{
			language = "en-CA",
			answers = new object[]
			{
				new { questionRevisionId = consent, value = (bool?)false },
				new { questionRevisionId = revisionId, value = (string?)"A synthetic answer" },
			},
		});

		response.StatusCode.ShouldBe(HttpStatusCode.Accepted, await response.Content.ReadAsStringAsync());
	}

	/// <summary>Every row holding this key, retired ones included.</summary>
	private static async Task<List<Question>> Rows(string key)
	{
		await using var scope = (await BootedApi.Factory()).Services.CreateAsyncScope();
		var database = scope.ServiceProvider.GetRequiredService<HpacSafetyDbContext>();

		return await database.Questions
			.IgnoreQueryFilters()
			.Include(question => question.Revisions)
			.AsNoTracking()
			.Where(question => question.Key == key)
			.ToListAsync();
	}

	/// <summary>The keys of the form's top-level entries, in the order the reporter meets them.</summary>
	private static async Task<List<string>> FormKeys()
	{
		using var anonymous = (await BootedApi.Factory()).CreateClient();
		var form = await anonymous.GetFromJsonAsync<JsonElement>(PublicQuestions);

		return [.. form.EnumerateArray().Select(entry => entry.GetProperty("key").GetString()!)];
	}
}
