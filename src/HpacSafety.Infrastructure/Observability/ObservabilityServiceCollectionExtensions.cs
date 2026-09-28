using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace HpacSafety.Infrastructure.Observability;

/// <summary>Registers <see cref="IMetricsPublisher" /> for the API and the Worker alike.</summary>
public static class ObservabilityServiceCollectionExtensions
{
	public static IServiceCollection AddHpacSafetyObservability(
		this IServiceCollection services,
		IConfiguration configuration)
	{
		ArgumentNullException.ThrowIfNull(services);
		ArgumentNullException.ThrowIfNull(configuration);

		services.Configure<MetricsOptions>(configuration.GetSection(MetricsOptions.SectionName));
		services.AddSingleton<IMetricsPublisher, EmbeddedMetricsPublisher>();

		return services;
	}
}
