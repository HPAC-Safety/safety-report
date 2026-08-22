using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using HpacSafety.Core.SharedKernel;

namespace HpacSafety.Infrastructure.Storage;

/// <summary>
/// The development store: an <b>Adapter</b> over the local filesystem, so a
/// contributor can run the whole upload path without AWS or Docker.
/// <para>
/// It is deliberately not a weaker stand-in. A development double that skips the
/// guarantee the production adapter makes is how a guarantee stops being tested,
/// so this class signs its URLs too: an HMAC over the operation, the key, the
/// content type, and the expiry. Retarget a URL at another key and
/// <see cref="ExecuteUploadAsync" /> refuses it, exactly as S3 answers
/// <c>403</c>. The shared contract suite runs the same tests against both.
/// See ADR-0026 and AGENTS.md — "a development stand-in must never weaken a
/// guarantee the production implementation makes".
/// </para>
/// </summary>
public sealed class FileSystemBlobStore : IBlobStore
{
    /// <summary>The scheme local pre-signed URLs use. It is not http: nothing serves these directly.</summary>
    public const string UrlScheme = "hpac-blob";

    private const string UploadOperation = "put";
    private const string ReadOperation = "get";

    private readonly string _blobRoot;
    private readonly string _metaRoot;
    private readonly byte[] _signingKey;
    private readonly TimeProvider _clock;

    /// <summary>Creates the store, generating a per-process signing key when none is configured.</summary>
    public FileSystemBlobStore(FileSystemBlobStoreOptions options, TimeProvider clock)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(clock);
        ArgumentException.ThrowIfNullOrWhiteSpace(options.RootPath);

        var root = Path.GetFullPath(options.RootPath);
        _blobRoot = Path.Combine(root, "blobs");
        _metaRoot = Path.Combine(root, "meta");
        _signingKey = options.SigningKey is { Length: > 0 } configured
            ? Convert.FromBase64String(configured)
            : RandomNumberGenerator.GetBytes(32);
        _clock = clock;

        Directory.CreateDirectory(_blobRoot);
        Directory.CreateDirectory(_metaRoot);
    }

    /// <inheritdoc />
    public Task<Uri> CreateUploadUrlAsync(BlobKey key, string contentType, TimeSpan lifetime, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(contentType);
        return Task.FromResult(Sign(UploadOperation, key, contentType, lifetime));
    }

    /// <inheritdoc />
    public Task<Uri> CreateReadUrlAsync(BlobKey key, TimeSpan lifetime, CancellationToken cancellationToken) =>
        Task.FromResult(Sign(ReadOperation, key, string.Empty, lifetime));

    /// <inheritdoc />
    public Task<Stream> OpenReadAsync(BlobKey key, CancellationToken cancellationToken)
    {
        var path = PathFor(_blobRoot, key);

        return File.Exists(path)
            ? Task.FromResult<Stream>(File.OpenRead(path))
            // The key is not passed as FileNotFoundException.FileName: that
            // property is appended to Message and ToString, which would put a
            // report identifier into any log that catches this.
            : throw new FileNotFoundException("No blob is stored under that key.");
    }

    /// <inheritdoc />
    public async Task WriteAsync(BlobKey key, Stream content, string contentType, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(content);
        ArgumentException.ThrowIfNullOrWhiteSpace(contentType);

        var path = PathFor(_blobRoot, key);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);

        await using (var file = File.Create(path))
        {
            await content.CopyToAsync(file, cancellationToken).ConfigureAwait(false);
        }

        var metaPath = PathFor(_metaRoot, key);
        Directory.CreateDirectory(Path.GetDirectoryName(metaPath)!);
        await File.WriteAllTextAsync(metaPath, contentType, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Performs the upload a signed URL authorises. This is what the local
    /// development endpoint calls in place of S3 accepting a PUT; a URL signed
    /// for another key, another operation, or an expired moment is refused.
    /// </summary>
    public async Task ExecuteUploadAsync(Uri signedUrl, Stream content, CancellationToken cancellationToken)
    {
        var ticket = Verify(signedUrl, UploadOperation);
        await WriteAsync(ticket.Key, content, ticket.ContentType, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Serves the bytes a signed GET authorises, and only those bytes.</summary>
    public async Task<Stream> ExecuteReadAsync(Uri signedUrl, CancellationToken cancellationToken)
    {
        var ticket = Verify(signedUrl, ReadOperation);
        return await OpenReadAsync(ticket.Key, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>The content type recorded when a blob was written.</summary>
    public async Task<string?> ReadContentTypeAsync(BlobKey key, CancellationToken cancellationToken)
    {
        var path = PathFor(_metaRoot, key);

        return File.Exists(path)
            ? await File.ReadAllTextAsync(path, cancellationToken).ConfigureAwait(false)
            : null;
    }

    private Uri Sign(string operation, BlobKey key, string contentType, TimeSpan lifetime)
    {
        var expiresAt = _clock.GetUtcNow().Add(BlobUrlLifetime.Validate(lifetime)).ToUnixTimeSeconds();
        var signature = Signature(operation, key.Value, contentType, expiresAt);

        var query = string.Create(
            CultureInfo.InvariantCulture,
            $"?op={operation}&ct={Uri.EscapeDataString(contentType)}&expires={expiresAt}&sig={signature}");

        return new Uri($"{UrlScheme}://local/{key.Value}{query}");
    }

    private SignedTicket Verify(Uri signedUrl, string expectedOperation)
    {
        ArgumentNullException.ThrowIfNull(signedUrl);

        if (!string.Equals(signedUrl.Scheme, UrlScheme, StringComparison.Ordinal))
        {
            throw new PresignedUrlRejectedException();
        }

        var query = ParseQuery(signedUrl.Query);

        if (!query.TryGetValue("op", out var operation)
            || !query.TryGetValue("expires", out var expires)
            || !query.TryGetValue("sig", out var presented))
        {
            throw new PresignedUrlRejectedException();
        }

        query.TryGetValue("ct", out var contentType);
        contentType ??= string.Empty;

        if (!string.Equals(operation, expectedOperation, StringComparison.Ordinal)
            || !long.TryParse(expires, NumberStyles.Integer, CultureInfo.InvariantCulture, out var expiresAt))
        {
            throw new PresignedUrlRejectedException();
        }

        if (!BlobKey.TryParse(Uri.UnescapeDataString(signedUrl.AbsolutePath.TrimStart('/')), out var key))
        {
            throw new PresignedUrlRejectedException();
        }

        var expected = Signature(operation, key.Value, contentType, expiresAt);

        // Fixed-time comparison: a signature check that leaks its progress through
        // timing is a signature check an attacker can walk.
        if (!CryptographicOperations.FixedTimeEquals(
                Encoding.ASCII.GetBytes(expected),
                Encoding.ASCII.GetBytes(presented)))
        {
            throw new PresignedUrlRejectedException();
        }

        if (_clock.GetUtcNow().ToUnixTimeSeconds() > expiresAt)
        {
            throw new PresignedUrlRejectedException("The pre-signed URL has expired.");
        }

        return new SignedTicket(key, contentType);
    }

    private string Signature(string operation, string key, string contentType, long expiresAt)
    {
        var payload = string.Create(CultureInfo.InvariantCulture, $"{operation}\n{key}\n{contentType}\n{expiresAt}");
        var mac = HMACSHA256.HashData(_signingKey, Encoding.UTF8.GetBytes(payload));
        return Convert.ToHexStringLower(mac);
    }

    private static Dictionary<string, string> ParseQuery(string query)
    {
        var parsed = new Dictionary<string, string>(StringComparer.Ordinal);

        foreach (var pair in query.TrimStart('?').Split('&', StringSplitOptions.RemoveEmptyEntries))
        {
            var separator = pair.IndexOf('=', StringComparison.Ordinal);
            if (separator > 0)
            {
                parsed[pair[..separator]] = Uri.UnescapeDataString(pair[(separator + 1)..]);
            }
        }

        return parsed;
    }

    private static string PathFor(string root, BlobKey key)
    {
        // BlobKey has already refused traversal, but the check is repeated here
        // because this is the one place where getting it wrong writes outside the
        // store. Two cheap checks beat one clever one.
        var path = Path.GetFullPath(Path.Combine(root, key.Value.Replace('/', Path.DirectorySeparatorChar)));

        return path.StartsWith(root + Path.DirectorySeparatorChar, StringComparison.Ordinal)
            ? path
            : throw new PresignedUrlRejectedException("The key escapes the blob root.");
    }

    private readonly record struct SignedTicket(BlobKey Key, string ContentType);
}
