namespace HpacSafety.Core.Features.PrivateAttachments;

/// <summary>
///     The only sanctioned way to hand a safety officer or administrator a link to
///     a private attachment (ADR-0135), and the only place that signs a URL for the
///     private compartment at all.
///     <para>
///         The mirror of <see cref="Reporting.ReviewerMediaLink" /> and
///         <see cref="Reporting.PublicMediaLink" />: each of those signs only its own
///         compartment, so neither can ever sign a private key, and this signs only
///         <see cref="MediaCompartment.Private" />, so it can never be pointed at a
///         reporter's original or an unclaimed upload.
///     </para>
/// </summary>
public sealed class PrivateAttachmentLink
{
	private readonly IBlobStore _blobStore;

	/// <summary>Creates the link issuer.</summary>
	public PrivateAttachmentLink(IBlobStore blobStore)
	{
		ArgumentNullException.ThrowIfNull(blobStore);
		_blobStore = blobStore;
	}

	/// <summary>
	///     A short-lived, forced-download pre-signed GET for one private attachment,
	///     saved as <paramref name="downloadFileName" />. Throws for any key outside
	///     the private compartment.
	/// </summary>
	public Task<Uri> CreateDownloadUrl(BlobKey key,
									   string downloadFileName,
									   TimeSpan lifetime,
									   CancellationToken cancellationToken)
	{
		if (key.Compartment is not MediaCompartment.Private)
		{
			throw new DomainRuleViolationException("A private attachment link may only name a key in the private compartment.");
		}

		return _blobStore.CreateReadUrl(key, downloadFileName, BlobUrlLifetime.Validate(lifetime), cancellationToken);
	}
}
