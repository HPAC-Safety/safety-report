using HpacSafety.Api.Admin;
using HpacSafety.Api.Authentication;
using HpacSafety.Api.PublicQuestions;
using HpacSafety.Api.PublicReports;
using HpacSafety.Api.RateLimiting;
using HpacSafety.Api.Reports;
using HpacSafety.Api.Security;
using HpacSafety.Infrastructure.Media;
using HpacSafety.Infrastructure.Persistence;
using HpacSafety.Infrastructure.Translation;
using HpacSafety.Infrastructure.Worker;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddOpenApi();
builder.Services.AddProblemDetails();
builder.Services.AddSingleton(TimeProvider.System);
// Fails the host at startup, outside Development, if no origin secret is
// configured — a caller must never reach an unverified Function URL because a
// Secrets Manager value was missing. See ADR-0159.
builder.Services.AddHpacSafetyOriginVerification(
	builder.Configuration,
	builder.Environment.IsDevelopment());

// Behind the same Lambda invocation the report-submission, comment, and
// review endpoints call after a commit that queues outbox work — a nudge
// that only shortens the wait until the next EventBridge sweep, never the
// only path to the work getting done. See ADR-0123, ADR-0159.
builder.Services.AddHpacSafetyWorkerNudge(builder.Configuration);

// Built once, at cold start, from the RDS-managed master-user secret in every
// deployed environment — Terraform can set the host/port/database name but
// never the connection string itself without putting the master password in
// state (ADR-0010). Falls back to the plain ConnectionStrings:HpacSafety
// value in Development and every test host. See #443, #465.
var connectionString = await DatabaseConnectionStringResolver.ResolveAsync(builder.Configuration).ConfigureAwait(false);
builder.Services.AddDbContext<HpacSafetyDbContext>((provider, options) =>
{
	options.UseNpgsql(connectionString);
	options.AddInterceptors(provider.GetRequiredService<OutboxNudgeInterceptor>());
});

// Machine translation for the question-authoring screen. With no credential
// the API reports translation unavailable, in Development as everywhere else.
// See ADR-0062, ADR-0109.
builder.Services.AddHpacSafetyTranslation(builder.Configuration);

// Identity is a signed JWT this API validates; it never sees a password. In
// Development the API also issues the tokens it validates, so the same
// middleware and the same policies run either way and only the issuer and key
// differ. See ADR-0064 and ADR-0066.
builder.Services.AddHpacSafetyAuthentication(
	builder.Configuration,
	builder.Environment.IsDevelopment());

// Private object storage and the media-ingest pipeline behind attachment
// uploads and report submission. S3 in AWS, an S3-compatible container in
// development; only configuration differs. See ADR-0096.
builder.Services.AddHpacSafetyMedia(builder.Configuration);

// The Lambda Web Adapter (infra/lambda.tf) turns each Function URL event into
// a loopback HTTP request against this process's own Kestrel listener — the
// only "hop" this process ever sees directly, and always local. The
// X-Forwarded-For/-Proto the adapter carries over from the original event are
// trusted without a static KnownProxies allowlist, same as the ALB reasoning
// this replaces: reaching this process at all already means the request came
// through the adapter, and CloudFront's origin-secret header
// (UseCloudFrontOriginVerification, ADR-0159) is what keeps a caller from
// reaching the Function URL directly in the first place. See ADR-0081, ADR-0042, issue #15.
builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
	options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
	options.KnownIPNetworks.Clear();
	options.KnownProxies.Clear();
});

// Two RateLimiter policies: public submission by trusted client IP, sign-in
// by attempted identity. See ADR-0081 and issue #15.
builder.Services.AddHpacSafetyRateLimiting(builder.Configuration);

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
	app.MapOpenApi();
}

// First, and unconditional: an unverified caller's headers — including the
// forwarded ones trusted next — are never trusted for anything. See
// ADR-0159.
app.UseCloudFrontOriginVerification();

app.UseForwardedHeaders();
app.UseHttpsRedirection();
app.UseAuthentication();
app.UseAuthorization();
app.UseSignInIdentityCapture("/api/auth/token");
app.UseRateLimiter();

// Whichever of the API or the Worker starts first after a deploy applies any
// pending migration; the other is a no-op. See ADR-0055.
await using (var scope = app.Services.CreateAsyncScope())
{
	var context = scope.ServiceProvider.GetRequiredService<HpacSafetyDbContext>();
	await context.EnsureMigrated(app.Logger).ConfigureAwait(false);
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

// The published reports and each one's own page (#28). Anonymous, and read
// only through the public_reports view.
app.MapPublicReports();

// Members' comments on a published report (ADR-0114): read by anyone, written
// by members, hidden by reviewers.
app.MapComments();

// The reporter-facing writes. Both require a member token. An attachment
// uploads to quarantine when it is attached; the report is written once, by the
// final submission that claims those uploads. See ADR-0096.
app.MapAttachmentUploads();
app.MapReportSubmission();

// The question bank is data an administrator edits, not code that ships
// (ADR-0016). These are the endpoints that edit it.
app.MapAdminQuestions();
app.MapAdminTranslation();
app.MapAdminAnswerTranslation();
app.MapAdminTypeformImport();

// Soft deletion (issue #82). The review queue itself is issue #25.
app.MapAdminReports();
app.MapAdminPendingCounts();
app.MapAdminTypeAheadValues();

// Staff-only notes on a report; nothing else reads them (ADR-0133).
app.MapAdminPrivateNotes();
app.MapAdminPrivateAttachments();

// A safety officer or administrator's only two ways to see an uploaded file.
app.MapAdminAttachments();

await app.RunAsync().ConfigureAwait(false);

/// <summary>
///     Exposed so <c>WebApplicationFactory&lt;Program&gt;</c> can boot the API in
///     process for integration tests. Top-level statements generate an internal
///     <c>Program</c>, which the factory cannot reach.
/// </summary>
public partial class Program;
