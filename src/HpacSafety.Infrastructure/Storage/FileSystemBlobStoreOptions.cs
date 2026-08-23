namespace HpacSafety.Infrastructure.Storage;

/// <summary>Configuration for <see cref="FileSystemBlobStore" />.</summary>
public sealed class FileSystemBlobStoreOptions
{
    /// <summary>The directory blobs are written under. Created on first use.</summary>
    public string RootPath { get; set; } = string.Empty;

    /// <summary>
    /// Base64 HMAC key used to sign local pre-signed URLs. Left unset, a random
    /// key is generated per process — which is the right default for a
    /// development store, because it means a URL cannot outlive a restart.
    /// </summary>
    public string? SigningKey { get; set; }
}
