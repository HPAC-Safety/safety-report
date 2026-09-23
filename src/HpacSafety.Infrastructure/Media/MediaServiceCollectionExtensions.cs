using Amazon.S3;
using HpacSafety.Core;
using HpacSafety.Core.Features.Reporting;
using HpacSafety.Infrastructure.Storage;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace HpacSafety.Infrastructure.Media;

/// <summary>Registers private object storage and the media-ingest pipeline.</summary>
public static class MediaServiceCollectionExtensions
{
	/// <summary>
	///     Adds <see cref="IBlobStore" />, <see cref="MediaIngestor" />, and the
	///     ports it depends on.
	/// </summary>
	/// <param name="services">The container.</param>
	/// <param name="configuration">Application configuration.</param>
	/// <param name="isDevelopment">
	///     True only in Development. A developer's machine writes quarantine,
	///     original, and stripped bytes to local disk rather than a real bucket, so
	///     the same code path runs everywhere and only the adapter differs.
	/// </param>
	public static IServiceCollection AddHpacSafetyMedia(
		this IServiceCollection services,
		IConfiguration configuration,
		bool isDevelopment)
	{
		ArgumentNullException.ThrowIfNull(services);
		ArgumentNullException.ThrowIfNull(configuration);

		services.Configure<MediaPolicyOptions>(configuration.GetSection("HpacSafety:Media:Policy"));
		services.AddSingleton(provider => provider.GetRequiredService<IOptions<MediaPolicyOptions>>().Value.ToPolicy());

		services.AddSingleton(MediaSnifferChain.Default());
		services.AddSingleton<IMediaSniffer>(provider => provider.GetRequiredService<MediaSnifferChain>());
		services.AddSingleton(provider =>
			new MagickNetExifStripper(provider.GetRequiredService<MediaPolicy>().AcceptedTypes));
		services.AddSingleton<IExifStripper>(provider => provider.GetRequiredService<MagickNetExifStripper>());

		// Video is remuxed rather than decoded, by ffmpeg as a child process
		// (ADR-0094). A deployment without ffmpeg still accepts video: the
		// remuxer reports that it produced nothing and the original is retained.
		services.AddSingleton<IVideoRemuxer>(provider =>
			new FfmpegVideoRemuxer(provider.GetRequiredService<ILogger<FfmpegVideoRemuxer>>()));

		if (isDevelopment)
		{
			services.Configure<FileSystemBlobStoreOptions>(
				configuration.GetSection("HpacSafety:Media:Storage:FileSystem"));
			services.AddSingleton<IBlobStore>(provider =>
				new FileSystemBlobStore(
					provider.GetRequiredService<IOptions<FileSystemBlobStoreOptions>>().Value,
					provider.GetRequiredService<TimeProvider>()));
		}
		else
		{
			services.Configure<S3BlobStoreOptions>(configuration.GetSection("HpacSafety:Media:Storage:S3"));
			services.AddSingleton<IAmazonS3>(_ => new AmazonS3Client());
			services.AddSingleton<IBlobStore>(provider =>
				new S3BlobStore(
					provider.GetRequiredService<IAmazonS3>(),
					provider.GetRequiredService<IOptions<S3BlobStoreOptions>>().Value,
					provider.GetRequiredService<TimeProvider>()));
		}

		services.AddScoped<MediaIngestor>();
		services.AddScoped<ReviewerMediaLink>();

		return services;
	}
}
