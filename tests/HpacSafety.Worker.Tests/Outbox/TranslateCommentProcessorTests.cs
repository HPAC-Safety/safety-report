using HpacSafety.Core;
using HpacSafety.Core.Features.Comments;
using HpacSafety.Core.Features.Outbox;
using HpacSafety.Core.Features.Reporting;
using HpacSafety.Worker.Outbox;
using Microsoft.EntityFrameworkCore;
using Shouldly;

namespace HpacSafety.Worker.Tests.Outbox;

/// <summary>
///     ADR-0114 against a real database: one revision of a comment is translated
///     into the other language and recorded as <c>auto</c>. A revision already
///     translated, or no longer there, is never sent to the provider.
/// </summary>
[Trait("Category", "Integration")]
[Collection(SharedWorkerPostgres.Name)]
public sealed class TranslateCommentProcessorTests(WorkerPostgresFixture postgres)
{
	private static readonly DateTimeOffset At = new(2026, 9, 24, 9, 0, 0, TimeSpan.Zero);

	[Fact]
	public void GivenTranslateCommentOutboxType_WhenAsked_ThenHandlesTranslateComment()
	{
		// Given
		var processor = new TranslateCommentProcessor(null!, new StubTranslator());

		// Then
		processor.HandlesType.ShouldBe(OutboxMessageType.TranslateComment);
	}

	[Fact]
	public async Task GivenFrenchRevision_WhenProcessed_ThenEnglishIsSuppliedAsAuto()
	{
		// Given
		var (connectionString, comment) = await Commented("Synthétique : bon rappel.", Locale.FrCa);
		await using var context = WorkerPostgresFixture.ContextFor(connectionString);
		var translator = new StubTranslator();

		// When
		await new TranslateCommentProcessor(context, translator).Process(Message(comment.Current.Id), CancellationToken.None);
		await context.SaveChangesAsync();

		// Then
		var call = translator.Calls.ShouldHaveSingleItem();
		call.Texts.ShouldBe(["Synthétique : bon rappel."]);
		call.Source.ShouldBe(Locale.FrCa);
		call.Target.ShouldBe(Locale.EnCa);

		await using var reader = WorkerPostgresFixture.ContextFor(connectionString);
		var revision = await reader.ReportCommentRevisions.SingleAsync(candidate => candidate.CommentId == comment.Id);
		revision.Text.ShouldBe("Synthétique : bon rappel.");
		revision.TranslatedText.ShouldNotBeNull();
		revision.TranslationSource.ShouldBe(TranslationSource.Auto);
	}

	[Fact]
	public async Task GivenRevisionAlreadyTranslated_WhenProcessedAgain_ThenProviderIsNotCalled()
	{
		// Given
		var (connectionString, comment) = await Commented("Synthetic: keep extra height.", Locale.EnCa);
		await using (var first = WorkerPostgresFixture.ContextFor(connectionString))
		{
			await new TranslateCommentProcessor(first, new StubTranslator()).Process(Message(comment.Current.Id), CancellationToken.None);
			await first.SaveChangesAsync();
		}

		await using var context = WorkerPostgresFixture.ContextFor(connectionString);
		var translator = new StubTranslator();

		// When
		await new TranslateCommentProcessor(context, translator).Process(Message(comment.Current.Id), CancellationToken.None);

		// Then
		translator.Calls.ShouldBeEmpty();
	}

	[Fact]
	public async Task GivenDeletedComment_WhenProcessed_ThenProviderIsNotCalled()
	{
		// Given
		var (connectionString, comment) = await Commented("Synthetic: deleted before translation.", Locale.EnCa);
		await using (var author = WorkerPostgresFixture.ContextFor(connectionString))
		{
			var stored = await author.ReportComments.Include(candidate => candidate.Revisions).SingleAsync(candidate => candidate.Id == comment.Id);
			stored.DeleteBy("member:author", At);
			await author.SaveChangesAsync();
		}

		await using var context = WorkerPostgresFixture.ContextFor(connectionString);
		var translator = new StubTranslator();

		// When
		await new TranslateCommentProcessor(context, translator).Process(Message(comment.Current.Id), CancellationToken.None);

		// Then
		translator.Calls.ShouldBeEmpty();
	}

	private static OutboxMessage Message(TinyId revisionId)
	{
		return new OutboxMessage(revisionId, OutboxMessageType.TranslateComment, revisionId.Value, At);
	}

	private async Task<(string ConnectionString, ReportComment Comment)> Commented(string text,
																					 Locale locale)
	{
		var connectionString = await postgres.CreateMigratedDatabase();
		await using var context = WorkerPostgresFixture.ContextFor(connectionString);
		var report = new Report(Locale.EnCa, At);
		context.Reports.Add(report);
		var comment = ReportComment.Post(report.Id, "member:author", text, locale, At);
		context.ReportComments.Add(comment);
		await context.SaveChangesAsync();
		return (connectionString, comment);
	}
}
