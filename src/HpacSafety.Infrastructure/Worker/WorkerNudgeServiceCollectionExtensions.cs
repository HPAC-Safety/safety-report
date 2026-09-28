using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace HpacSafety.Infrastructure.Worker;

/// <summary>Registers <see cref="IWorkerNudge" />.</summary>
public static class WorkerNudgeServiceCollectionExtensions
{
	/// <summary>
	///     With <see cref="WorkerNudgeOptions.FunctionName" /> set — every
	///     deployed environment — registers <see cref="LambdaWorkerNudge" />
	///     against the AWS SDK's default credential and region resolution (the
	///     API's Lambda execution role). Left unset, registers
	///     <see cref="NoOpWorkerNudge" />: Development, and every test host.
	/// </summary>
	public static IServiceCollection AddHpacSafetyWorkerNudge(
		this IServiceCollection services,
		IConfiguration configuration)
	{
		ArgumentNullException.ThrowIfNull(services);
		ArgumentNullException.ThrowIfNull(configuration);

		services.Configure<WorkerNudgeOptions>(configuration.GetSection(WorkerNudgeOptions.SectionName));
		services.AddSingleton<OutboxNudgeInterceptor>();

		services.AddSingleton<IWorkerNudge>(provider =>
		{
			var options = provider.GetRequiredService<IOptions<WorkerNudgeOptions>>().Value;

			if (string.IsNullOrWhiteSpace(options.FunctionName))
			{
				return new NoOpWorkerNudge();
			}

			return new LambdaWorkerNudge(
				new AwsLambdaInvoker(),
				options.FunctionName,
				provider.GetRequiredService<ILogger<LambdaWorkerNudge>>());
		});

		return services;
	}
}
