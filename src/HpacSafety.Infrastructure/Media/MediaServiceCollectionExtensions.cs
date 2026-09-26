using Amazon.Runtime;
using Amazon.S3;
using HpacSafety.Core;
using HpacSafety.Core.Features.PrivateAttachments;
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
	/// <remarks>
	///     There is one storage adapter, <see cref="S3BlobStore" />, in every
	///     environment. Configuration decides where it points: with no
	///     <see cref="S3BlobStoreOptions.ServiceUrl" /> it is AWS S3, authenticated by
	///     the ECS task role; with one it is the S3-compatible container docker-compose runs
	///     for development (ADR-0096).
	/// </remarks>
	public static IServiceCollection AddHpacSafetyMedia(
		this IServiceCollection services,
		IConfiguration configuration)
	{
		ArgumentNullException.ThrowIfNull(services);
		ArgumentNullException.ThrowIfNull(configuration);

		var policySection = configuration.GetSection(MediaPolicyOptions.SectionName);

		// The single MaxByteSize became one limit per kind (ADR-0126). Left set, it
		// would bind to nothing and the deployment would run on limits nobody there
		// chose, so it is refused here rather than in a checklist.
		if (policySection[MediaPolicyOptions.RetiredMaxByteSizeKey] is not null)
		{
			throw new InvalidOperationException(
				$"{MediaPolicyOptions.SectionName}:{MediaPolicyOptions.RetiredMaxByteSizeKey} is retired; set MaxVideoByteSize, MaxImageByteSize, and MaxDocumentByteSize instead (ADR-0126).");
		}

		services.Configure<MediaPolicyOptions>(policySection);
		services.AddSingleton(provider => provider.GetRequiredService<IOptions<MediaPolicyOptions>>().Value.ToPolicy());

		// A staff private attachment's cap, separate from the reporter caps (ADR-0135).
		services.Configure<PrivateAttachmentOptions>(configuration.GetSection(PrivateAttachmentOptions.SectionName));
		services.AddSingleton(provider => provider.GetRequiredService<IOptions<PrivateAttachmentOptions>>().Value.ToPolicy());

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

		services.Configure<S3BlobStoreOptions>(configuration.GetSection("HpacSafety:Media:Storage:S3"));
		services.AddSingleton<IBlobStore>(provider =>
		{
			var options = provider.GetRequiredService<IOptions<S3BlobStoreOptions>>().Value;
			var s3 = CreateClient(options, options.ServiceUrl);
			var signer = string.IsNullOrWhiteSpace(options.PublicServiceUrl)
				? null
				: CreateClient(options, options.PublicServiceUrl);

			return new S3BlobStore(s3, options, provider.GetRequiredService<TimeProvider>(), signer);
		});

		services.AddScoped<MediaIngestor>();
		services.AddScoped<UploadLink>();
		services.AddScoped<ReviewerMediaLink>();
		services.AddScoped<PublicMediaLink>();
		services.AddScoped<PrivateAttachmentLink>();

		return services;
	}

	private static AmazonS3Client CreateClient(S3BlobStoreOptions options,
											   string serviceUrl)
	{
		if (string.IsNullOrWhiteSpace(serviceUrl))
		{
			// AWS itself: region and credentials come from the environment the
			// task runs in. No access key is ever configured for production.
			return new AmazonS3Client();
		}

		var config = new AmazonS3Config
		{
			ServiceURL = serviceUrl,
			ForcePathStyle = true,
			AuthenticationRegion = "ca-central-1",
		};

		return string.IsNullOrWhiteSpace(options.AccessKey)
			? new AmazonS3Client(config)
			: new AmazonS3Client(new BasicAWSCredentials(options.AccessKey, options.SecretKey), config);
	}
}
