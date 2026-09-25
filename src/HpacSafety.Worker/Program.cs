using HpacSafety.Core.Features.Reporting;
using HpacSafety.Infrastructure.AiChatClient;
using HpacSafety.Infrastructure.Media;
using HpacSafety.Infrastructure.Persistence;
using HpacSafety.Infrastructure.Translation;
using HpacSafety.Worker;
using HpacSafety.Worker.Outbox;
using HpacSafety.Worker.Summarization;
using Microsoft.EntityFrameworkCore;

// The Generic Host reads DOTNET_ENVIRONMENT by default; this project's own
// convention (docker-compose.yml, infra/ecs.tf) is ASPNETCORE_ENVIRONMENT,
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
builder.Services.AddDbContext<HpacSafetyDbContext>(options =>
	options.UseNpgsql(builder.Configuration.GetConnectionString("HpacSafety")));
builder.Services.AddHpacSafetyAiChatClient(builder.Configuration);
builder.Services.AddScoped<ISummarizer, PromptDrivenSummarizer>();

// Same port and adapter question authoring uses. With no credential, in any
// environment, translation is unavailable and the message backs off rather
// than being marked done. See ADR-0080, ADR-0109.
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
builder.Services.AddHostedService<Worker>();

var host = builder.Build();

// Whichever of the Worker or the API starts first after a deploy applies any
// pending migration; the other is a no-op. See ADR-0055.
await using (var scope = host.Services.CreateAsyncScope())
{
	var context = scope.ServiceProvider.GetRequiredService<HpacSafetyDbContext>();
	var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();
	await context.EnsureMigrated(logger).ConfigureAwait(false);
}

await host.RunAsync().ConfigureAwait(false);
