namespace HpacSafety.Core.Features.Reporting;

/// <summary>
///     The only sanctioned way to hand an anonymous visitor a link to a published
///     report's media (ADR-0117).
///     <para>
///         Whether a file is public at all is the <c>public_report_media</c> view's
///         rule, and the caller has already asked it. This is the second check, the
///         one that does not depend on the caller having asked correctly: like
///         <see cref="ReviewerMediaLink" />, it signs only a key in the
///         <see cref="MediaCompartment.Stripped" /> compartment, so an original can
///         never be served to the public whatever a query returned.
///     </para>
/// </summary>
public sealed class PublicMediaLink
{
	private readonly IBlobStore _blobStore;

	/// <summary>Creates the link issuer.</summary>
	public PublicMediaLink(IBlobStore blobStore)
	{
		ArgumentNullException.ThrowIfNull(blobStore);
		_blobStore = blobStore;
	}

	/// <summary>
	///     A short-lived, inline pre-signed GET for a stripped derivative, served
	///     under <paramref name="contentType" />. Throws for any other key. See
	///     REQ-MED-028.
	/// </summary>
	public Task<Uri> CreateUrl(BlobKey derivativeKey,
							   string contentType,
							   TimeSpan lifetime,
							   CancellationToken cancellationToken)
	{
		if (!ReviewerMediaLink.IsViewable(derivativeKey))
		{
			throw new DomainRuleViolationException("The public may only be shown a stripped derivative, never the original upload.");
		}

		return _blobStore.CreateInlineReadUrl(derivativeKey, contentType, lifetime, cancellationToken);
	}
}
