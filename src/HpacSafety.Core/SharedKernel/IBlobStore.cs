namespace HpacSafety.Core.SharedKernel;

/// <summary>
/// Private object storage for uploaded media. There are no public object URLs,
/// ever — a reviewer sees a short-lived pre-signed GET. See
/// docs/data-handling.md.
/// <para>
/// Two rules bind every implementation, and both are covered by the shared
/// contract suite in <c>HpacSafety.Infrastructure.Tests</c> so that the
/// development stand-in cannot be weaker than the production adapter:
/// a URL is scoped to exactly one <see cref="BlobKey" /> and cannot be reused
/// for another, and every lifetime passes <see cref="BlobUrlLifetime.Validate" />.
/// </para>
/// </summary>
public interface IBlobStore
{
    /// <summary>A short-lived URL a browser may PUT one file to, and only that one key.</summary>
    Task<Uri> CreateUploadUrlAsync(BlobKey key, string contentType, TimeSpan lifetime, CancellationToken cancellationToken);

    /// <summary>A short-lived URL an administrator may GET one file from, and only that one key.</summary>
    Task<Uri> CreateReadUrlAsync(BlobKey key, TimeSpan lifetime, CancellationToken cancellationToken);

    /// <summary>Opens stored bytes for server-side work such as EXIF stripping.</summary>
    Task<Stream> OpenReadAsync(BlobKey key, CancellationToken cancellationToken);

    /// <summary>Writes bytes, such as the EXIF-stripped derivative.</summary>
    Task WriteAsync(BlobKey key, Stream content, string contentType, CancellationToken cancellationToken);
}
