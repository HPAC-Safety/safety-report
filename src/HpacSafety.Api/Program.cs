using HpacSafety.Api.Admin;
using HpacSafety.Infrastructure.Persistence;
using HpacSafety.Infrastructure.Translation;

using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddOpenApi();
builder.Services.AddProblemDetails();
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddDbContext<HpacSafetyDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("HpacSafety")));

// Machine translation for the question-authoring screen. Registered whether or
// not a credential is present; without one the endpoint says so and the
// authoring screen disables the control. See ADR-0062.
builder.Services.AddHpacSafetyTranslation(builder.Configuration);

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();

// Whichever of the API or the Worker starts first after a deploy applies any
// pending migration; the other is a no-op. See ADR-0055.
await using (var scope = app.Services.CreateAsyncScope())
{
    var context = scope.ServiceProvider.GetRequiredService<HpacSafetyDbContext>();
    await context.EnsureMigratedAsync(app.Logger).ConfigureAwait(false);
}

// Endpoints are added as features land. See the Foundation and Phase 1
// milestones, and src/HpacSafety.Api/README.md.
app.MapGet("/health", () => Results.Ok(new { status = "ok" }));

// The question bank is data an administrator edits, not code that ships
// (ADR-0016). These are the endpoints that edit it.
app.MapAdminQuestions();
app.MapAdminOptionSets();
app.MapAdminTranslation();

await app.RunAsync().ConfigureAwait(false);

/// <summary>
/// Exposed so <c>WebApplicationFactory&lt;Program&gt;</c> can boot the API in
/// process for integration tests. Top-level statements generate an internal
/// <c>Program</c>, which the factory cannot reach.
/// </summary>
public partial class Program;
