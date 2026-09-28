using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Options;

namespace HpacSafety.Api.Security;

/// <summary>
///     The API runs as a Lambda Function URL with authorization type
///     <c>NONE</c> — Lambda has no "allow only CloudFront" option — reached
///     only through CloudFront's <c>/api/*</c> behavior (#465). CloudFront's
///     origin request policy injects one shared-secret header; this middleware
///     refuses any request that does not carry it with a matching value,
///     which is every request that reached the Function URL directly rather
///     than through CloudFront. See ADR-0159, ADR-0042.
/// </summary>
public static class OriginVerificationMiddlewareExtensions
{
	/// <summary>
	///     Adds the check as the pipeline's first middleware, before
	///     <c>UseForwardedHeaders</c> — an unverified caller's headers are never
	///     trusted for anything, including which IP or protocol it claims.
	/// </summary>
	/// <param name="app">The pipeline builder.</param>
	public static IApplicationBuilder UseCloudFrontOriginVerification(this IApplicationBuilder app)
	{
		ArgumentNullException.ThrowIfNull(app);

		return app.Use(async (context,
							  next) =>
		{
			var options = context.RequestServices
				.GetRequiredService<IOptions<OriginVerificationOptions>>().Value;

			// No secret configured: Development and the test host, where there
			// is no CloudFront in front of the process at all.
			if (string.IsNullOrEmpty(options.Secret))
			{
				await next(context).ConfigureAwait(false);
				return;
			}

			var provided = context.Request.Headers[options.HeaderName].ToString();

			if (!FixedTimeEquals(provided, options.Secret))
			{
				context.Response.StatusCode = StatusCodes.Status403Forbidden;
				return;
			}

			await next(context).ConfigureAwait(false);
		});
	}

	/// <summary>
	///     A constant-time comparison, so a mismatched header's length or which
	///     byte differs never leaks through response timing.
	/// </summary>
	private static bool FixedTimeEquals(string provided,
										string expected)
	{
		var providedBytes = Encoding.UTF8.GetBytes(provided);
		var expectedBytes = Encoding.UTF8.GetBytes(expected);

		return providedBytes.Length == expectedBytes.Length
			   && CryptographicOperations.FixedTimeEquals(providedBytes, expectedBytes);
	}
}
