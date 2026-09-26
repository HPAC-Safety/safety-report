using System.Collections.Concurrent;
using HpacSafety.Core;

namespace HpacSafety.Acceptance.Tests;

/// <summary>
///     Wraps the booted host's real store and counts what it reads, per key, so a
///     scenario can say how much of an upload the API fetched (REQ-SUB-075).
///     Everything else passes straight through to storage.
/// </summary>
internal sealed class ReadRecordingBlobStore(IBlobStore inner) : IBlobStore
{
	private static readonly ConcurrentDictionary<string, long> RangeBytes = new(StringComparer.Ordinal);
	private static readonly ConcurrentDictionary<string, int> WholeReads = new(StringComparer.Ordinal);

	/// <summary>How many bytes ranged reads of <paramref name="key" /> asked for in all.</summary>
	public static long RangeBytesRead(string key)
	{
		return RangeBytes.GetValueOrDefault(key);
	}

	/// <summary>How many times <paramref name="key" /> was opened whole.</summary>
	public static int WholeReadsOf(string key)
	{
		return WholeReads.GetValueOrDefault(key);
	}

	public Task<Uri> CreateReadUrl(BlobKey key,
								   string downloadFileName,
								   TimeSpan lifetime,
								   CancellationToken cancellationToken)
	{
		return inner.CreateReadUrl(key, downloadFileName, lifetime, cancellationToken);
	}

	public Task<Uri> CreateInlineReadUrl(BlobKey key,
										 string contentType,
										 TimeSpan lifetime,
										 CancellationToken cancellationToken)
	{
		return inner.CreateInlineReadUrl(key, contentType, lifetime, cancellationToken);
	}

	public Task<Uri> CreateUploadUrl(BlobKey key,
									 string contentType,
									 long byteSize,
									 TimeSpan lifetime,
									 CancellationToken cancellationToken)
	{
		return inner.CreateUploadUrl(key, contentType, byteSize, lifetime, cancellationToken);
	}

	public Task<Stream> OpenRead(BlobKey key,
								 CancellationToken cancellationToken)
	{
		WholeReads.AddOrUpdate(key.Value, 1, (_, count) => count + 1);
		return inner.OpenRead(key, cancellationToken);
	}

	public Task<Stream> OpenReadRange(BlobKey key,
									  long offset,
									  long length,
									  CancellationToken cancellationToken)
	{
		RangeBytes.AddOrUpdate(key.Value, length, (_, total) => total + length);
		return inner.OpenReadRange(key, offset, length, cancellationToken);
	}

	public Task Write(BlobKey key,
					  Stream content,
					  string contentType,
					  CancellationToken cancellationToken)
	{
		return inner.Write(key, content, contentType, cancellationToken);
	}

	public Task<StoredBlob?> Describe(BlobKey key,
									  CancellationToken cancellationToken)
	{
		return inner.Describe(key, cancellationToken);
	}

	public Task Copy(BlobKey source,
					 BlobKey destination,
					 CancellationToken cancellationToken)
	{
		return inner.Copy(source, destination, cancellationToken);
	}

	public Task Delete(BlobKey key,
					   CancellationToken cancellationToken)
	{
		return inner.Delete(key, cancellationToken);
	}
}
