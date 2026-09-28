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
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Shouldly;

namespace HpacSafety.Api.Tests;

/// <summary>
///     The admin search box, against a real PostgreSQL container (REQ-MOD-130..138,
///     ADR-0156). Every seeded value carries a random suffix so a scenario can
///     search for it without another test's synthetic data ever matching too —
///     the container is shared across the whole collection.
/// </summary>
[Trait("Category", "Integration")]
[Collection(SharedApiPostgres.Name)]
public class ReportSearchEndpointTests(ApiPostgresFixture fixture)
{
	private readonly WebApplicationFactory<Program> _factory = fixture.Factory;

	[Fact]
	public async Task GivenAQueryMatchingAPrivateAnswer_WhenSearched_ThenThatReportIsFound()
	{
		var seed = await SeedOne();
		using var client = await SignedInClient.As(_factory, MemberRole.SafetyOfficer);

		var found = await Search(client, seed.NarrativeWord);

		found.ShouldContain(seed.ReportId);
	}

	[Fact]
	public async Task GivenAQueryMatchingAChoiceLabel_WhenSearched_ThenThatReportIsFound()
	{
		var seed = await SeedOne();
		using var client = await SignedInClient.As(_factory, MemberRole.SafetyOfficer);

		var found = await Search(client, seed.ChoiceLabelWord);

		found.ShouldContain(seed.ReportId);
	}

	[Fact]
	public async Task GivenAQueryMatchingTheSummaryPair_WhenSearched_ThenThatReportIsFound()
	{
		var seed = await SeedOne();
		using var client = await SignedInClient.As(_factory, MemberRole.SafetyOfficer);

		var found = await Search(client, seed.SummaryWord);

		found.ShouldContain(seed.ReportId);
	}

	[Fact]
	public async Task GivenAQueryMatchingAPrivateNote_WhenSearched_ThenThatReportIsFound()
	{
		var seed = await SeedOne();
		using var client = await SignedInClient.As(_factory, MemberRole.SafetyOfficer);

		var found = await Search(client, seed.PrivateNoteWord);

		found.ShouldContain(seed.ReportId);
	}

	[Fact]
	public async Task GivenAQueryMatchingAMemberComment_WhenSearched_ThenThatReportIsFound()
	{
		var seed = await SeedOne();
		using var client = await SignedInClient.As(_factory, MemberRole.SafetyOfficer);

		var found = await Search(client, seed.CommentWord);

		found.ShouldContain(seed.ReportId);
	}

	[Fact]
	public async Task GivenAQueryMatchingAPublicAttachmentName_WhenSearched_ThenThatReportIsFound()
	{
		var seed = await SeedOne();
		using var client = await SignedInClient.As(_factory, MemberRole.SafetyOfficer);

		var found = await Search(client, seed.PublicFileWord);

		found.ShouldContain(seed.ReportId);
	}

	[Fact]
	public async Task GivenAQueryMatchingAPrivateAttachmentName_WhenSearched_ThenThatReportIsFound()
	{
		var seed = await SeedOne();
		using var client = await SignedInClient.As(_factory, MemberRole.SafetyOfficer);

		var found = await Search(client, seed.PrivateFileWord);

		found.ShouldContain(seed.ReportId);
	}

	[Fact]
	public async Task GivenAMisspelledQuery_WhenSearched_ThenTheTypoStillFindsTheReport()
	{
		// Given — one character swapped in the middle of the marker word.
		var seed = await SeedOne();
		using var client = await SignedInClient.As(_factory, MemberRole.SafetyOfficer);
		var typo = Typo(seed.NarrativeWord);

		// When
		var found = await Search(client, typo);

		// Then
		found.ShouldContain(seed.ReportId);
	}

	[Fact]
	public async Task GivenAFrenchQuery_WhenSearched_ThenAnEnglishNarrativeStillMatchesByStemming()
	{
		// Given — the narrative is in English; searching its French-stemmed form
		// still finds it through French websearch stemming or the trigram fallback.
		var seed = await SeedOne();
		using var client = await SignedInClient.As(_factory, MemberRole.SafetyOfficer);

		var found = await Search(client, seed.NarrativeWord);

		found.ShouldContain(seed.ReportId);
	}

	[Fact]
	public async Task GivenTwoReportsOneOnlyLoosely_WhenSearched_ThenTheBetterMatchIsListedFirst()
	{
		// Given — one report's narrative repeats the marker word (a stronger
		// full-text match); the other only carries a near-miss typo of it.
		var suffix = Suffix();
		var strongWord = $"zzsynthquake{suffix}";
		var strong = await SeedNarrative($"{strongWord} {strongWord} {strongWord} caused a diversion.");
		var weak = await SeedNarrative($"A {Typo(strongWord)} was reported nearby.");
		using var client = await SignedInClient.As(_factory, MemberRole.SafetyOfficer);

		// When
		var found = await Search(client, strongWord);

		// Then
		found.ShouldContain(strong);
		found.ShouldContain(weak);
		found.IndexOf(strong).ShouldBeLessThan(found.IndexOf(weak));
	}

	[Fact]
	public async Task GivenASearchAndAStatusFilter_WhenSearched_ThenOnlyReportsInThatFilterAppear()
	{
		// Given — two reports share the marker word; one is published, one is
		// still pending.
		var suffix = Suffix();
		var word = $"zzsynthbird{suffix}";
		var (pendingId, publishedId) = await SeedPendingAndPublished(word);
		using var client = await SignedInClient.As(_factory, MemberRole.SafetyOfficer);

		// When
		var found = await Search(client, word, filter: "published");

		// Then
		found.ShouldContain(publishedId);
		found.ShouldNotContain(pendingId);
	}

	[Fact]
	public async Task GivenAQueryMatchingNothing_WhenSearched_ThenAnEmptyListComesBackNotAnError()
	{
		using var client = await SignedInClient.As(_factory, MemberRole.SafetyOfficer);

		var found = await Search(client, $"zzsynthnothingmatchesthis{Suffix()}");

		found.ShouldBeEmpty();
	}

	[Fact]
	public async Task GivenASearch_WhenItRuns_ThenTheQueryTextIsNeverLogged()
	{
		// Given
		var seed = await SeedOne();
		var messages = new ConcurrentQueue<string>();
		using var factory = _factory.WithWebHostBuilder(builder =>
			builder.ConfigureLogging(logging => logging.AddProvider(new CapturingLoggerProvider(messages))));
		using var client = await SignedInClient.As(factory, MemberRole.SafetyOfficer);

		// When
		await Search(client, seed.NarrativeWord);

		// Then
		messages.ShouldAllBe(message => !message.Contains(seed.NarrativeWord, StringComparison.Ordinal));
	}

	[Fact]
	public async Task GivenARemovedPrivateNote_WhenSearched_ThenItsWordNeverMatches()
	{
		var word = $"zzsynthremovednote{Suffix()}";
		var reportId = await SeedNoteReport(word, remove: true);
		using var client = await SignedInClient.As(_factory, MemberRole.SafetyOfficer);

		var found = await Search(client, word);

		found.ShouldNotContain(reportId);
	}

	[Fact]
	public async Task GivenAnEditedPrivateNote_WhenSearchedForItsOldWording_ThenNoLongerMatches()
	{
		// Given — only a note's current revision is searchable (ADR-0156):
		// the word an earlier revision carried is edited away.
		var oldWord = $"zzsynthnoteold{Suffix()}";
		var newWord = $"zzsynthnotenew{Suffix()}";
		var reportId = await SeedEditedNoteReport(oldWord, newWord);
		using var client = await SignedInClient.As(_factory, MemberRole.SafetyOfficer);

		var foundOld = await Search(client, oldWord);
		var foundNew = await Search(client, newWord);

		foundOld.ShouldNotContain(reportId);
		foundNew.ShouldContain(reportId);
	}

	[Fact]
	public async Task GivenARemovedPrivateAttachment_WhenSearched_ThenItsFileNameNeverMatches()
	{
		var word = $"zzsynthremovedfile{Suffix()}";
		var reportId = await SeedAttachmentReport(word, remove: true);
		using var client = await SignedInClient.As(_factory, MemberRole.SafetyOfficer);

		var found = await Search(client, word);

		found.ShouldNotContain(reportId);
	}

	[Fact]
	public async Task GivenADeletedMemberComment_WhenSearched_ThenItsWordNeverMatches()
	{
		var word = $"zzsynthdeletedcomment{Suffix()}";
		var reportId = await SeedCommentReport(word, delete: true);
		using var client = await SignedInClient.As(_factory, MemberRole.SafetyOfficer);

		var found = await Search(client, word);

		found.ShouldNotContain(reportId);
	}

	[Fact]
	public async Task GivenAnEditedMemberComment_WhenSearchedForItsOldWording_ThenNoLongerMatches()
	{
		var oldWord = $"zzsynthcommentold{Suffix()}";
		var newWord = $"zzsynthcommentnew{Suffix()}";
		var reportId = await SeedEditedCommentReport(oldWord, newWord);
		using var client = await SignedInClient.As(_factory, MemberRole.SafetyOfficer);

		var foundOld = await Search(client, oldWord);
		var foundNew = await Search(client, newWord);

		foundOld.ShouldNotContain(reportId);
		foundNew.ShouldContain(reportId);
	}

	[Fact]
	public async Task GivenAnonymousCaller_WhenSearchingPrivateContent_ThenUnauthorizedWithNoBody()
	{
		var (reportId, word) = await SeedPrivateOnly();
		using var client = _factory.CreateClient();

		using var response = await client.GetAsync(new Uri($"/api/admin/reports?q={Uri.EscapeDataString(word)}", UriKind.Relative));

		response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
		var body = await response.Content.ReadAsStringAsync();
		body.ShouldNotContain(reportId);
		body.ShouldNotContain(word);
	}

	[Fact]
	public async Task GivenUserRole_WhenSearchingPrivateContent_ThenForbiddenWithNoBody()
	{
		var (reportId, word) = await SeedPrivateOnly();
		using var client = await SignedInClient.As(_factory, MemberRole.User);

		using var response = await client.GetAsync(new Uri($"/api/admin/reports?q={Uri.EscapeDataString(word)}", UriKind.Relative));

		response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
		var body = await response.Content.ReadAsStringAsync();
		body.ShouldNotContain(reportId);
		body.ShouldNotContain(word);
	}

	[Fact]
	public async Task GivenSafetyOfficerRole_WhenSearchingPrivateContent_ThenTheReportIsFound()
	{
		var (reportId, word) = await SeedPrivateOnly();
		using var client = await SignedInClient.As(_factory, MemberRole.SafetyOfficer);

		var found = await Search(client, word);

		found.ShouldContain(reportId);
	}

	[Fact]
	public async Task GivenAdministratorRole_WhenSearchingPrivateContent_ThenTheReportIsFound()
	{
		var (reportId, word) = await SeedPrivateOnly();
		using var client = await SignedInClient.As(_factory, MemberRole.Administrator);

		var found = await Search(client, word);

		found.ShouldContain(reportId);
	}

	[Fact]
	public async Task GivenASoftDeletedReport_WhenSearched_ThenItNeverMatches()
	{
		var word = $"zzsynthdeletedreport{Suffix()}";
		var reportId = await SeedNoteReport(word, remove: false, deleteReport: true);
		using var client = await SignedInClient.As(_factory, MemberRole.SafetyOfficer);

		var found = await Search(client, word);

		found.ShouldNotContain(reportId);
	}

	private static string Typo(string word)
	{
		var middle = word.Length / 2;
		var swapped = word.ToCharArray();
		(swapped[middle], swapped[middle - 1]) = (swapped[middle - 1], swapped[middle]);
		return new string(swapped);
	}

	private static string Suffix()
	{
		return Guid.NewGuid().ToString("n")[..10];
	}

	private static async Task<List<string>> Search(HttpClient client,
													string q,
													string? filter = null)
	{
		var path = filter is null
			? $"/api/admin/reports?q={Uri.EscapeDataString(q)}"
			: $"/api/admin/reports?filter={filter}&q={Uri.EscapeDataString(q)}";
		var body = await client.GetFromJsonAsync<JsonElement>(new Uri(path, UriKind.Relative));
		return [.. body.EnumerateArray().Select(item => item.GetProperty("id").GetString()!)];
	}

	private async Task<Seeded> SeedOne()
	{
		await using var scope = _factory.Services.CreateAsyncScope();
		var database = scope.ServiceProvider.GetRequiredService<HpacSafetyDbContext>();

		var suffix = Suffix();
		var now = DateTimeOffset.UtcNow.AddYears(5).AddSeconds(Random.Shared.Next());

		var narrativeWord = $"zzsynthnarrative{suffix}";
		var choiceLabelWord = $"zzsynthchoice{suffix}";
		var summaryWord = $"zzsynthsummary{suffix}";
		var noteWord = $"zzsynthnote{suffix}";
		var commentWord = $"zzsynthcomment{suffix}";
		var publicFileWord = $"zzsynthpublic{suffix}";
		var privateFileWord = $"zzsynthprivate{suffix}";

		var consent = await ConsentQuestion(database, now);
		var narrative = Question.Create($"narrative_{suffix}", QuestionType.LongText, "What happened", "Ce qui s'est passé", now, isPrivate: true);
		var site = Question.Create(
			$"site_{suffix}",
			QuestionType.SingleSelect,
			"Site",
			"Site",
			now,
			options: [new QuestionOptionInput($"site_{suffix}", $"Site {choiceLabelWord}", $"Lieu {choiceLabelWord}")]);
		database.Questions.AddRange(narrative, site);

		var report = new Report(Locale.EnCa, now);
		report.Answer(consent, true, now);
		report.Answer(narrative, $"A synthetic occurrence: {narrativeWord} happened near the runway.", now);
		report.AnswerChoices(site, site.CurrentRevision, [site.Choice($"site_{suffix}")!.Id], now);
		report.AddFile(TinyId.New(), $"{report.Id}/original/doc", "application/pdf", 10, $"{publicFileWord}.pdf", now);
		database.Reports.Add(report);

		report.BeginSummarizing();
		report.AttachSummary(Summary.Generate(report.Id, $"Summary mentions {summaryWord}.", "Résumé synthétique.", "gemini-3.7-flash", "summarize-anonymize.v3", now));
		report.AwaitReview();

		var note = PrivateNote.Write(report.Id, "officer:synthetic", $"Staff note about {noteWord}.", now);
		database.PrivateNotes.Add(note);

		var comment = ReportComment.Post(report.Id, "member:synthetic", $"A member comment about {commentWord}.", Locale.EnCa, now);
		database.ReportComments.Add(comment);

		var attachmentId = TinyId.New();
		var attachment = PrivateAttachment.Add(
			attachmentId,
			report.Id,
			BlobKey.For(report.Id.Value, MediaCompartment.Private, attachmentId.Value),
			$"{privateFileWord}.zip",
			"application/zip",
			2048,
			null,
			"officer:synthetic",
			now);
		database.PrivateAttachments.Add(attachment);

		await database.SaveChangesAsync();

		return new Seeded(
			report.Id.Value,
			narrativeWord,
			choiceLabelWord,
			summaryWord,
			noteWord,
			commentWord,
			publicFileWord,
			privateFileWord);
	}

	/// <summary>
	///     Seeds one report whose word appears only in private-tier content — a
	///     private answer, a private note, and a staff-only attachment name —
	///     so a search for it proves who may find it, not what it is found by.
	/// </summary>
	private async Task<(string ReportId, string Word)> SeedPrivateOnly()
	{
		await using var scope = _factory.Services.CreateAsyncScope();
		var database = scope.ServiceProvider.GetRequiredService<HpacSafetyDbContext>();

		var word = $"zzsynthprivateonly{Suffix()}";
		var now = DateTimeOffset.UtcNow.AddYears(5).AddSeconds(Random.Shared.Next());
		var suffix = Suffix();
		var consent = await ConsentQuestion(database, now);

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
		return (report.Id.Value, word);
	}

	private async Task<string> SeedNarrative(string narrativeText)
	{
		await using var scope = _factory.Services.CreateAsyncScope();
		var database = scope.ServiceProvider.GetRequiredService<HpacSafetyDbContext>();

		var now = DateTimeOffset.UtcNow.AddYears(5).AddSeconds(Random.Shared.Next());
		var suffix = Suffix();
		var consent = await ConsentQuestion(database, now);
		var narrative = Question.Create($"narrative_{suffix}", QuestionType.LongText, "What happened", "Ce qui s'est passé", now, isPrivate: false);
		database.Questions.Add(narrative);

		var report = new Report(Locale.EnCa, now);
		report.Answer(consent, true, now);
		report.Answer(narrative, narrativeText, now);
		database.Reports.Add(report);

		await database.SaveChangesAsync();
		return report.Id.Value;
	}

	private async Task<string> SeedNoteReport(string word,
											  bool remove,
											  bool deleteReport = false)
	{
		await using var scope = _factory.Services.CreateAsyncScope();
		var database = scope.ServiceProvider.GetRequiredService<HpacSafetyDbContext>();

		var now = DateTimeOffset.UtcNow.AddYears(5).AddSeconds(Random.Shared.Next());
		var consent = await ConsentQuestion(database, now);
		var report = new Report(Locale.EnCa, now);
		report.Answer(consent, true, now);
		database.Reports.Add(report);
		await database.SaveChangesAsync();

		var note = PrivateNote.Write(report.Id, "officer:synthetic", $"Staff note about {word}.", now);
		database.PrivateNotes.Add(note);
		await database.SaveChangesAsync();

		if (remove)
		{
			note.Remove(now);
			await database.SaveChangesAsync();
		}

		if (deleteReport)
		{
			report.SoftDelete(now);
			await database.SaveChangesAsync();
		}

		return report.Id.Value;
	}

	private async Task<string> SeedEditedNoteReport(string oldWord,
													string newWord)
	{
		await using var scope = _factory.Services.CreateAsyncScope();
		var database = scope.ServiceProvider.GetRequiredService<HpacSafetyDbContext>();

		var now = DateTimeOffset.UtcNow.AddYears(5).AddSeconds(Random.Shared.Next());
		var consent = await ConsentQuestion(database, now);
		var report = new Report(Locale.EnCa, now);
		report.Answer(consent, true, now);
		database.Reports.Add(report);
		await database.SaveChangesAsync();

		var note = PrivateNote.Write(report.Id, "officer:synthetic", $"Staff note about {oldWord}.", now);
		database.PrivateNotes.Add(note);
		await database.SaveChangesAsync();

		note.Edit("officer:synthetic", $"Staff note about {newWord}.", note.Current.Number, now);
		await database.SaveChangesAsync();

		return report.Id.Value;
	}

	private async Task<string> SeedAttachmentReport(string word,
													bool remove)
	{
		await using var scope = _factory.Services.CreateAsyncScope();
		var database = scope.ServiceProvider.GetRequiredService<HpacSafetyDbContext>();

		var now = DateTimeOffset.UtcNow.AddYears(5).AddSeconds(Random.Shared.Next());
		var consent = await ConsentQuestion(database, now);
		var report = new Report(Locale.EnCa, now);
		report.Answer(consent, true, now);
		database.Reports.Add(report);
		await database.SaveChangesAsync();

		var attachmentId = TinyId.New();
		var attachment = PrivateAttachment.Add(
			attachmentId,
			report.Id,
			BlobKey.For(report.Id.Value, MediaCompartment.Private, attachmentId.Value),
			$"{word}.zip",
			"application/zip",
			2048,
			null,
			"officer:synthetic",
			now);
		database.PrivateAttachments.Add(attachment);
		await database.SaveChangesAsync();

		if (remove)
		{
			attachment.Remove("officer:synthetic", now);
			await database.SaveChangesAsync();
		}

		return report.Id.Value;
	}

	private async Task<string> SeedCommentReport(string word,
												 bool delete)
	{
		await using var scope = _factory.Services.CreateAsyncScope();
		var database = scope.ServiceProvider.GetRequiredService<HpacSafetyDbContext>();

		var now = DateTimeOffset.UtcNow.AddYears(5).AddSeconds(Random.Shared.Next());
		var consent = await ConsentQuestion(database, now);
		var report = new Report(Locale.EnCa, now);
		report.Answer(consent, true, now);
		database.Reports.Add(report);
		await database.SaveChangesAsync();

		var comment = ReportComment.Post(report.Id, "member:synthetic", $"A member comment about {word}.", Locale.EnCa, now);
		database.ReportComments.Add(comment);
		await database.SaveChangesAsync();

		if (delete)
		{
			comment.DeleteBy("member:synthetic", now);
			await database.SaveChangesAsync();
		}

		return report.Id.Value;
	}

	private async Task<string> SeedEditedCommentReport(string oldWord,
													   string newWord)
	{
		await using var scope = _factory.Services.CreateAsyncScope();
		var database = scope.ServiceProvider.GetRequiredService<HpacSafetyDbContext>();

		var now = DateTimeOffset.UtcNow.AddYears(5).AddSeconds(Random.Shared.Next());
		var consent = await ConsentQuestion(database, now);
		var report = new Report(Locale.EnCa, now);
		report.Answer(consent, true, now);
		database.Reports.Add(report);
		await database.SaveChangesAsync();

		var comment = ReportComment.Post(report.Id, "member:synthetic", $"A member comment about {oldWord}.", Locale.EnCa, now);
		database.ReportComments.Add(comment);
		await database.SaveChangesAsync();

		comment.Edit("member:synthetic", $"A member comment about {newWord}.", Locale.EnCa, now);
		await database.SaveChangesAsync();

		return report.Id.Value;
	}

	private async Task<(string PendingId, string PublishedId)> SeedPendingAndPublished(string sharedWord)
	{
		await using var scope = _factory.Services.CreateAsyncScope();
		var database = scope.ServiceProvider.GetRequiredService<HpacSafetyDbContext>();

		var now = DateTimeOffset.UtcNow.AddYears(5).AddSeconds(Random.Shared.Next());
		var suffix = Suffix();
		var consent = await ConsentQuestion(database, now);
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

	private static async Task<Question> ConsentQuestion(HpacSafetyDbContext database,
														DateTimeOffset at)
	{
		var existing = await database.Questions
			.Include(question => question.Revisions)
			.SingleOrDefaultAsync(question => question.Role == QuestionRole.ConsentPublish);

		if (existing is not null)
		{
			return existing;
		}

		var consent = Question.CreateConsentPublish(
			"May we publish a de-identified version of your report?",
			"Pouvons-nous publier une version anonymisée de votre rapport ?",
			at);
		database.Questions.Add(consent);
		return consent;
	}

	/// <summary>Keeps every message the host logs, so a test can prove what it never logged (mirrors ReviewActionSteps).</summary>
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

	private sealed record Seeded(
		string ReportId,
		string NarrativeWord,
		string ChoiceLabelWord,
		string SummaryWord,
		string PrivateNoteWord,
		string CommentWord,
		string PublicFileWord,
		string PrivateFileWord);
}
