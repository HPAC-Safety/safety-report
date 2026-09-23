using System.Globalization;
using System.Text.Json;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace HpacSafety.Api.RateLimiting;

/// <summary>
///     Registers the two <c>RateLimiter</c> policies this API applies: public
///     submission (by trusted client IP) and sign-in (by attempted identity).
///     ASP.NET Core's built-in middleware, not a third-party package — see
///     issue #15.
/// </summary>
public static class RateLimitingServiceCollectionExtensions
{
	private const string SignInIdentityItemsKey = "HpacSafety.SignInIdentity";

	/// <summary>Adds both policies and the shared rejection handler.</summary>
	/// <param name="services">The container.</param>
	/// <param name="configuration">Application configuration.</param>
	public static IServiceCollection AddHpacSafetyRateLimiting(
		this IServiceCollection services,
		IConfiguration configuration)
	{
		ArgumentNullException.ThrowIfNull(services);
		ArgumentNullException.ThrowIfNull(configuration);

		services.Configure<RateLimitingOptions>(configuration.GetSection("HpacSafety:RateLimiting"));

		services.AddRateLimiter(limiterOptions =>
		{
			limiterOptions.OnRejected = OnRejected;

			limiterOptions.AddPolicy(RateLimitPolicies.PublicSubmission, httpContext =>
			{
				var options = httpContext.RequestServices
					.GetRequiredService<IOptions<RateLimitingOptions>>().Value.PublicSubmission;

				// The Forwarded Headers middleware (configured in Program.cs) has
				// already rewritten this to the real client IP when the request
				// came through the trusted ALB hop; otherwise it is the direct
				// connection's own address. Either way this is never a spoofable
				// header read a second time here.
				var partitionKey = httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";

				return RateLimitPartition.GetSlidingWindowLimiter(partitionKey, _ => new SlidingWindowRateLimiterOptions
				{
					PermitLimit = options.PermitLimit,
					Window = TimeSpan.FromSeconds(options.WindowSeconds),
					SegmentsPerWindow = 4,
					QueueLimit = options.QueueLimit,
					QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
				});
			});

			limiterOptions.AddPolicy(RateLimitPolicies.SignIn, httpContext =>
			{
				var options = httpContext.RequestServices
					.GetRequiredService<IOptions<RateLimitingOptions>>().Value.SignIn;

				var partitionKey = httpContext.Items.TryGetValue(SignInIdentityItemsKey, out var identity)
								   && identity is string { Length: > 0 } capturedIdentity
					? capturedIdentity
					: "unknown";

				return RateLimitPartition.GetSlidingWindowLimiter(partitionKey, _ => new SlidingWindowRateLimiterOptions
				{
					PermitLimit = options.PermitLimit,
					Window = TimeSpan.FromSeconds(options.WindowSeconds),
					SegmentsPerWindow = 4,
					QueueLimit = options.QueueLimit,
					QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
				});
			});
		});

		return services;
	}

	/// <summary>
	///     Reads only the <c>username</c> field from the sign-in request body,
	///     normalizes it, and stashes it for the sign-in policy's partition key,
	///     then rewinds the body so model binding still reads it whole. Runs only
	///     on the sign-in route; every other request passes through untouched.
	///     A body that fails to parse still gets a (shared, harmless) partition —
	///     the endpoint itself rejects the malformed request afterward.
	/// </summary>
	/// <param name="app">The pipeline builder.</param>
	/// <param name="signInPath">The sign-in route to capture the identity from.</param>
	public static IApplicationBuilder UseSignInIdentityCapture(this IApplicationBuilder app,
															   PathString signInPath)
	{
		ArgumentNullException.ThrowIfNull(app);

		return app.Use(async (context,
							  next) =>
		{
			if (HttpMethods.IsPost(context.Request.Method)
				&& context.Request.Path.Equals(signInPath))
			{
				context.Request.EnableBuffering();

				try
				{
					using var document = await JsonDocument.ParseAsync(
						context.Request.Body, cancellationToken: context.RequestAborted).ConfigureAwait(false);

					if (document.RootElement.TryGetProperty("username", out var usernameElement)
						&& usernameElement.ValueKind == JsonValueKind.String)
					{
						var normalized = usernameElement.GetString()?.Trim().ToUpperInvariant();
						if (!string.IsNullOrEmpty(normalized))
						{
							context.Items[SignInIdentityItemsKey] = normalized;
						}
					}
				}
				catch (JsonException)
				{
					// Left uncaptured on purpose — the endpoint's own model
					// binding rejects the malformed body with its usual error.
				}

				context.Request.Body.Position = 0;
			}

			await next(context).ConfigureAwait(false);
		});
	}

	private static ValueTask OnRejected(OnRejectedContext context,
										CancellationToken cancellationToken)
	{
		context.HttpContext.Response.StatusCode = StatusCodes.Status429TooManyRequests;

		if (context.Lease.TryGetMetadata(MetadataName.RetryAfter, out var retryAfter))
		{
			context.HttpContext.Response.Headers.RetryAfter =
				((int)retryAfter.TotalSeconds).ToString(CultureInfo.InvariantCulture);
		}

		var problem = new ProblemDetails
		{
			Type = "https://hpac.ca/problems/rate-limited",
			Title = "Too many requests.",
			Status = StatusCodes.Status429TooManyRequests,
			Detail = "Please wait before trying again. If this keeps happening, contact HPAC at safety@hpac.ca.",
		};

		return new ValueTask(context.HttpContext.Response.WriteAsJsonAsync(
			problem, options: null, contentType: "application/problem+json", cancellationToken));
	}
}
