namespace HpacSafety.Core.SharedKernel;

/// <summary>
/// Thrown when an encrypted field cannot be read back: the wrong key, a value
/// this cipher did not write, or a value altered since it was written.
/// </summary>
/// <remarks>
/// The message never carries the ciphertext, the plaintext, or the key. A
/// failure here is reported as "this field could not be decrypted" and nothing
/// more — see <c>docs/data-handling.md</c>, "Logging".
/// </remarks>
public class FieldDecryptionException : Exception
{
    /// <summary>Creates the exception with a default message.</summary>
    public FieldDecryptionException()
        : base("An encrypted field could not be decrypted.")
    {
    }

    /// <summary>Creates the exception with a message that must not contain field content.</summary>
    public FieldDecryptionException(string message)
        : base(message)
    {
    }

    /// <summary>Creates the exception with a message and the underlying cause.</summary>
    public FieldDecryptionException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
