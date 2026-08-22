var builder = WebApplication.CreateBuilder(args);

builder.Services.AddOpenApi();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();

// Endpoints are added as features land. See the Foundation and Phase 1
// milestones, and src/HpacSafety.Api/README.md.
app.MapGet("/health", () => Results.Ok(new { status = "ok" }));

await app.RunAsync().ConfigureAwait(false);

/// <summary>
/// Exposed so <c>WebApplicationFactory&lt;Program&gt;</c> can boot the API in
/// process for integration tests. Top-level statements generate an internal
/// <c>Program</c>, which the factory cannot reach.
/// </summary>
public partial class Program;
