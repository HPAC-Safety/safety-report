namespace HpacSafety.Infrastructure.Storage;

/// <summary>Configuration for <see cref="S3BlobStore" />.</summary>
public sealed class S3BlobStoreOptions
{
    /// <summary>
    /// The private bucket. It has no public read policy and never gains one —
    /// see docs/data-handling.md.
    /// </summary>
    public string BucketName { get; set; } = string.Empty;
}
