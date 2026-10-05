using System.Net;
using System.Net.Http.Json;
using HpacSafety.Core;
using HpacSafety.Core.Features.Moderation;
using HpacSafety.Core.Features.QuestionBank;
using HpacSafety.Infrastructure.Persistence;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;

namespace HpacSafety.Api.Tests;

/// <summary>
///     Two reports that type the same existing value under the same new parent
///     answer both offer it under that answer (ADR-0151). The pair has one row for
///     life, so the second insert meets the first's; the save treats that as done,
///     and both reports are accepted. Every question and answer here is synthetic.
/// </summary>
[Trait("Category", "Integration")]
[Collection(SharedApiPostgres.Name)]
public class ChoiceParentLinkRaceTests(ApiPostgresFixture fixture)
{
	private static readonly Uri Submit = new("/api/v1/reports", UriKind.Relative);

	private readonly WebApplicationFactory<Program> _factory = fixture.Factory;

	// Both submissions are held at SaveChanges until both have loaded the bank,
	// so the second insert always meets the first one's link row.
	[Fact]
	public async Task GivenTwoReportsLinkingOneValueUnderOneAnswer_WhenBothSave_ThenBothAcceptedWithOneLink()
	{
		// Given
		var (consent, make, model, mentor, ozone) = await Bank();
		var gate = new SaveGate(parties: 2);
		await using var gated = _factory.WithWebHostBuilder(builder => builder.ConfigureTestServices(services =>
			services.ConfigureDbContext<HpacSafetyDbContext>(options => options.AddInterceptors(gate))));
		using var reporter = await SignedInClient.As(gated, MemberRole.User);

		// When
		var responses = await Task.WhenAll(Enumerable.Range(0, 2).Select(_ => reporter.PostAsJsonAsync(Submit, new
		{
			language = "en-CA",
			answers = new object[]
			{
				new { questionRevisionId = consent, value = (bool?)false, choices = (string[]?)null },
				new { questionRevisionId = make, value = (string?)null, choices = new[] { ozone.Value } },
				new { questionRevisionId = model, value = (string?)"mentor 7", choices = (string[]?)null },
			},
		})));

		// Then
		foreach (var response in responses)
		{
			response.StatusCode.ShouldBe(HttpStatusCode.Accepted, await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
			response.Dispose();
		}

		await using var scope = _factory.Services.CreateAsyncScope();
		var database = scope.ServiceProvider.GetRequiredService<HpacSafetyDbContext>();
		(await database.ChoiceParentLinks.CountAsync(link => link.ChoiceId == mentor && link.ParentChoiceId == ozone && link.Deleted == null, cancellationToken: TestContext.Current.CancellationToken))
			.ShouldBe(1);
	}

	/// <summary>A make, and a model whose "Mentor 7" is offered under "Niviuk" only.</summary>
	private async Task<(string Consent, string Make, string Model, TinyId Mentor, TinyId Ozone)> Bank()
	{
		await using var scope = _factory.Services.CreateAsyncScope();
		var database = scope.ServiceProvider.GetRequiredService<HpacSafetyDbContext>();
		var now = DateTimeOffset.UtcNow;

		var consent = await database.Questions.Include(question => question.Revisions)
						  .SingleOrDefaultAsync(question => question.Role == QuestionRole.ConsentPublish)
					  ?? database.Questions.Add(Question.CreateConsentPublish(
						  "May we publish a de-identified version of your report?",
						  "Pouvons-nous publier une version anonymisée de votre rapport ?",
						  now)).Entity;

		var suffix = Guid.NewGuid().ToString("N")[..8];
		var make = Question.Create(
			$"race_make_{suffix}", QuestionType.SingleSelect, $"Make {suffix}", $"Marque {suffix}", now, isActive: true, isPrivate: false,
			displayOrder: 900,
			options: [new QuestionOptionInput("niviuk", "Niviuk", "Niviuk"), new QuestionOptionInput("ozone", "Ozone", "Ozone")]);
		var model = Question.Create(
			$"race_model_{suffix}", QuestionType.Autocomplete, $"Model {suffix}", $"Modèle {suffix}", now, isActive: true, isPrivate: false,
			displayOrder: 901, choicesDependOnQuestionId: make.Id,
			options: [new QuestionOptionInput("mentor_7", "Mentor 7", "Mentor 7", ParentChoiceIds: [make.Choice("niviuk")!.Id])]);

		database.Questions.Add(make);
		database.Questions.Add(model);
		await database.SaveChangesAsync();

		return (consent.CurrentRevision.Id.Value, make.CurrentRevision.Id.Value, model.CurrentRevision.Id.Value,
			model.Choice("mentor_7")!.Id, make.Choice("ozone")!.Id);
	}

	/// <summary>Holds every save that adds a parent link until <c>parties</c> of them are waiting.</summary>
	private sealed class SaveGate(int parties) : SaveChangesInterceptor
	{
		private readonly TaskCompletionSource _everyoneWaiting = new(TaskCreationOptions.RunContinuationsAsynchronously);
		private int _waiting;

		public override async ValueTask<InterceptionResult<int>> SavingChangesAsync(DbContextEventData eventData,
																					InterceptionResult<int> result,
																					CancellationToken cancellationToken = default)
		{
			var addsLink = eventData.Context?.ChangeTracker.Entries<ChoiceParentLink>()
				.Any(entry => entry.State == EntityState.Added) ?? false;

			if (addsLink && !_everyoneWaiting.Task.IsCompleted)
			{
				if (Interlocked.Increment(ref _waiting) == parties)
				{
					_everyoneWaiting.SetResult();
				}

				await _everyoneWaiting.Task.WaitAsync(TimeSpan.FromSeconds(30), cancellationToken).ConfigureAwait(false);
			}

			return result;
		}
	}
}
