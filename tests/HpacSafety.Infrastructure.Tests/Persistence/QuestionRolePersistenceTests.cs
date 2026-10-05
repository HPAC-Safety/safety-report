using HpacSafety.Core;
using HpacSafety.Core.Features.QuestionBank;
using Microsoft.EntityFrameworkCore;
using Shouldly;

namespace HpacSafety.Infrastructure.Tests.Persistence;

/// <summary>
///     <c>ix_questions_role</c>: a role lives on at most one live question, the
///     database enforces it (ADR-0154), and forking an answered question that
///     carries one — <see cref="Question.ApplyEdit" /> retires the old row and
///     inserts the replacement, both carrying the same role, in one
///     <c>SaveChanges</c> — does not collide with it. Uses the real seeded
///     reporter-first-name question, since every role is already spoken for by a
///     seeded question on a freshly migrated database — the same constraint an
///     administrator reworking that question in production would run into.
/// </summary>
[Trait("Category", "Integration")]
[Collection(SharedPostgres.Name)]
public sealed class QuestionRolePersistenceTests(PostgresFixture postgres)
{
	private static readonly DateTimeOffset At = new(2026, 9, 22, 12, 0, 0, TimeSpan.Zero);

	[Fact]
	public async Task GivenTheSeededReporterFirstNameQuestionAnswered_WhenItForks_ThenTheReplacementAloneIsLiveWithTheRole()
	{
		// Given
		var connectionString = await postgres.CreateMigratedDatabase();
		string originalId;

		await using (var context = PostgresFixture.ContextFor(connectionString))
		{
			var seeded = await context.Questions.Include(q => q.Revisions)
				.SingleAsync(q => q.Role == QuestionRole.ReporterFirstName, cancellationToken: TestContext.Current.CancellationToken);
			originalId = seeded.Id.Value;
		}

		// When — an Administrator rewords it after it has been answered, which
		// retires this row and inserts a replacement carrying the same role, in
		// the one SaveChanges call that follows
		Question forked;

		await using (var context = PostgresFixture.ContextFor(connectionString))
		{
			var loaded = await context.Questions.Include(q => q.Revisions).SingleAsync(q => q.Role == QuestionRole.ReporterFirstName, cancellationToken: TestContext.Current.CancellationToken);
			forked = loaded.ApplyEdit(
				hasBeenAnswered: true, QuestionType.ShortText, "Your first name", "Votre prénom",
				isPrivate: true, isActive: true, displayOrder: 0, At);
			context.Questions.Add(forked);

			// Then — no unique-violation on ix_questions_role
			await Should.NotThrowAsync(() => context.SaveChangesAsync());
		}

		// Then
		await using (var context = PostgresFixture.ContextFor(connectionString))
		{
			var live = await context.Questions.Where(q => q.Role == QuestionRole.ReporterFirstName).ToListAsync(cancellationToken: TestContext.Current.CancellationToken);
			live.Count.ShouldBe(1);
			live.Single().Id.ShouldBe(forked.Id);
			live.Single().Id.Value.ShouldNotBe(originalId);

			var originalTinyId = TinyId.Parse(originalId);
			var retired = await context.Questions.IgnoreQueryFilters().SingleAsync(q => q.Id == originalTinyId, cancellationToken: TestContext.Current.CancellationToken);
			retired.Deleted.ShouldNotBeNull();
			retired.Role.ShouldBe(QuestionRole.ReporterFirstName);
		}
	}
}
