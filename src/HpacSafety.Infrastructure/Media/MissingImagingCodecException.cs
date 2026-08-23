namespace HpacSafety.Infrastructure.Media;

/// <summary>
/// The runtime's imaging library cannot decode a format this deployment accepts.
/// <para>
/// Thrown when the stripper is constructed, so the process fails to start rather
/// than degrading silently. The failure mode this prevents is specific and
/// nasty: without libheif, every iPhone reporter's upload would be refused as
/// unrecognisable content, and nothing in the logs would say why.
/// </para>
/// </summary>
public sealed class MissingImagingCodecException : Exception
{
    /// <summary>Creates the exception.</summary>
    public MissingImagingCodecException()
        : this("The imaging library cannot decode a format this deployment accepts.")
    {
    }

    /// <summary>Creates the exception with a developer-facing message.</summary>
    public MissingImagingCodecException(string message)
        : base(message)
    {
    }

    /// <summary>Creates the exception with an inner cause.</summary>
    public MissingImagingCodecException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
