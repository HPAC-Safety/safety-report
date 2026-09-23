namespace HpacSafety.Infrastructure.Storage;

/// <summary>Configuration for <see cref="S3BlobStore" />.</summary>
public sealed class S3BlobStoreOptions
{
	/// <summary>
	///     The private bucket. It has no public read policy and never gains one —
	///     see docs/data-handling.md.
	/// </summary>
	public string BucketName { get; set; } = string.Empty;

	/// <summary>
	///     An S3-compatible endpoint to use instead of AWS — the MinIO container in
	///     development. Empty in production, where the SDK resolves S3 for the region
	///     and authenticates as the ECS task role.
	/// </summary>
	public string ServiceUrl { get; set; } = string.Empty;

	/// <summary>
	///     The endpoint a browser reaches the same bucket by, when it differs from
	///     <see cref="ServiceUrl" />. Reviewer URLs are signed for this host.
	/// </summary>
	public string PublicServiceUrl { get; set; } = string.Empty;

	/// <summary>
	///     Access key for an S3-compatible endpoint. Development only; production
	///     leaves both empty and uses the task role.
	/// </summary>
	public string AccessKey { get; set; } = string.Empty;

	/// <summary>Secret key paired with <see cref="AccessKey" />. Development only.</summary>
	public string SecretKey { get; set; } = string.Empty;
}
