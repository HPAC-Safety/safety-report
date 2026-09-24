namespace HpacSafety.Core.Tests.Media;

/// <summary>
///     A test double that hands back one specific stream for whatever key ingest
///     asks to read — used only to inject <see cref="SyntheticOversizedStream" />,
///     which <see cref="InMemoryBlobStore" /> cannot do because it stores content as
///     a materialised <c>byte[]</c>.
/// </summary>
internal sealed class SingleStreamBlobStore(Stream source) : IBlobStore
{
	public Task<Uri> CreateReadUrl(BlobKey key,
								   string downloadFileName,
								   TimeSpan lifetime,
								   CancellationToken cancellationToken)
	{
		throw new NotSupportedException();
	}

	public Task<Uri> CreateInlineReadUrl(BlobKey key,
											 string contentType,
											 TimeSpan lifetime,
											 CancellationToken cancellationToken)
	{
		throw new NotSupportedException();
	}

	public Task<Stream> OpenRead(BlobKey key,
								 CancellationToken cancellationToken)
	{
		return Task.FromResult(source);
	}

	public Task Write(BlobKey key,
					  Stream content,
					  string contentType,
					  CancellationToken cancellationToken)
	{
		throw new InvalidOperationException("An oversized, rejected upload must never reach a write.");
	}

	public Task<StoredBlob?> Describe(BlobKey key,
									  CancellationToken cancellationToken)
	{
		throw new NotSupportedException();
	}

	public Task Copy(BlobKey source,
					 BlobKey destination,
					 CancellationToken cancellationToken)
	{
		throw new NotSupportedException();
	}

	public Task Delete(BlobKey key,
					   CancellationToken cancellationToken)
	{
		throw new NotSupportedException();
	}
}
