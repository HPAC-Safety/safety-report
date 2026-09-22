using System.Collections.Concurrent;

namespace HpacSafety.Core.Tests.Media;

/// <summary>
///     A test double for <see cref="IBlobStore" />. It is deliberately not a
///     production stand-in: it issues opaque URLs that expire, so a test cannot lean
///     on a URL shape the real adapters do not promise.
/// </summary>
internal sealed class InMemoryBlobStore : IBlobStore
{
	private readonly ConcurrentDictionary<string, byte[]> _blobs = new(StringComparer.Ordinal);

	public IReadOnlyCollection<string> Keys => _blobs.Keys.ToArray();

	public Task<Uri> CreateUploadUrl(BlobKey key, string contentType, TimeSpan lifetime, CancellationToken cancellationToken)
	{
		return Task.FromResult(new Uri($"https://example.invalid/{key.Value}?op=put&ttl={BlobUrlLifetime.Validate(lifetime).TotalSeconds}"));
	}

	public Task<Uri> CreateReadUrl(BlobKey key, string downloadFileName, TimeSpan lifetime, CancellationToken cancellationToken)
	{
		return Task.FromResult(new Uri($"https://example.invalid/{key.Value}?op=get&fn={Uri.EscapeDataString(downloadFileName)}&ttl={BlobUrlLifetime.Validate(lifetime).TotalSeconds}"));
	}

	public Task<Stream> OpenRead(BlobKey key, CancellationToken cancellationToken)
	{
		return _blobs.TryGetValue(key.Value, out var content)
			? Task.FromResult<Stream>(new MemoryStream(content, false))
			: throw new KeyNotFoundException(key.Value);
	}

	public async Task Write(BlobKey key, Stream content, string contentType, CancellationToken cancellationToken)
	{
		using var buffer = new MemoryStream();
		await content.CopyToAsync(buffer, cancellationToken);
		_blobs[key.Value] = buffer.ToArray();
	}

	public void Seed(BlobKey key, byte[] content)
	{
		_blobs[key.Value] = content;
	}

	public byte[] Read(BlobKey key)
	{
		return _blobs[key.Value];
	}
}
