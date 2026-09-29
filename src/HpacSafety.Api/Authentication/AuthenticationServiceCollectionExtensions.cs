using System.Security.Claims;
using System.Text;
using System.Text.Json;
using HpacSafety.Core.Features.Moderation;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace HpacSafety.Api.Authentication;

/// <summary>Registers bearer-token validation and the three role policies.</summary>
public static class AuthenticationServiceCollectionExtensions
{
	/// <summary>
	///     Adds authentication and authorization.
	/// </summary>
	/// <param name="services">The service collection.</param>
	/// <param name="configuration">Configuration to bind options from.</param>
	/// <param name="useDevelopmentIssuer">
	///     Whether this host signs and accepts its own development tokens. The
	///     environment decision is made by the caller, not read from configuration
	///     here — the same shape <c>AddHpacSafetyTranslation</c> uses.
	/// </param>
	/// <returns>The same collection.</returns>
	/// <exception cref="InvalidOperationException">
	///     When Development cannot produce a host that validates anything: no
	///     development signing key, or a key too short to sign safely. Outside
	///     Development, a missing <see cref="HpacAuthenticationOptions.Authority" />
	///     no longer throws — the identity provider is an external dependency not
	///     yet chosen (ADR-0064), and an environment without one still starts and
	///     serves its public endpoints; only sign-in, review, and administration
	///     cannot work there (ADR-0158). The host instead registers a bearer scheme
	///     that can never validate a token: no authority, no signing keys, and an
	///     issuer no real token will ever carry.
	/// </exception>
	public static IServiceCollection AddHpacSafetyAuthentication(
		this IServiceCollection services,
		IConfiguration configuration,
		bool useDevelopmentIssuer)
	{
		ArgumentNullException.ThrowIfNull(services);
		ArgumentNullException.ThrowIfNull(configuration);

		var options = new HpacAuthenticationOptions();
		configuration.GetSection(HpacAuthenticationOptions.SectionName).Bind(options);

		services.Configure<HpacAuthenticationOptions>(
			configuration.GetSection(HpacAuthenticationOptions.SectionName));

		// The temporary interim issuer (issue #648, ADR-0172) is honored only
		// outside Development, which keeps its own HS256 issuer regardless of
		// this setting.
		var interimIssuerEnabled = !useDevelopmentIssuer && options.InterimIssuer.Enabled;
		InterimIssuerSigningKey? interimSigningKey = null;

		if (interimIssuerEnabled)
		{
			if (!string.IsNullOrWhiteSpace(options.Authority))
			{
				// A real provider replaces the interim issuer; it is never
				// meant to run alongside one.
				throw new InvalidOperationException(
					$"{HpacAuthenticationOptions.SectionName}:InterimIssuer:Enabled and "
					+ $"{HpacAuthenticationOptions.SectionName}:Authority are both configured. "
					+ "A real identity provider replaces the interim issuer — configure exactly one.");
			}

			if (string.IsNullOrWhiteSpace(options.InterimIssuer.SigningKeyPem))
			{
				throw new InvalidOperationException(
					$"{HpacAuthenticationOptions.SectionName}:InterimIssuer:SigningKeyPem is required "
					+ "when InterimIssuer:Enabled is true. It signs the tokens this host issues and then "
					+ "validates. In a deployed environment it is resolved from "
					+ $"{HpacAuthenticationOptions.SectionName}:InterimIssuer:SigningKeySecretArn at cold start.");
			}

			interimSigningKey = InterimIssuerSigningKey.FromPem(options.InterimIssuer.SigningKeyPem);
		}

		var parameters = interimIssuerEnabled
			? InterimIssuerParameters(options, interimSigningKey!.SecurityKey)
			: ValidationParametersFor(options, useDevelopmentIssuer);

		services
			.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
			.AddJwtBearer(jwt =>
			{
				jwt.TokenValidationParameters = parameters;
				jwt.MapInboundClaims = false;

				// No Authority for the interim issuer either: it validates
				// in-process against its own key, with no metadata fetch —
				// exactly like the development issuer, and unlike a real
				// provider.
				if (!useDevelopmentIssuer && !interimIssuerEnabled)
				{
					jwt.Authority = options.Authority;
				}

				jwt.Events = ProblemDetailsEvents();
			});

		services.AddAuthorizationBuilder()
			.AddPolicy(HpacPolicies.Member, policy => policy.RequireAuthenticatedUser())
			.AddPolicy(HpacPolicies.Reviewer, policy => policy
				.RequireAuthenticatedUser()
				.RequireAssertion(context =>
					MemberRoles.EffectiveRole(context.User, options.RoleClaimType) >= MemberRole.SafetyOfficer))
			.AddPolicy(HpacPolicies.Administrator, policy => policy
				.RequireAuthenticatedUser()
				.RequireAssertion(context =>
					MemberRoles.EffectiveRole(context.User, options.RoleClaimType) >= MemberRole.Administrator));

		if (useDevelopmentIssuer)
		{
			services.AddSingleton<DevelopmentTokenIssuer>();
			services.AddSingleton<IMemberTokenIssuer>(provider => provider.GetRequiredService<DevelopmentTokenIssuer>());
			services.AddSingleton<IDevelopmentCredentialSource, FixedAccountCredentialSource>();
			services.AddSingleton<IDevelopmentCredentialSource, MembersSiteCredentialSource>();

			services.Configure<MembersSiteLoginOptions>(
				configuration.GetSection(MembersSiteLoginOptions.SectionName));

			services
				.AddHttpClient(MembersSiteCredentialSource.HttpClientName, (provider,
																			client) =>
				{
					var membersOptions = provider.GetRequiredService<IOptions<MembersSiteLoginOptions>>().Value;
					client.BaseAddress = new Uri(membersOptions.BaseUrl);
					client.Timeout = membersOptions.Timeout;
				})
				.ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler
				{
					UseCookies = false,
					AllowAutoRedirect = false,
				});
		}
		else if (interimIssuerEnabled)
		{
			// The members-site check only — the owner's decision on issue #648:
			// the password is verified live against the members site, and "we
			// only use the hard-coded accounts to determine administrators"
			// (MembersSiteLoginOptions' email lists). NOT FixedAccountCredentialSource:
			// its admin/admin, officer/officer, user/user accounts are for a
			// developer's own machine, and on staging's public address they
			// would let anyone sign in as an Administrator. Everything
			// registered here is temporary (ADR-0172) and named "Interim".
			services.AddSingleton(interimSigningKey!);
			services.AddSingleton<InterimTokenIssuer>();
			services.AddSingleton<IMemberTokenIssuer>(provider => provider.GetRequiredService<InterimTokenIssuer>());
			services.AddSingleton<IDevelopmentCredentialSource, MembersSiteCredentialSource>();

			services.Configure<MembersSiteLoginOptions>(
				configuration.GetSection(MembersSiteLoginOptions.SectionName));

			services
				.AddHttpClient(MembersSiteCredentialSource.HttpClientName, (provider,
																			client) =>
				{
					var membersOptions = provider.GetRequiredService<IOptions<MembersSiteLoginOptions>>().Value;
					client.BaseAddress = new Uri(membersOptions.BaseUrl);
					client.Timeout = membersOptions.Timeout;
				})
				.ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler
				{
					UseCookies = false,
					AllowAutoRedirect = false,
				});
		}

		return services;
	}

	/// <summary>
	///     The rules a temporary interim-issued token must satisfy (issue
	///     #648, ADR-0172): the same shape <see cref="Common" /> builds for
	///     every issuer, pinned to <see cref="InterimTokenIssuer.IssuerName" />
	///     and this host's own public key — no Authority, no metadata fetch.
	/// </summary>
	private static TokenValidationParameters InterimIssuerParameters(
		HpacAuthenticationOptions options,
		SecurityKey signingKey)
	{
		return Common(options, InterimTokenIssuer.IssuerName, signingKey);
	}

	/// <summary>
	///     The rules a token must satisfy. One definition, used both by the
	///     registration above and by the acceptance scenarios, so a scenario
	///     asserts the validation this API actually performs rather than a copy of
	///     it that could drift.
	/// </summary>
	/// <param name="options">The bound options.</param>
	/// <param name="useDevelopmentIssuer">Whether this host issues its own tokens.</param>
	/// <returns>The validation parameters.</returns>
	public static TokenValidationParameters ValidationParametersFor(
		HpacAuthenticationOptions options,
		bool useDevelopmentIssuer)
	{
		ArgumentNullException.ThrowIfNull(options);

		return useDevelopmentIssuer ? DevelopmentParameters(options) : ProviderParameters(options);
	}

	private static TokenValidationParameters DevelopmentParameters(HpacAuthenticationOptions options)
	{
		if (string.IsNullOrWhiteSpace(options.DevelopmentSigningKey))
		{
			throw new InvalidOperationException(
				$"{HpacAuthenticationOptions.SectionName}:DevelopmentSigningKey is required in Development. "
				+ "It signs the tokens this host issues and then validates.");
		}

		if (Encoding.UTF8.GetByteCount(options.DevelopmentSigningKey) < DevelopmentTokenIssuer.MinimumKeyBytes)
		{
			throw new InvalidOperationException(
				$"{HpacAuthenticationOptions.SectionName}:DevelopmentSigningKey must be at least "
				+ $"{DevelopmentTokenIssuer.MinimumKeyBytes} bytes. A shorter HS256 key weakens the signature, and a "
				+ "developer should not learn a habit here that would be wrong anywhere else.");
		}

		return Common(options, DevelopmentTokenIssuer.IssuerName, DevelopmentTokenIssuer.KeyFrom(options.DevelopmentSigningKey));
	}

	/// <summary>
	///     No real token will ever carry this issuer, so <see cref="Common" />'s
	///     <c>ValidIssuer</c> match can never succeed when no authority is
	///     configured. See <see cref="ProviderParameters" /> and ADR-0158.
	/// </summary>
	private const string NoAuthorityIssuer = "urn:hpac-safety:no-identity-provider-configured";

	private static TokenValidationParameters ProviderParameters(HpacAuthenticationOptions options)
	{
		if (string.IsNullOrWhiteSpace(options.Authority))
		{
			// The identity provider is an external dependency not yet chosen
			// (ADR-0064). Until it is, this environment still starts and serves
			// its public endpoints; only sign-in, review, and administration
			// cannot work here (ADR-0158). Refuse every token rather than fail
			// to start: no signing keys are ever resolvable, and the sentinel
			// issuer above matches nothing a real provider or the development
			// issuer would ever emit.
			return Common(options, NoAuthorityIssuer, null, noKeysCanEverResolve: true);
		}

		// No IssuerSigningKey: the authority's published keys are fetched and
		// rotated by the handler.
		return Common(options, options.Issuer ?? options.Authority, null);
	}

	private static TokenValidationParameters Common(
		HpacAuthenticationOptions options,
		string issuer,
		SecurityKey? signingKey,
		bool noKeysCanEverResolve = false)
	{
		var parameters = new TokenValidationParameters
		{
			ValidateIssuer = true,
			ValidIssuer = issuer,
			ValidateAudience = true,
			ValidAudience = options.Audience,
			ValidateLifetime = true,
			ValidateIssuerSigningKey = true,
			IssuerSigningKey = signingKey,
			RequireSignedTokens = true,
			RequireExpirationTime = true,

			// Thirty seconds, not the five-minute default. An expired token
			// should stop working when it expires.
			ClockSkew = TimeSpan.FromSeconds(30),

			// So RequireRole and IsInRole read the claim this provider emits,
			// and so the subject lands somewhere predictable.
			RoleClaimType = options.RoleClaimType,
			NameClaimType = ClaimTypes.NameIdentifier,
		};

		if (noKeysCanEverResolve)
		{
			// Belt and braces alongside the sentinel issuer above: even if a
			// caller somehow forged a token naming that issuer, there is still
			// no key this resolver will ever hand back to verify its signature.
			parameters.IssuerSigningKeyResolver = (_, _, _, _) => [];
		}

		return parameters;
	}

	/// <summary>
	///     Keeps the problem shape the web application already handles, rather
	///     than the handler's empty body with a WWW-Authenticate header.
	/// </summary>
	private static JwtBearerEvents ProblemDetailsEvents()
	{
		return new JwtBearerEvents
		{
			OnChallenge = async context =>
			{
				context.HandleResponse();
				await WriteProblem(
					context.HttpContext,
					StatusCodes.Status401Unauthorized,
					"Not signed in.",
					"These endpoints are available to signed-in HPAC members.",
					"https://hpac.ca/problems/not-signed-in").ConfigureAwait(false);
			},
			OnForbidden = context => WriteProblem(
				context.HttpContext,
				StatusCodes.Status403Forbidden,
				"Not permitted.",
				"This operation needs a role this member does not have.",
				"https://hpac.ca/problems/insufficient-role"),
		};
	}

	// Deliberately says nothing about which claim was missing or what role
	// would have been enough.
	private static async Task WriteProblem(
		HttpContext context,
		int status,
		string title,
		string detail,
		string type)
	{
		context.Response.StatusCode = status;
		context.Response.ContentType = "application/problem+json";

		await context.Response.WriteAsync(JsonSerializer.Serialize(new
		{
			type,
			title,
			status,
			detail,
		})).ConfigureAwait(false);
	}
}
