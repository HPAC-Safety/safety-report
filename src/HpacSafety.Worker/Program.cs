using HpacSafety.Core.Features.Reporting;
using HpacSafety.Infrastructure.AiChatClient;
using HpacSafety.Infrastructure.Persistence;
using HpacSafety.Worker;
using HpacSafety.Worker.Summarization;
using Microsoft.EntityFrameworkCore;

var builder = Host.CreateApplicationBuilder(args);
builder.Services.AddDbContext<HpacSafetyDbContext>(options =>
	options.UseNpgsql(builder.Configuration.GetConnectionString("HpacSafety")));
builder.Services.Configure<AiChatClientOptions>(builder.Configuration.GetSection(AiChatClientOptions.SectionName));
builder.Services.AddHpacSafetyAiChatClient();
builder.Services.AddScoped<ISummarizer, PromptDrivenSummarizer>();
builder.Services.AddHostedService<Worker>();

var host = builder.Build();

// Whichever of the Worker or the API starts first after a deploy applies any
// pending migration; the other is a no-op. See ADR-0055.
await using (var scope = host.Services.CreateAsyncScope())
{
	var context = scope.ServiceProvider.GetRequiredService<HpacSafetyDbContext>();
	var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();
	await context.EnsureMigratedAsync(logger).ConfigureAwait(false);
}

await host.RunAsync().ConfigureAwait(false);
