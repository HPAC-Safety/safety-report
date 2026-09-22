using HpacSafety.Api.Admin;
using HpacSafety.Api.Authentication;
using HpacSafety.Api.PublicQuestions;
using HpacSafety.Api.Reports;
using HpacSafety.Infrastructure.Media;
using HpacSafety.Infrastructure.Persistence;
using HpacSafety.Infrastructure.Translation;
using Microsoft.AspNetCore.Http.Features;
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
	builder.Environment.IsDevelopment());

// Identity is a signed JWT this API validates; it never sees a password. In
// Development the API also issues the tokens it validates, so the same
// middleware and the same policies run either way and only the issuer and key
// differ. See ADR-0064 and ADR-0066.
builder.Services.AddHpacSafetyAuthentication(
	builder.Configuration,
	builder.Environment.IsDevelopment());

// Private object storage and the media-ingest pipeline behind report
// submission. In Development this writes to local disk instead of a real
// bucket, so the same code path runs everywhere. See issue #14.
builder.Services.AddHpacSafetyMedia(
	builder.Configuration,
	builder.Environment.IsDevelopment());

// The multipart body carries the JSON report part plus every attachment. The
// per-file/count bound the submission endpoint enforces is the real limit;
// this is generous headroom so a legitimate submission is never rejected by
// the framework before that code runs.
builder.Services.Configure<FormOptions>(options =>
{
	var media = builder.Configuration.GetSection("HpacSafety:Media:Policy").Get<MediaPolicyOptions>()
				?? new MediaPolicyOptions();
	options.MultipartBodyLengthLimit = (media.MaxByteSize * media.MaxAttachmentCount) + (1024 * 1024);
	options.ValueCountLimit = 8;
});

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

// Today's live question set, as the reporter-facing form renders it. Public,
// unlike everything below it.
app.MapPublicQuestions();

// The only reporter-facing write. Requires a member token; creates no state
// before the one final multipart request succeeds. See issue #14.
app.MapReportSubmission();

// The question bank is data an administrator edits, not code that ships
// (ADR-0016). These are the endpoints that edit it.
app.MapAdminQuestions();
app.MapAdminOptionSets();
app.MapAdminTranslation();
app.MapAdminAnswerTranslation();
app.MapAdminTypeformImport();

await app.RunAsync().ConfigureAwait(false);

/// <summary>
///     Exposed so <c>WebApplicationFactory&lt;Program&gt;</c> can boot the API in
///     process for integration tests. Top-level statements generate an internal
///     <c>Program</c>, which the factory cannot reach.
/// </summary>
public partial class Program;
