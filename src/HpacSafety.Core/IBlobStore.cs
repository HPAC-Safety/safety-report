namespace HpacSafety.Core;

/// <summary>
///     Private object storage for uploaded media. There are no public object URLs,
///     ever — a reviewer sees a short-lived pre-signed GET. See
///     docs/data-handling.md.
///     <para>
///         Two rules bind every implementation, and both are covered by the shared
///         contract suite in <c>HpacSafety.Infrastructure.Tests</c>, run against
///         an S3-compatible server: a URL is scoped to exactly one <see cref="BlobKey" /> and cannot be reused
///         for another, and every lifetime passes <see cref="BlobUrlLifetime.Validate" />.
///     </para>
/// </summary>
public interface IBlobStore
{
	/// <summary>
	///     A short-lived URL an administrator may GET one file from, and only that
	///     one key. <paramref name="downloadFileName" /> is a server-minted display
	///     name — never a client-supplied one — that the URL forces the browser to
	///     download as, rather than render inline.
	/// </summary>
	Task<Uri> CreateReadUrl(BlobKey key,
							string downloadFileName,
							TimeSpan lifetime,
							CancellationToken cancellationToken);

	/// <summary>Opens stored bytes for server-side work such as EXIF stripping.</summary>
	Task<Stream> OpenRead(BlobKey key,
						  CancellationToken cancellationToken);

	/// <summary>Writes bytes, such as the EXIF-stripped derivative.</summary>
	Task Write(BlobKey key,
			   Stream content,
			   string contentType,
			   CancellationToken cancellationToken);

	/// <summary>
	///     The stored type and size of the object under <paramref name="key" />, or
	///     <see langword="null" /> when nothing is stored there.
	/// </summary>
	Task<StoredBlob?> Describe(BlobKey key,
							   CancellationToken cancellationToken);

	/// <summary>
	///     Copies an object, unchanged and inside storage, to another key. No bytes
	///     pass through this process (ADR-0098).
	/// </summary>
	Task Copy(BlobKey source,
			  BlobKey destination,
			  CancellationToken cancellationToken);

	/// <summary>
	///     Erases every stored version of <paramref name="key" />, so that on a
	///     versioned bucket the bytes are gone at once rather than left behind a
	///     delete marker. Deleting a key that holds nothing succeeds. Only an
	///     unclaimed upload in quarantine may be erased — a report's own media is
	///     never physically deleted (AGENTS.md invariant 8, ADR-0096).
	/// </summary>
	Task Delete(BlobKey key,
				CancellationToken cancellationToken);
}
