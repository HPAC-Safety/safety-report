namespace HpacSafety.Core.SharedKernel;

/// <summary>
/// Private object storage for uploaded media. There are no public object URLs,
/// ever — a reviewer sees a short-lived pre-signed GET. See
/// docs/data-handling.md.
/// </summary>
public interface IBlobStore
{
    /// <summary>A short-lived URL a browser may PUT one file to.</summary>
    Task<Uri> CreateUploadUrlAsync(string key, string contentType, TimeSpan lifetime, CancellationToken cancellationToken);

    /// <summary>A short-lived URL an administrator may GET one file from.</summary>
    Task<Uri> CreateReadUrlAsync(string key, TimeSpan lifetime, CancellationToken cancellationToken);

    /// <summary>Opens stored bytes for server-side work such as EXIF stripping.</summary>
    Task<Stream> OpenReadAsync(string key, CancellationToken cancellationToken);

    /// <summary>Writes bytes, such as the EXIF-stripped derivative.</summary>
    Task WriteAsync(string key, Stream content, string contentType, CancellationToken cancellationToken);
}
