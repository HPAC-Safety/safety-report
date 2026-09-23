using HpacSafety.Core.Features.Reporting;
using HpacSafety.Infrastructure.AiChatClient;
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
// actually sets rather than silently disabling the Development
// translation stand-in below.
var builder = Host.CreateApplicationBuilder(new HostApplicationBuilderSettings
{
	Args = args,
	EnvironmentName = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT"),
});
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddDbContext<HpacSafetyDbContext>(options =>
	options.UseNpgsql(builder.Configuration.GetConnectionString("HpacSafety")));
builder.Services.Configure<AiChatClientOptions>(builder.Configuration.GetSection(AiChatClientOptions.SectionName));
builder.Services.AddHpacSafetyAiChatClient(builder.Configuration);
builder.Services.AddScoped<ISummarizer, PromptDrivenSummarizer>();

// Same port and adapter selection question authoring uses: a real credential
// gets DeepL, Development with none gets an echo stand-in, everywhere else
// with none reports translation unavailable and the message backs off
// rather than being marked done. See ADR-0062, ADR-0080.
builder.Services.AddHpacSafetyTranslation(
	builder.Configuration,
	builder.Environment.IsDevelopment());

builder.Services.AddScoped<IOutboxMessageProcessor, TranslateAnswersProcessor>();
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
