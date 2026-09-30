using System.Text.Json;
using Amazon.Lambda.Core;
using Amazon.Lambda.RuntimeSupport;
using HpacSafety.Core.Features.Reporting;
using HpacSafety.Infrastructure.AiChatClient;
using HpacSafety.Infrastructure.Media;
using HpacSafety.Infrastructure.Observability;
using HpacSafety.Infrastructure.Persistence;
using HpacSafety.Infrastructure.Translation;
using HpacSafety.Worker;
using HpacSafety.Worker.Outbox;
using HpacSafety.Worker.Summarization;
using Microsoft.EntityFrameworkCore;

// AWS sets AWS_LAMBDA_RUNTIME_API only inside the Lambda execution
// environment. Deployed, the Worker is a Lambda function that drains once per
// invocation and returns (ADR-0123); everywhere else — docker-compose.yml,
// developer machines, CI — it is the polling BackgroundService loop
// (Worker.cs) unchanged from before.
var isLambda = !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("AWS_LAMBDA_RUNTIME_API"));

// The Generic Host reads DOTNET_ENVIRONMENT by default; this project's own
// convention (docker-compose.yml, infra/lambda.tf) is ASPNETCORE_ENVIRONMENT,
// the variable WebApplication-based HpacSafety.Api reads automatically.
// Preferring it here, and falling back to the Generic Host's own resolution
// when it is unset, keeps both hosts driven by the one variable a deploy
// actually sets rather than silently skipping appsettings.Development.json.
var builder = Host.CreateApplicationBuilder(new HostApplicationBuilderSettings
{
	Args = args,
	EnvironmentName = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT"),
});
builder.Services.AddSingleton(TimeProvider.System);

// One CloudWatch Embedded Metric Format log line per Publish call — no AWS
// SDK, no CloudWatch API call (issue #467). See infra/observability.tf.
builder.Services.AddHpacSafetyObservability(builder.Configuration);

// Built once, at cold start, from the RDS-managed master-user secret in every
// deployed environment (same shape the API resolves, ADR-0055 applies to
// both). Falls back to the plain ConnectionStrings:HpacSafety value in
// Development and every test host. See #443, #465.
var connectionString = await DatabaseConnectionStringResolver.ResolveAsync(builder.Configuration).ConfigureAwait(false);
builder.Services.AddDbContext<HpacSafetyDbContext>(options => options.UseNpgsql(connectionString));

// Resolved once, here, from Secrets Manager when Terraform supplies an ARN —
// every deployed environment; the Lambda environment carries only the ARN,
// never the key's value. Left unset in Development and every test host,
// where the plain AiChatClient:ApiKey setting still applies. See #597.
var aiChatClientSection = builder.Configuration.GetSection(AiChatClientOptions.SectionName);
var aiChatClientApiKey = await SecretArnResolver.ResolveAsync(
	aiChatClientSection[nameof(AiChatClientOptions.ApiKey)],
	aiChatClientSection[nameof(AiChatClientOptions.ApiKeySecretArn)]).ConfigureAwait(false);
builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
{
	[$"{AiChatClientOptions.SectionName}:{nameof(AiChatClientOptions.ApiKey)}"] = aiChatClientApiKey,
});
builder.Services.AddHpacSafetyAiSummarization(builder.Configuration);
builder.Services.AddScoped<ISummarizer, OpenAiSummarizer>();

// Same port and adapter question authoring uses, through the same Gemini key
// resolved above and its own Translation:Model. With no key, in any
// environment, translation is unavailable and the message backs off rather
// than being marked done. See ADR-0080, ADR-0109, ADR-0179.
builder.Services.AddHpacSafetyTranslation(builder.Configuration);

// Attachment derivatives are produced here, one outbox message per file, never
// on the submission path (ADR-0098). The same storage adapter and ingest
// pipeline the API uses: S3 in AWS, an S3-compatible container in development.
builder.Services.AddHpacSafetyMedia(builder.Configuration);
builder.Services.AddScoped<IOutboxMessageProcessor, ProcessAttachmentProcessor>();

builder.Services.AddScoped<IOutboxMessageProcessor, TranslateAnswersProcessor>();
builder.Services.AddScoped<IOutboxMessageProcessor, TranslateCommentProcessor>();
builder.Services.AddScoped<IOutboxMessageProcessor, TranslateChoiceProcessor>();
builder.Services.AddScoped<IOutboxMessageProcessor, SummarizeReportProcessor>();

// The polling loop is a Lambda-less-host concern only — a deployed Lambda
// invocation drains through OutboxDrainPass directly, below, and never starts
// this hosted service.
if (!isLambda)
{
	builder.Services.AddHostedService<Worker>();
}

var host = builder.Build();

// Whichever of the Worker or the API starts first after a deploy applies any
// pending migration; the other is a no-op. See ADR-0055. A Lambda cold start
// pays this cost once per warm container, not once per invocation.
await using (var scope = host.Services.CreateAsyncScope())
{
	var context = scope.ServiceProvider.GetRequiredService<HpacSafetyDbContext>();
	var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();
	await context.EnsureMigrated(logger).ConfigureAwait(false);
}

if (isLambda)
{
	await host.StartAsync().ConfigureAwait(false);

	var scopeFactory = host.Services.GetRequiredService<IServiceScopeFactory>();
	var clock = host.Services.GetRequiredService<TimeProvider>();

	// One invocation, one drain, then return — never idle-poll inside a Lambda
	// invocation. The API's async nudge and the EventBridge one-minute sweep
	// both invoke this same handler; polling in Worker.cs remains the source
	// of truth, so a lost or throttled invocation only delays work until the
	// next sweep (ADR-0123). The margin leaves room for an in-flight message
	// (at most a two-minute remux, ADR-0123) to finish and commit before the
	// 15-minute Lambda ceiling.
	var safetyMargin = TimeSpan.FromSeconds(30);

	var requestJsonOptions = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };

	// A raw Stream in and out — no Amazon.Lambda.Serialization.* package
	// (issue #467: "no new SDK"). Every invocation (the API's nudge, the
	// EventBridge sweep, and a manual requeue call alike) reaches this same
	// handler; only a payload matching {"requeue":"poison"} is read as
	// anything other than "drain what is due" (PoisonRequeue, ADR-0123).
	using var handlerWrapper = HandlerWrapper.GetHandlerWrapper(async (Stream inputStream, ILambdaContext context) =>
	{
		PoisonRequeue.Request? requeueRequest = null;
		if (inputStream.Length > 0)
		{
			try
			{
				requeueRequest = await JsonSerializer
					.DeserializeAsync<PoisonRequeue.Request>(inputStream, requestJsonOptions)
					.ConfigureAwait(false);
			}
			catch (JsonException)
			{
				requeueRequest = null;
			}
		}

		if (PoisonRequeue.IsPoisonRequeue(requeueRequest))
		{
			await using var requeueScope = scopeFactory.CreateAsyncScope();
			var database = requeueScope.ServiceProvider.GetRequiredService<HpacSafetyDbContext>();
			var logger = requeueScope.ServiceProvider.GetRequiredService<ILogger<Program>>();

			var result = await PoisonRequeue
				.Requeue(database, clock.GetUtcNow(), requeueRequest!.From, requeueRequest.To, logger, CancellationToken.None)
				.ConfigureAwait(false);

			return new MemoryStream(JsonSerializer.SerializeToUtf8Bytes(result));
		}

		await OutboxDrainPass
			.DrainUntilIdleOrOutOfTime(scopeFactory, clock, () => context.RemainingTime, safetyMargin, CancellationToken.None)
			.ConfigureAwait(false);

		return new MemoryStream();
	});

	using var bootstrap = new LambdaBootstrap(handlerWrapper);
	await bootstrap.RunAsync().ConfigureAwait(false);
}
else
{
	await host.RunAsync().ConfigureAwait(false);
}
