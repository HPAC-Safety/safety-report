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
	///     When the configuration cannot produce a host that validates anything: no
	///     development signing key, a key too short to sign safely, or no authority
	///     outside Development. Failing at startup is the point — a host that
	///     silently accepts nothing, or silently accepts everything, is worse.
	/// </exception>
	public static IServiceCollection AddHpacSafetyAuthentication(
		this IServiceCollection services, IConfiguration configuration, bool useDevelopmentIssuer)
	{
		ArgumentNullException.ThrowIfNull(services);
		ArgumentNullException.ThrowIfNull(configuration);

		var options = new HpacAuthenticationOptions();
		configuration.GetSection(HpacAuthenticationOptions.SectionName).Bind(options);

		services.Configure<HpacAuthenticationOptions>(
			configuration.GetSection(HpacAuthenticationOptions.SectionName));

		var parameters = ValidationParametersFor(options, useDevelopmentIssuer);

		services
			.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
			.AddJwtBearer(jwt =>
			{
				jwt.TokenValidationParameters = parameters;
				jwt.MapInboundClaims = false;

				if (!useDevelopmentIssuer)
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
			services.AddSingleton<IDevelopmentCredentialSource, FixedAccountCredentialSource>();
			services.AddSingleton<IDevelopmentCredentialSource, MembersSiteCredentialSource>();

			services.Configure<MembersSiteLoginOptions>(
				configuration.GetSection(MembersSiteLoginOptions.SectionName));

			services
				.AddHttpClient(MembersSiteCredentialSource.HttpClientName, (provider, client) =>
				{
					var membersOptions = provider.GetRequiredService<IOptions<MembersSiteLoginOptions>>().Value;
					client.BaseAddress = new Uri(membersOptions.BaseUrl);
					client.Timeout = membersOptions.Timeout;
				})
				.ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler
				{
					UseCookies = false,
					AllowAutoRedirect = false
				});
		}

		return services;
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
		HpacAuthenticationOptions options, bool useDevelopmentIssuer)
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

	private static TokenValidationParameters ProviderParameters(HpacAuthenticationOptions options)
	{
		if (string.IsNullOrWhiteSpace(options.Authority))
		{
			throw new InvalidOperationException(
				$"{HpacAuthenticationOptions.SectionName}:Authority is required outside Development. "
				+ "Without it there are no signing keys to validate against.");
		}

		// No IssuerSigningKey: the authority's published keys are fetched and
		// rotated by the handler.
		return Common(options, options.Issuer ?? options.Authority, null);
	}

	private static TokenValidationParameters Common(
		HpacAuthenticationOptions options, string issuer, SecurityKey? signingKey)
	{
		return new TokenValidationParameters
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
			NameClaimType = ClaimTypes.NameIdentifier
		};
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
				await WriteProblemAsync(
					context.HttpContext,
					StatusCodes.Status401Unauthorized,
					"Not signed in.",
					"These endpoints are available to signed-in HPAC members.",
					"https://hpac.ca/problems/not-signed-in").ConfigureAwait(false);
			},
			OnForbidden = context => WriteProblemAsync(
				context.HttpContext,
				StatusCodes.Status403Forbidden,
				"Not permitted.",
				"This operation needs a role this member does not have.",
				"https://hpac.ca/problems/insufficient-role")
		};
	}

	// Deliberately says nothing about which claim was missing or what role
	// would have been enough.
	private static async Task WriteProblemAsync(
		HttpContext context, int status, string title, string detail, string type)
	{
		context.Response.StatusCode = status;
		context.Response.ContentType = "application/problem+json";

		await context.Response.WriteAsync(JsonSerializer.Serialize(new
		{
			type,
			title,
			status,
			detail
		})).ConfigureAwait(false);
	}
}
