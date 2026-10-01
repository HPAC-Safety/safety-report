using System.Text.Json;
using Amazon.Runtime;
using Amazon.S3;
using Amazon.S3.Model;
using DotNet.Testcontainers.Builders;
using DotNet.Testcontainers.Containers;

namespace HpacSafety.Testing;

/// <summary>
///     An S3-compatible server in a container, with one private, versioned bucket
///     shaped like the production one (infra/storage.tf, ADR-0096).
/// </summary>
/// <remarks>
///     <para>
///         Compiled into each test project that needs one, so the image is pinned in
///         exactly one place. The API is the part under test — the pre-signed
///         signature, the private bucket, the 403 on a retargeted URL, the version
///         purge — and none of that needs an AWS account or a network.
///     </para>
///     <para>
///         RustFS, not MinIO: MinIO withdrew its public images from quay.io and
///         Docker Hub and archived its repository, so a clean machine could no
///         longer pull the server at all (ADR-0110). Pinned rather than floating on
///         <c>latest</c>, for the same reason the Postgres container is: a server
///         that moves underneath the suite is a failure nobody can reproduce.
///     </para>
/// </remarks>
internal static class S3Emulator
{
	/// <summary>The pinned server image. docker-compose.yml pins the same one.</summary>
	public const string Image = "rustfs/rustfs:1.0.0";

	public const string AccessKey = "hpac-test";

	public const string SecretKey = "hpac-test-secret";

	private const ushort S3Port = 9000;

	/// <summary>
	///     A server that counts as started once <c>/health/ready</c> answers 200. Plain
	///     <c>/health</c> answers as soon as the process is up, while S3 requests still
	///     get a 503 until storage and IAM are ready. The caller starts and disposes it.
	/// </summary>
	public static IContainer Build()
	{
		return new ContainerBuilder(Image)
			.WithPortBinding(S3Port, true)
			.WithEnvironment("RUSTFS_ACCESS_KEY", AccessKey)
			.WithEnvironment("RUSTFS_SECRET_KEY", SecretKey)
			.WithWaitStrategy(Wait.ForUnixContainer().UntilHttpRequestIsSucceeded(request => request.ForPort(S3Port).ForPath("/health/ready")))
			.Build();
	}

	/// <summary>The endpoint the S3 client and every pre-signed URL address.</summary>
	public static string ServiceUrl(IContainer server)
	{
		ArgumentNullException.ThrowIfNull(server);

		return new UriBuilder(Uri.UriSchemeHttp, server.Hostname, server.GetMappedPublicPort(S3Port)).Uri.ToString();
	}

	/// <summary>
	///     Creates <paramref name="bucketName" /> on a started server: private, with
	///     no public read policy, and versioned, so deleting an upload is proven
	///     against the case where a plain delete would only leave a marker over the
	///     bytes.
	/// </summary>
	/// <returns>A client straight to the bucket; the caller disposes it.</returns>
	public static async Task<AmazonS3Client> CreateBucket(IContainer server,
														  string bucketName)
	{
		var client = new AmazonS3Client(
			new BasicAWSCredentials(AccessKey, SecretKey),
			new AmazonS3Config { ServiceURL = ServiceUrl(server), ForcePathStyle = true, AuthenticationRegion = "ca-central-1" });

		await client.PutBucketAsync(bucketName);
		await client.PutBucketVersioningAsync(new PutBucketVersioningRequest
		{
			BucketName = bucketName,
			VersioningConfig = new S3BucketVersioningConfig { Status = VersionStatus.Enabled },
		});

		await ApplyQuarantineLifecycle(client, bucketName);

		return client;
	}

	/// <summary>
	///     The quarantine expiry rule dev-up applies to the local bucket and
	///     infra/storage.tf applies to production, read from the one file
	///     docker-compose.yml also mounts (CON-INF-010, REQ-MED-005).
	/// </summary>
	public static string LifecycleRulePath()
	{
		var directory = new DirectoryInfo(AppContext.BaseDirectory);
		while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "HpacSafety.slnx")))
		{
			directory = directory.Parent;
		}

		return Path.Combine(
			directory?.FullName ?? throw new InvalidOperationException("The repository root was not found."),
			"docker",
			"s3-quarantine-lifecycle.json");
	}

	private static async Task ApplyQuarantineLifecycle(AmazonS3Client client,
													   string bucketName)
	{
		using var document = JsonDocument.Parse(await File.ReadAllTextAsync(LifecycleRulePath()));
		var rules = document.RootElement.GetProperty("Rules").EnumerateArray().Select(rule => new LifecycleRule
		{
			Id = rule.GetProperty("ID").GetString(),
			Status = LifecycleRuleStatus.FindValue(rule.GetProperty("Status").GetString()!),
			Filter = new LifecycleFilter
			{
				LifecycleFilterPredicate = new LifecyclePrefixPredicate { Prefix = rule.GetProperty("Filter").GetProperty("Prefix").GetString() },
			},
			Expiration = new LifecycleRuleExpiration { Days = rule.GetProperty("Expiration").GetProperty("Days").GetInt32() },
			NoncurrentVersionExpiration = new LifecycleRuleNoncurrentVersionExpiration
			{
				NoncurrentDays = rule.GetProperty("NoncurrentVersionExpiration").GetProperty("NoncurrentDays").GetInt32(),
			},
			AbortIncompleteMultipartUpload = new LifecycleRuleAbortIncompleteMultipartUpload
			{
				DaysAfterInitiation = rule.GetProperty("AbortIncompleteMultipartUpload").GetProperty("DaysAfterInitiation").GetInt32(),
			},
		}).ToList();

		await client.PutLifecycleConfigurationAsync(new PutLifecycleConfigurationRequest
		{
			BucketName = bucketName,
			Configuration = new LifecycleConfiguration { Rules = rules },
		});
	}
}
