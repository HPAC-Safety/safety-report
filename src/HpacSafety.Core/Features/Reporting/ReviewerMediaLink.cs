namespace HpacSafety.Core.Features.Reporting;

/// <summary>
///     The only sanctioned way to hand a safety officer a link to uploaded media.
///     <para>
///         <see cref="IBlobStore.CreateReadUrl" /> will mint a URL for any key it is
///         given — the quarantined upload and the private original included. It is
///         generic storage and knows nothing about which bytes are safe to look at. This
///         does: only <see cref="MediaCompartment.Stripped" /> is ever issued.
///     </para>
///     <para>
///         The check is on the compartment, which is a parsed property of
///         <see cref="BlobKey" /> rather than a substring of it. That matters: the
///         layout puts the report id first, so a prefix match would have had to move
///         when the layout did, and a check that silently passes a differently-shaped
///         key is worse than one that fails. A video has no stripped compartment at all
///         until #65, so it is refused here by the same rule rather than a special case.
///         See docs/data-handling.md and ADR-0026.
///     </para>
/// </summary>
public sealed class ReviewerMediaLink
{
	private readonly IBlobStore _blobStore;

	/// <summary>Creates the link issuer.</summary>
	public ReviewerMediaLink(IBlobStore blobStore)
	{
		ArgumentNullException.ThrowIfNull(blobStore);
		_blobStore = blobStore;
	}

	/// <summary>
	///     A short-lived, forced-download pre-signed GET for a stripped derivative.
	///     Throws for anything else, including the original and the quarantined
	///     upload. See REQ-MED-010.
	/// </summary>
	public Task<Uri> CreateViewUrl(BlobKey key,
								   string downloadFileName,
								   TimeSpan lifetime,
								   CancellationToken cancellationToken)
	{
		if (!IsViewable(key))
		{
			throw new DomainRuleViolationException(
				"A reviewer may only be shown a stripped derivative, never the original upload.");
		}

		return _blobStore.CreateReadUrl(key, downloadFileName, lifetime, cancellationToken);
	}

	/// <summary>
	///     A short-lived, inline pre-signed GET for a stripped derivative, served
	///     under <paramref name="contentType" /> so the lightbox can embed it. Throws
	///     for anything else. See REQ-MED-010 and issue #427 decision 10.
	/// </summary>
	public Task<Uri> CreateInlineViewUrl(BlobKey key,
										 string contentType,
										 TimeSpan lifetime,
										 CancellationToken cancellationToken)
	{
		if (!IsViewable(key))
		{
			throw new DomainRuleViolationException(
				"A reviewer may only be shown a stripped derivative, never the original upload.");
		}

		return _blobStore.CreateInlineReadUrl(key, contentType, lifetime, cancellationToken);
	}

	/// <summary>
	///     A short-lived, forced-download pre-signed GET for the raw original of an
	///     image or video that has no stripped derivative yet — still processing, or
	///     failed. Throws for a document (use <see cref="CreateDocumentDownloadUrl" />),
	///     for anything not in the original compartment, and — enforced here, the
	///     privacy chokepoint, not merely expected of the caller (ADR-0026) — once
	///     <paramref name="hasDerivative" /> is true: use
	///     <see cref="CreateInlineViewUrl" /> instead. See issue #427 decision 15,
	///     which widens ADR-0094 from a failed video to any image or video with no
	///     derivative.
	/// </summary>
	public Task<Uri> CreateOriginalMediaDownloadUrl(BlobKey originalKey,
													AttachmentKind kind,
													bool hasDerivative,
													string downloadFileName,
													TimeSpan lifetime,
													CancellationToken cancellationToken)
	{
		if (kind is not (AttachmentKind.Image or AttachmentKind.Video))
		{
			throw new DomainRuleViolationException(
				"Only an image or video's raw original may be downloaded this way; a document downloads through the document endpoint.");
		}

		if (hasDerivative)
		{
			throw new DomainRuleViolationException(
				"The raw original is offered only while there is no derivative to view instead.");
		}

		if (originalKey.Compartment is not MediaCompartment.Original)
		{
			throw new DomainRuleViolationException("A raw-original download must reference the original compartment.");
		}

		return _blobStore.CreateReadUrl(originalKey, downloadFileName, lifetime, cancellationToken);
	}

	/// <summary>
	///     A short-lived, forced-download pre-signed GET for a document's validated
	///     private original. Throws for anything that is not a document's original —
	///     an image or video original is never issued this way, because only a
	///     document has no stripped derivative to redirect a reviewer to instead. See
	///     REQ-MED-011.
	/// </summary>
	public Task<Uri> CreateDocumentDownloadUrl(BlobKey originalKey,
											   AttachmentKind kind,
											   string downloadFileName,
											   TimeSpan lifetime,
											   CancellationToken cancellationToken)
	{
		if (kind is not AttachmentKind.Document)
		{
			throw new DomainRuleViolationException(
				"Only a document's original may be downloaded directly; an image or video original is never exposed.");
		}

		if (originalKey.Compartment is not MediaCompartment.Original)
		{
			throw new DomainRuleViolationException("A document download must reference its private original.");
		}

		return _blobStore.CreateReadUrl(originalKey, downloadFileName, lifetime, cancellationToken);
	}

	/// <summary>True when the key names a stripped derivative rather than an original or a quarantined upload.</summary>
	public static bool IsViewable(BlobKey key)
	{
		return key.Compartment is MediaCompartment.Stripped;
	}
}
