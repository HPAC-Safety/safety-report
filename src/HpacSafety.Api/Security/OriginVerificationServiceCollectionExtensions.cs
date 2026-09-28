using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace HpacSafety.Api.Security;

/// <summary>Registers <see cref="OriginVerificationOptions" />.</summary>
public static class OriginVerificationServiceCollectionExtensions
{
	/// <summary>
	///     Binds <see cref="OriginVerificationOptions" /> and, outside
	///     Development, refuses to start without a configured
	///     <see cref="OriginVerificationOptions.Secret" />. Fail closed: a
	///     missing Secrets Manager value in a deployed environment must stop the
	///     API cold, not leave <see cref="OriginVerificationMiddlewareExtensions.UseCloudFrontOriginVerification" />
	///     letting every request through unverified. Development, and the test
	///     host that passes <paramref name="useDevelopmentIssuer" /> as
	///     <see langword="true" />, may run with no CloudFront in front of them
	///     at all.
	/// </summary>
	/// <param name="services">The container.</param>
	/// <param name="configuration">Application configuration.</param>
	/// <param name="useDevelopmentIssuer">
	///     Whether this host runs in Development — the same environment decision
	///     <c>AddHpacSafetyAuthentication</c> takes, made by the caller rather
	///     than read from configuration here.
	/// </param>
	/// <exception cref="InvalidOperationException">
	///     No <see cref="OriginVerificationOptions.Secret" /> is configured and
	///     this is not Development. Failing at startup is the point.
	/// </exception>
	public static IServiceCollection AddHpacSafetyOriginVerification(
		this IServiceCollection services,
		IConfiguration configuration,
		bool useDevelopmentIssuer)
	{
		ArgumentNullException.ThrowIfNull(services);
		ArgumentNullException.ThrowIfNull(configuration);

		var section = configuration.GetSection(OriginVerificationOptions.SectionName);
		services.Configure<OriginVerificationOptions>(section);

		if (!useDevelopmentIssuer && string.IsNullOrWhiteSpace(section[nameof(OriginVerificationOptions.Secret)]))
		{
			throw new InvalidOperationException(
				$"{OriginVerificationOptions.SectionName}:{nameof(OriginVerificationOptions.Secret)} is required outside "
				+ "Development. Without it, the API would answer any request that reaches its Function URL directly, "
				+ "bypassing CloudFront (ADR-0159).");
		}

		return services;
	}
}
