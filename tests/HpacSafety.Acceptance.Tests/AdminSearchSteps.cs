using System.Collections.Concurrent;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using HpacSafety.Api.Authentication;
using HpacSafety.Core;
using HpacSafety.Core.Features.Comments;
using HpacSafety.Core.Features.Moderation;
using HpacSafety.Core.Features.PrivateAttachments;
using HpacSafety.Core.Features.PrivateNotes;
using HpacSafety.Core.Features.QuestionBank;
using HpacSafety.Core.Features.Reporting;
using HpacSafety.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Reqnroll;
using Shouldly;

namespace HpacSafety.Acceptance.Tests;

/// <summary>
///     The admin search box's engine, through the booted API (REQ-MOD-130..133,
///     REQ-MOD-135, REQ-MOD-138, REQ-MOD-139, ADR-0156). Every seeded marker word
///     is unique to its scenario, so a match can never be another scenario's data
///     in the database this run shares.
/// </summary>
[Binding]
public sealed class AdminSearchSteps
{
	private readonly List<string> _foundIds = [];
	private string _word = string.Empty;
	private string _reportId = string.Empty;
	private string _strongId = string.Empty;
	private string _weakId = string.Empty;
	private string _pendingId = string.Empty;
	private string _publishedId = string.Empty;
	private HttpStatusCode? _lastStatus;

	[Given(@"a report carries a distinct word in its (.+)")]
	public async Task GivenAReportCarriesAWordIn(string source)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(source);

		_word = $"zzsynth{source.Replace(" ", string.Empty, StringComparison.Ordinal)}{Suffix()}";
		_reportId = await SeedReportCarrying(source, _word);
	}

	[Given(@"two reports share a word, one repeating it and one only carrying a near-miss typo of it")]
	public async Task GivenTwoReportsOneStrongOneWeak()
	{
		_word = $"zzsynthquake{Suffix()}";
		_strongId = await SeedReportCarrying("narrative", $"{_word} {_word} {_word} caused a diversion.", literal: true);
		_weakId = await SeedReportCarrying("narrative", $"A {Typo(_word)} was reported nearby.", literal: true);
	}

	[Given(@"a pending report and a published report share a distinct word")]
	public async Task GivenAPendingAndAPublishedReportShareAWord()
	{
		_word = $"zzsynthbird{Suffix()}";
		(_pendingId, _publishedId) = await SeedPendingAndPublished(_word);
	}

	[When(@"a reviewer searches for that word")]
	public async Task WhenAReviewerSearchesForThatWord()
	{
		_foundIds.Clear();
		_foundIds.AddRange(await Search(_word, null));
	}

	[When(@"a reviewer searches for a misspelling of that word")]
	public async Task WhenAReviewerSearchesForAMisspelling()
	{
		_foundIds.Clear();
		_foundIds.AddRange(await Search(Typo(_word), null));
	}

	[When(@"a reviewer searches for that word within the published filter")]
	public async Task WhenAReviewerSearchesWithinThePublishedFilter()
	{
		_foundIds.Clear();
		_foundIds.AddRange(await Search(_word, "published"));
	}

	[Given(@"a report carries a distinct word only in its private answer, its private note, and its staff-only attachment name")]
	public async Task GivenAReportCarriesAWordOnlyInPrivateSources()
	{
		_word = $"zzsynthprivateonly{Suffix()}";
		_reportId = await SeedReportCarryingPrivateOnly(_word);
	}

	[When(@"an anonymous visitor searches for that word")]
	public async Task WhenAnAnonymousVisitorSearchesForThatWord()
	{
		var factory = await BootedApi.Factory();
		await SearchAs(factory.CreateClient());
	}

	[When(@"a member searches for that word")]
	public async Task WhenAMemberSearchesForThatWord()
	{
		await SearchAs(await BootedApi.SignedInAs(MemberRole.User));
	}

	[When(@"a Safety Officer searches for that word")]
	public async Task WhenASafetyOfficerSearchesForThatWord()
	{
		await SearchAs(await BootedApi.SignedInAs(MemberRole.SafetyOfficer));
	}

	[When(@"an Administrator searches for that word")]
	public async Task WhenAnAdministratorSearchesForThatWord()
	{
		await SearchAs(await BootedApi.SignedInAs(MemberRole.Administrator));
	}

	private async Task SearchAs(HttpClient client)
	{
		using (client)
		{
			using var response = await client.GetAsync(new Uri($"/api/admin/reports?q={Uri.EscapeDataString(_word)}", UriKind.Relative));
			_lastStatus = response.StatusCode;

			if (response.StatusCode == HttpStatusCode.OK)
			{
				var body = await response.Content.ReadFromJsonAsync<JsonElement>();
				_foundIds.Clear();
				_foundIds.AddRange(body.GetProperty("items").EnumerateArray().Select(item => item.GetProperty("id").GetString()!));
			}
		}
	}

	[Then(@"the report is found")]
	public void ThenTheReportIsFound()
	{
		_foundIds.ShouldContain(_reportId);
	}

	[Then(@"the request is refused as unauthorized")]
	public void ThenRefusedAsUnauthorized()
	{
		_lastStatus.ShouldBe(HttpStatusCode.Unauthorized);
		_foundIds.ShouldBeEmpty();
	}

	[Then(@"the request is refused as forbidden")]
	public void ThenRefusedAsForbidden()
	{
		_lastStatus.ShouldBe(HttpStatusCode.Forbidden);
		_foundIds.ShouldBeEmpty();
	}

	[Then(@"the report repeating the word is listed before the one with the typo")]
	public void ThenTheStrongMatchIsListedFirst()
	{
		_foundIds.ShouldContain(_strongId);
		_foundIds.ShouldContain(_weakId);
		_foundIds.IndexOf(_strongId).ShouldBeLessThan(_foundIds.IndexOf(_weakId));
	}

	[Then(@"only the published report is found")]
	public void ThenOnlyThePublishedReportIsFound()
	{
		_foundIds.ShouldContain(_publishedId);
		_foundIds.ShouldNotContain(_pendingId);
	}

	[Then(@"the query text never appears in anything the host logs")]
	public async Task ThenTheQueryTextNeverAppearsInAnyLog()
	{
		var factory = await BootedApi.Factory();
		var messages = new ConcurrentQueue<string>();
		factory.Services.GetRequiredService<ILoggerFactory>().AddProvider(new CapturingLoggerProvider(messages));

		await Search(_word, null);

		messages.ShouldAllBe(message => !message.Contains(_word, StringComparison.Ordinal));
	}

	// ── Helpers ─────────────────────────────────────────────────────────────

	private static string Suffix()
	{
		return Guid.NewGuid().ToString("n")[..10];
	}

	private static string Typo(string word)
	{
		var middle = word.Length / 2;
		var swapped = word.ToCharArray();
		(swapped[middle], swapped[middle - 1]) = (swapped[middle - 1], swapped[middle]);
		return new string(swapped);
	}

	private static async Task<List<string>> Search(string q,
													string? filter)
	{
		using var client = await BootedApi.SignedInAs(MemberRole.SafetyOfficer);
		var path = filter is null
			? $"/api/admin/reports?q={Uri.EscapeDataString(q)}"
			: $"/api/admin/reports?filter={filter}&q={Uri.EscapeDataString(q)}";
		using var response = await client.GetAsync(new Uri(path, UriKind.Relative));
		response.EnsureSuccessStatusCode();
		var body = await response.Content.ReadFromJsonAsync<JsonElement>();
		return [.. body.GetProperty("items").EnumerateArray().Select(item => item.GetProperty("id").GetString()!)];
	}

	private static async Task<Question> ConsentQuestion(HpacSafetyDbContext database)
	{
		return await database.Questions
			.Include(question => question.Revisions)
			.SingleAsync(question => question.Role == QuestionRole.ConsentPublish);
	}

	/// <summary>Seeds one report whose named source carries a marker word.</summary>
	/// <param name="source">Which part of the report should carry it.</param>
	/// <param name="word">The marker word, or (with <paramref name="literal" />) the whole narrative text.</param>
	/// <param name="literal">
	///     True to use <paramref name="word" /> as the whole narrative text as
	///     given, rather than embedding a marker word inside a sentence.
	/// </param>
	private static async Task<string> SeedReportCarrying(string source,
														  string word,
														  bool literal = false)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(source);
		var factory = await BootedApi.Factory();
		await using var scope = factory.Services.CreateAsyncScope();
		var database = scope.ServiceProvider.GetRequiredService<HpacSafetyDbContext>();

		var now = DateTimeOffset.UtcNow.AddYears(2).AddSeconds(Random.Shared.Next(86400));
		var suffix = Guid.NewGuid().ToString("n")[..8];
		var consent = await ConsentQuestion(database);

		var report = new Report(Locale.EnCa, now);
		report.Answer(consent, true, now);

		switch (source)
		{
			case "private answer":
				{
					var narrative = Question.Create($"narrative_{suffix}", QuestionType.LongText, "What happened", "Ce qui s'est passé", now, isPrivate: true);
					database.Questions.Add(narrative);
					report.Answer(narrative, literal ? word : $"A synthetic occurrence: {word} happened near the runway.", now);
					break;
				}

			case "choice label":
				{
					var site = Question.Create(
						$"site_{suffix}",
						QuestionType.SingleSelect,
						"Site",
						"Site",
						now,
						options: [new QuestionOptionInput($"site_{suffix}", $"Site {word}", $"Lieu {word}")]);
					database.Questions.Add(site);
					report.AnswerChoices(site, site.CurrentRevision, [site.Choice($"site_{suffix}")!.Id], now);
					break;
				}

			case "summary pair":
				{
					report.BeginSummarizing();
					report.AttachSummary(Summary.Generate(report.Id, $"Summary mentions {word}.", "Résumé synthétique.", "gemini-3.7-flash", "summarize-anonymize.v3", now));
					report.AwaitReview();
					break;
				}

			case "private note":
				{
					database.Reports.Add(report);
					await database.SaveChangesAsync();
					database.PrivateNotes.Add(PrivateNote.Write(report.Id, "officer:synthetic", $"Staff note about {word}.", now));
					await database.SaveChangesAsync();
					return report.Id.Value;
				}

			case "member comment":
				{
					database.Reports.Add(report);
					await database.SaveChangesAsync();
					database.ReportComments.Add(ReportComment.Post(report.Id, "member:synthetic", $"A member comment about {word}.", Locale.EnCa, now));
					await database.SaveChangesAsync();
					return report.Id.Value;
				}

			case "reporter-uploaded attachment name":
				{
					database.Reports.Add(report);
					await database.SaveChangesAsync();
					report.AddFile(TinyId.New(), $"{report.Id}/original/doc", "application/pdf", 10, $"{word}.pdf", now);
					await database.SaveChangesAsync();
					return report.Id.Value;
				}

			case "staff-only attachment name":
				{
					database.Reports.Add(report);
					await database.SaveChangesAsync();
					var attachmentId = TinyId.New();
					database.PrivateAttachments.Add(PrivateAttachment.Add(
						attachmentId,
						report.Id,
						BlobKey.For(report.Id.Value, MediaCompartment.Private, attachmentId.Value),
						$"{word}.zip",
						"application/zip",
						2048,
						null,
						"officer:synthetic",
						now));
					await database.SaveChangesAsync();
					return report.Id.Value;
				}

			case "narrative":
				{
					var narrative = Question.Create($"narrative_{suffix}", QuestionType.LongText, "What happened", "Ce qui s'est passé", now, isPrivate: false);
					database.Questions.Add(narrative);
					report.Answer(narrative, literal ? word : $"A synthetic occurrence: {word} happened near the runway.", now);
					break;
				}

			default:
				throw new ArgumentOutOfRangeException(nameof(source), source, "No seeded source matches.");
		}

		database.Reports.Add(report);
		await database.SaveChangesAsync();
		return report.Id.Value;
	}

	/// <summary>
	///     Seeds one report whose word appears only in private-tier content — a
	///     private answer, a private note, and a staff-only attachment name —
	///     and nowhere a public or member read ever reaches (REQ-MOD-138).
	/// </summary>
	private static async Task<string> SeedReportCarryingPrivateOnly(string word)
	{
		var factory = await BootedApi.Factory();
		await using var scope = factory.Services.CreateAsyncScope();
		var database = scope.ServiceProvider.GetRequiredService<HpacSafetyDbContext>();

		var now = DateTimeOffset.UtcNow.AddYears(2).AddSeconds(Random.Shared.Next(86400));
		var suffix = Guid.NewGuid().ToString("n")[..8];
		var consent = await ConsentQuestion(database);

		var report = new Report(Locale.EnCa, now);
		report.Answer(consent, true, now);

		var narrative = Question.Create($"narrative_{suffix}", QuestionType.LongText, "What happened", "Ce qui s'est passé", now, isPrivate: true);
		database.Questions.Add(narrative);
		report.Answer(narrative, $"A synthetic occurrence: {word} happened near the runway.", now);
		database.Reports.Add(report);
		await database.SaveChangesAsync();

		database.PrivateNotes.Add(PrivateNote.Write(report.Id, "officer:synthetic", $"Staff note about {word}.", now));

		var attachmentId = TinyId.New();
		database.PrivateAttachments.Add(PrivateAttachment.Add(
			attachmentId,
			report.Id,
			BlobKey.For(report.Id.Value, MediaCompartment.Private, attachmentId.Value),
			$"{word}.zip",
			"application/zip",
			2048,
			null,
			"officer:synthetic",
			now));

		await database.SaveChangesAsync();
		return report.Id.Value;
	}

	private static async Task<(string PendingId, string PublishedId)> SeedPendingAndPublished(string sharedWord)
	{
		var factory = await BootedApi.Factory();
		await using var scope = factory.Services.CreateAsyncScope();
		var database = scope.ServiceProvider.GetRequiredService<HpacSafetyDbContext>();

		var now = DateTimeOffset.UtcNow.AddYears(2).AddSeconds(Random.Shared.Next(86400));
		var suffix = Guid.NewGuid().ToString("n")[..8];
		var consent = await ConsentQuestion(database);
		var narrative = Question.Create($"narrative_{suffix}", QuestionType.LongText, "What happened", "Ce qui s'est passé", now, isPrivate: false);
		database.Questions.Add(narrative);

		var pending = new Report(Locale.EnCa, now);
		pending.Answer(consent, true, now);
		pending.Answer(narrative, $"Report one mentions {sharedWord}.", now);
		pending.BeginSummarizing();
		pending.AttachSummary(Summary.Generate(pending.Id, "Summary.", "Résumé.", "gemini-3.7-flash", "summarize-anonymize.v3", now));
		pending.AwaitReview();
		database.Reports.Add(pending);

		var published = new Report(Locale.EnCa, now.AddSeconds(1));
		published.Answer(consent, true, now);
		published.Answer(narrative, $"Report two also mentions {sharedWord}.", now);
		published.BeginSummarizing();
		published.AttachSummary(Summary.Generate(published.Id, "Summary.", "Résumé.", "gemini-3.7-flash", "summarize-anonymize.v3", now));
		published.AwaitReview();
		published.Publish("synthetic-approver-subject", now);
		database.Reports.Add(published);

		await database.SaveChangesAsync();
		return (pending.Id.Value, published.Id.Value);
	}

	/// <summary>Keeps every message the host logs, so a scenario can prove what it never logged.</summary>
	private sealed class CapturingLoggerProvider(ConcurrentQueue<string> messages) : ILoggerProvider
	{
		public ILogger CreateLogger(string categoryName)
		{
			return new CapturingLogger(messages);
		}

		public void Dispose()
		{
		}

		private sealed class CapturingLogger(ConcurrentQueue<string> messages) : ILogger
		{
			public IDisposable? BeginScope<TState>(TState state)
				where TState : notnull
			{
				return null;
			}

			public bool IsEnabled(LogLevel logLevel)
			{
				return true;
			}

			public void Log<TState>(LogLevel logLevel,
									EventId eventId,
									TState state,
									Exception? exception,
									Func<TState, Exception?, string> formatter)
			{
				messages.Enqueue(formatter(state, exception));
			}
		}
	}
}
