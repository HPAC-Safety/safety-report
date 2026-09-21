using HpacSafety.Api.Admin;
using HpacSafety.Api.Authentication;
using HpacSafety.Infrastructure.Persistence;
using HpacSafety.Infrastructure.Translation;

using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddOpenApi();
builder.Services.AddProblemDetails();
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddDbContext<HpacSafetyDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("HpacSafety")));

// Machine translation for the question-authoring screen. In Development with
// no credential this resolves a stand-in that echoes its input, so the control
// works locally and exercises the same endpoint and port as production. A
// non-development deployment with no credential reports translation
// unavailable instead. See ADR-0062.
builder.Services.AddHpacSafetyTranslation(
    builder.Configuration,
    useStandInWhenUnconfigured: builder.Environment.IsDevelopment());

// Identity is a signed JWT this API validates; it never sees a password. In
// Development the API also issues the tokens it validates, so the same
// middleware and the same policies run either way and only the issuer and key
// differ. See ADR-0064 and ADR-0066.
builder.Services.AddHpacSafetyAuthentication(
    builder.Configuration,
    useDevelopmentIssuer: builder.Environment.IsDevelopment());

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();
app.UseAuthentication();
app.UseAuthorization();

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

// Sign-in, and who the caller is. The development token endpoint inside is
// mapped only in Development.
app.MapAuth(app.Environment.IsDevelopment());

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
