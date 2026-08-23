namespace HpacSafety.Infrastructure.Storage;

/// <summary>
/// A pre-signed URL was presented for a key, an operation, or a moment it was
/// not signed for. S3 answers this case with <c>403</c>; the filesystem store
/// throws, so the development stand-in refuses exactly what production refuses.
/// </summary>
public sealed class PresignedUrlRejectedException : Exception
{
    /// <summary>Creates the exception.</summary>
    public PresignedUrlRejectedException()
        : this("The pre-signed URL is not valid for this request.")
    {
    }

    /// <summary>Creates the exception with a developer-facing message. It never echoes user content.</summary>
    public PresignedUrlRejectedException(string message)
        : base(message)
    {
    }

    /// <summary>Creates the exception with an inner cause.</summary>
    public PresignedUrlRejectedException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
