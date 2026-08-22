namespace HpacSafety.Core.SharedKernel;

/// <summary>
/// Server-side verification of a Cloudflare Turnstile token. A token the browser
/// says is fine is not a verification.
/// </summary>
public interface ITurnstileVerifier
{
    /// <summary>Verifies a token with the issuer.</summary>
    Task<bool> VerifyAsync(string token, string? remoteIpAddress, CancellationToken cancellationToken);
}
