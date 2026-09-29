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
using Microsoft.Extensions.Options;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddOpenApi();
builder.Services.AddProblemDetails();
builder.Services.AddSingleton(TimeProvider.System);

// Resolved once, here, from Secrets Manager when Terraform supplies an ARN —
// every deployed environment; the Lambda environment carries only the ARN,
// never the secret's value. Left unset in Development and every test host.
// Mirrors DatabaseConnectionStringResolver's ARN pattern (#586). See #597,
// ADR-0159, ADR-0163.
var originVerificationSection = builder.Configuration.GetSection(OriginVerificationOptions.SectionName);
var originVerificationSecret = await SecretArnResolver.ResolveAsync(
	originVerificationSection[nameof(OriginVerificationOptions.Secret)],
	originVerificationSection[nameof(OriginVerificationOptions.SecretArn)]).ConfigureAwait(false);
builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
{
	[$"{OriginVerificationOptions.SectionName}:{nameof(OriginVerificationOptions.Secret)}"] = originVerificationSecret,
});

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

// Resolved once, here, from Secrets Manager when Terraform supplies an ARN —
// every deployed environment; the Lambda environment carries only the ARN,
// never the key's value. Left unset in Development and every test host,
// where the plain Translation:ApiKey/DEEPL_API_KEY setting still applies.
// See #597.
var translationSection = builder.Configuration.GetSection(DeepLOptions.SectionName);
var deepLApiKey = await SecretArnResolver.ResolveAsync(
	translationSection[nameof(DeepLOptions.ApiKey)] ?? builder.Configuration["DEEPL_API_KEY"],
	translationSection[nameof(DeepLOptions.ApiKeySecretArn)]).ConfigureAwait(false);
builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
{
	[$"{DeepLOptions.SectionName}:{nameof(DeepLOptions.ApiKey)}"] = deepLApiKey,
});

// Machine translation for the question-authoring screen. With no credential
// the API reports translation unavailable, in Development as everywhere else.
// See ADR-0062, ADR-0109.
builder.Services.AddHpacSafetyTranslation(builder.Configuration);

// The temporary interim issuer's RSA private key (issue #648, ADR-0172):
// resolved once, here, from Secrets Manager when Terraform supplies an ARN —
// staging only, and only when HpacSafety:Authentication:InterimIssuer:Enabled
// is set. Left unset in Development, every test host, and production, where
// AddHpacSafetyAuthentication never reads it. Mirrors the origin-verification
// secret and the DeepL key above (#597).
var interimIssuerSection = builder.Configuration.GetSection(
	$"{HpacAuthenticationOptions.SectionName}:InterimIssuer");
var interimIssuerSigningKeyPem = await SecretArnResolver.ResolveAsync(
	interimIssuerSection[nameof(InterimIssuerOptions.SigningKeyPem)],
	interimIssuerSection[nameof(InterimIssuerOptions.SigningKeySecretArn)]).ConfigureAwait(false);
builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
{
	[$"{HpacAuthenticationOptions.SectionName}:InterimIssuer:{nameof(InterimIssuerOptions.SigningKeyPem)}"] =
		interimIssuerSigningKeyPem,
});

// Identity is a signed JWT this API validates; it never sees a password. In
// Development the API also issues the tokens it validates, so the same
// middleware and the same policies run either way and only the issuer and key
// differ. See ADR-0064 and ADR-0066. Outside Development, the temporary
// interim issuer (ADR-0172) may do the same with its own RS256 key, in
// staging only, until a real provider replaces it.
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

// The identity provider is an external dependency not yet chosen (ADR-0064).
// Outside Development, an empty Authority no longer fails startup — this
// environment still serves public endpoints, but no bearer token can ever
// validate here, so sign-in, review, and administration cannot work until an
// Authority is configured (ADR-0158). Logged once at startup rather than on
// every refused request, so it never touches request-path logging. The
// temporary interim issuer (ADR-0172) is the one thing that makes sign-in
// work here without an Authority, so its warning is suppressed while it is
// enabled.
var authenticationOptions = app.Services.GetRequiredService<IOptions<HpacAuthenticationOptions>>().Value;
var interimIssuerEnabled = !app.Environment.IsDevelopment() && authenticationOptions.InterimIssuer.Enabled;

if (!app.Environment.IsDevelopment()
	&& !interimIssuerEnabled
	&& string.IsNullOrWhiteSpace(authenticationOptions.Authority))
{
	StartupLog.LogNoAuthorityConfigured(app.Logger, HpacAuthenticationOptions.SectionName);
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

// The same answer under /api/: CloudFront forwards only /api/* to this
// function, unchanged, so this is the health route a deployment can reach
// through the site's own address. The release's smoke test calls it (#647).
app.MapGet("/api/health", () => Results.Ok(new { status = "ok" }));

// Sign-in, and who the caller is. The token endpoint inside is mapped only in
// Development or where the temporary interim issuer is enabled (ADR-0172).
app.MapAuth(app.Environment.IsDevelopment(), interimIssuerEnabled);

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

/// <summary>The one startup-time log message Program.cs's top-level statements need.</summary>
internal static partial class StartupLog
{
	/// <summary>
	///     Logged once at startup, never on the request path — outside
	///     Development, with no identity provider configured (ADR-0158).
	/// </summary>
	[LoggerMessage(
		Level = LogLevel.Warning,
		Message = "{Section}:Authority is empty. This environment will start and serve its public endpoints, "
			+ "but no bearer token can validate — sign-in, review, and administration cannot work here until "
			+ "an identity provider is configured. See ADR-0158.")]
	public static partial void LogNoAuthorityConfigured(ILogger logger, string section);
}
