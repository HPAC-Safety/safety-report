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
	private readonly ConcurrentDictionary<string, string> _types = new(StringComparer.Ordinal);

	public IReadOnlyCollection<string> Keys => _blobs.Keys.ToArray();

	public Task<Uri> CreateReadUrl(BlobKey key,
								   string downloadFileName,
								   TimeSpan lifetime,
								   CancellationToken cancellationToken)
	{
		return Task.FromResult(new Uri($"https://example.invalid/{key.Value}?op=get&fn={Uri.EscapeDataString(downloadFileName)}&ttl={BlobUrlLifetime.Validate(lifetime).TotalSeconds}"));
	}

	public Task<Uri> CreateInlineReadUrl(BlobKey key,
											 string contentType,
											 TimeSpan lifetime,
											 CancellationToken cancellationToken)
	{
		return Task.FromResult(new Uri($"https://example.invalid/{key.Value}?op=inline&ct={Uri.EscapeDataString(contentType)}&ttl={BlobUrlLifetime.Validate(lifetime).TotalSeconds}"));
	}

	public Task<Uri> CreateUploadUrl(BlobKey key,
								   string contentType,
								   long byteSize,
								   TimeSpan lifetime,
								   CancellationToken cancellationToken)
	{
		if (!key.AcceptsDirectUpload)
		{
			throw new DomainRuleViolationException("Only a key that takes direct uploads may be handed a PUT.");
		}

		return Task.FromResult(new Uri($"https://example.invalid/{key.Value}?op=put&ct={Uri.EscapeDataString(contentType)}&len={byteSize}&ttl={BlobUrlLifetime.Validate(lifetime).TotalSeconds}"));
	}

	/// <summary>Every range any caller asked for, in order.</summary>
	public List<(string Key, long Offset, long Length)> RangesRead { get; } = [];

	public Task<Stream> OpenReadRange(BlobKey key,
									  long offset,
									  long length,
									  CancellationToken cancellationToken)
	{
		var content = _blobs.TryGetValue(key.Value, out var stored) ? stored : throw new KeyNotFoundException(key.Value);
		lock (RangesRead)
		{
			RangesRead.Add((key.Value, offset, length));
		}

		var start = (int)Math.Min(offset, content.Length);
		var count = (int)Math.Min(length, content.Length - start);
		return Task.FromResult<Stream>(new MemoryStream(content, start, count, false));
	}

	public Task<Stream> OpenRead(BlobKey key,
								 CancellationToken cancellationToken)
	{
		return _blobs.TryGetValue(key.Value, out var content)
			? Task.FromResult<Stream>(new MemoryStream(content, false))
			: throw new KeyNotFoundException(key.Value);
	}

	public async Task Write(BlobKey key,
							Stream content,
							string contentType,
							CancellationToken cancellationToken)
	{
		using var buffer = new MemoryStream();
		await content.CopyToAsync(buffer, cancellationToken);
		_blobs[key.Value] = buffer.ToArray();
		_types[key.Value] = contentType;
	}

	public Task<StoredBlob?> Describe(BlobKey key,
									  CancellationToken cancellationToken)
	{
		return Task.FromResult(_blobs.TryGetValue(key.Value, out var bytes)
			? new StoredBlob(_types.GetValueOrDefault(key.Value, "application/octet-stream"), bytes.Length)
			: null);
	}

	public Task Copy(BlobKey source,
					 BlobKey destination,
					 CancellationToken cancellationToken)
	{
		_blobs[destination.Value] = _blobs[source.Value];
		if (_types.TryGetValue(source.Value, out var type))
		{
			_types[destination.Value] = type;
		}

		return Task.CompletedTask;
	}

	public Task Delete(BlobKey key,
					   CancellationToken cancellationToken)
	{
		_blobs.TryRemove(key.Value, out _);
		return Task.CompletedTask;
	}

	public void Seed(BlobKey key,
					 byte[] content,
					 string? contentType = null)
	{
		_blobs[key.Value] = content;
		if (contentType is not null)
		{
			_types[key.Value] = contentType;
		}
	}

	public byte[] Read(BlobKey key)
	{
		return _blobs[key.Value];
	}
}
