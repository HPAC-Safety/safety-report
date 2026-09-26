using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using HpacSafety.Core;
using HpacSafety.Core.Features.Moderation;
using HpacSafety.Core.Features.Outbox;
using HpacSafety.Core.Features.QuestionBank;
using HpacSafety.Infrastructure.Persistence;
using HpacSafety.Worker.Outbox;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Reqnroll;
using Shouldly;

namespace HpacSafety.Acceptance.Tests;

/// <summary>
///     The Worker supplies a reporter-added type-ahead value's other language,
///     and review is left alone (<c>REQ-QB-134</c>, ADR-0129).
/// </summary>
/// <remarks>
///     The submission runs through the booted API. The Worker is a separate
///     deployable, so its step runs here as the real
///     <see cref="TranslateChoiceProcessor" /> against the booted database, with a
///     translator that records what it was sent instead of calling a provider.
///     Every value here is synthetic.
/// </remarks>
[Binding]
public sealed class ReporterValueTranslationSteps
{
	private static readonly Uri Submit = new("/api/v1/reports", UriKind.Relative);
	private static readonly DateTimeOffset Noon = new(2026, 9, 25, 12, 0, 0, TimeSpan.Zero);

	private readonly RecordingTranslator _translator = new();
	private TinyId _valueId;
	private int _sentBeforeTheWorker = -1;

	[Given(@"a reporter answering in French added the type-ahead value ""(.*)""")]
	public async Task GivenAFrenchReporterAddedAValue(string typed)
	{
		using var admin = await BootedApi.SignedInAs(MemberRole.Administrator);
		var revisionId = await ReporterChoiceSubmissionSteps.CreateTypeAhead(admin);

		using var reporter = await BootedApi.SignedInAs(MemberRole.User);
		using var response = await reporter.PostAsJsonAsync(Submit, new
		{
			language = "fr-CA",
			answers = new object[]
			{
				new { questionRevisionId = await ReportSubmissionEndpointSteps.ConsentRevisionId(), value = (bool?)true },
				new { questionRevisionId = revisionId, value = (string?)typed },
			},
		});
		response.StatusCode.ShouldBe(HttpStatusCode.Accepted, await response.Content.ReadAsStringAsync());
		var reportId = TinyId.Parse((await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetString()!);

		await using var scope = (await BootedApi.Factory()).Services.CreateAsyncScope();
		var database = scope.ServiceProvider.GetRequiredService<HpacSafetyDbContext>();
		var revision = TinyId.Parse(revisionId);
		_valueId = (await database.ReportAnswers.AsNoTracking()
			.SingleAsync(answer => answer.ReportId == reportId && answer.QuestionRevisionId == revision)).ChoiceId!.Value;

		// Submitted, and nothing sent anywhere yet: the translator is the Worker's.
		_sentBeforeTheWorker = _translator.Sent.Count;
	}

	[When(@"the Worker processes its translation work")]
	public async Task WhenTheWorkerTranslates()
	{
		await using var scope = (await BootedApi.Factory()).Services.CreateAsyncScope();
		var database = scope.ServiceProvider.GetRequiredService<HpacSafetyDbContext>();

		var message = await database.OutboxMessages.AsNoTracking()
			.SingleAsync(candidate => candidate.Type == OutboxMessageType.TranslateChoice && candidate.Payload == _valueId.Value);

		await new TranslateChoiceProcessor(database, _translator).Process(message, CancellationToken.None);
		await database.SaveChangesAsync();
	}

	[Then(@"the value gains an English wording from the translation provider, marked as machine-translated")]
	public async Task ThenTheValueGainsEnglish()
	{
		var value = await Value();
		value.LabelEn.ShouldBe("[en-CA] Élévation Sainte-Anne");
		value.LabelEnSource.ShouldBe(LabelSource.Auto);
		value.LabelFr.ShouldBe("Élévation Sainte-Anne");
	}

	[Then(@"the value is still flagged for review")]
	public async Task ThenStillFlagged()
	{
		// Translation is mechanical; review is a person's (ADR-0129).
		var value = await Value();
		value.NeedsReview.ShouldBeTrue();
		value.ReviewedAt.ShouldBeNull();
	}

	[Then(@"no translation provider was called while the report was submitted")]
	public void ThenNoProviderOnSubmission()
	{
		_sentBeforeTheWorker.ShouldBe(0);
		_translator.Sent.ShouldBe(["Élévation Sainte-Anne"]);
	}

	private async Task<QuestionChoice> Value()
	{
		await using var scope = (await BootedApi.Factory()).Services.CreateAsyncScope();
		var database = scope.ServiceProvider.GetRequiredService<HpacSafetyDbContext>();
		return await database.QuestionChoices.AsNoTracking().SingleAsync(choice => choice.Id == _valueId);
	}

	/// <summary>A translator that records what it was sent and answers without a provider.</summary>
	private sealed class RecordingTranslator : ITranslator
	{
		public List<string> Sent { get; } = [];

		public bool IsConfigured => true;

		public Task<IReadOnlyList<string>> Translate(
			IReadOnlyList<string> texts,
			Locale source,
			Locale target,
			CancellationToken cancellationToken)
		{
			Sent.AddRange(texts);
			return Task.FromResult<IReadOnlyList<string>>([.. texts.Select(text => $"[{target.Code}] {text}")]);
		}
	}
}
