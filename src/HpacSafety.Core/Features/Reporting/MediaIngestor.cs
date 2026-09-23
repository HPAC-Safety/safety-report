using System.Security.Cryptography;

namespace HpacSafety.Core.Features.Reporting;

/// <summary>
///     Judges a reporter's upload, then turns a claimed one into something this
///     system is willing to keep — and, where it can, into something a reviewer may
///     safely be shown.
///     <para>
///         The order is the point. <see cref="Inspect" /> sniffs and validates an upload
///         before it is ever stored, so a refused file never reaches quarantine at all.
///         <see cref="Ingest" /> runs when a submission claims the upload: it judges the
///         bytes again, then <i>promotes</i> them into the report's own compartments
///         under a new name. An upload nobody claims is never promoted, and expires
///         where it landed through a bucket lifecycle rule (ADR-0096).
///     </para>
///     <para>
///         A format this system cannot strip is still promoted, because the original is
///         the private source record regardless, but it produces no derivative and the
///         outcome says so. It fails closed: there is nothing for a reviewer to open,
///         rather than a fall-through to the unstripped original.
///     </para>
///     <para>
///         It lives in <c>Core</c> and depends only on ports, so the rule "a reviewer
///         only ever sees stripped bytes" is provable in a plain unit test with no
///         bucket, no database and no imaging library.
///     </para>
/// </summary>
public sealed class MediaIngestor
{
	// Stream.CopyToAsync's own default. Reading in chunks this size is what
	// keeps the bound below tight rather than "the whole object, minus a
	// rounding error".
	private const int ReadBufferSize = 81920;

	private readonly IBlobStore _blobStore;
	private readonly TimeProvider _clock;
	private readonly MediaPolicy _policy;
	private readonly IMediaSniffer _sniffer;
	private readonly IVideoRemuxer _remuxer;
	private readonly IExifStripper _stripper;

	/// <summary>Creates an ingestor over the ports it needs.</summary>
	public MediaIngestor(
		IBlobStore blobStore,
		IMediaSniffer sniffer,
		IExifStripper stripper,
		IVideoRemuxer remuxer,
		MediaPolicy policy,
		TimeProvider clock)
	{
		ArgumentNullException.ThrowIfNull(blobStore);
		ArgumentNullException.ThrowIfNull(sniffer);
		ArgumentNullException.ThrowIfNull(stripper);
		ArgumentNullException.ThrowIfNull(remuxer);
		ArgumentNullException.ThrowIfNull(policy);
		ArgumentNullException.ThrowIfNull(clock);

		_blobStore = blobStore;
		_sniffer = sniffer;
		_stripper = stripper;
		_remuxer = remuxer;
		_policy = policy;
		_clock = clock;
	}

	/// <summary>
	///     Judges an upload before it is stored: its size, its sniffed format, and
	///     whether that format is what the browser declared.
	/// </summary>
	/// <param name="content">
	///     The whole upload, already bounded by the caller and seekable, positioned
	///     anywhere.
	/// </param>
	/// <param name="declaredContentType">The browser's <c>Content-Type</c> — evidence, never authority.</param>
	/// <param name="cancellationToken">Cancels the sniff.</param>
	public async Task<MediaValidation> Inspect(
		Stream content,
		string? declaredContentType,
		CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(content);

		if (!content.CanSeek)
		{
			throw new ArgumentException("An upload is inspected from a seekable copy.", nameof(content));
		}

		var byteSize = content.Length;
		if (byteSize <= 0)
		{
			return MediaValidation.Rejected(MediaRejectionReason.Empty);
		}

		if (byteSize > _policy.MaxByteSize)
		{
			return MediaValidation.Rejected(MediaRejectionReason.TooLarge);
		}

		content.Position = 0;
		var sniffed = await _sniffer.Sniff(content, cancellationToken).ConfigureAwait(false);

		return _policy.Validate(declaredContentType, sniffed, byteSize);
	}

	/// <summary>
	///     Reads a claimed upload from quarantine, judges it again, and on acceptance
	///     promotes it to <paramref name="reportId" />'s private source record, named by
	///     the report file's own <paramref name="fileId" /> — writing a stripped
	///     derivative alongside when the format allows one.
	/// </summary>
	public async Task<MediaIngestOutcome> Ingest(
		BlobKey quarantineKey,
		TinyId reportId,
		TinyId fileId,
		CancellationToken cancellationToken)
	{
		if (quarantineKey.Compartment is not MediaCompartment.Quarantine)
		{
			throw new DomainRuleViolationException("Ingest reads from quarantine and nowhere else.");
		}

		if (reportId.IsEmpty
			|| fileId.IsEmpty)
		{
			throw new DomainRuleViolationException("A claimed upload is promoted into a report file.");
		}

		// Buffered rather than streamed past this point: the bytes are read three
		// more times - for the digest, for the sniff and for the strip - and
		// MediaPolicy.MaxByteSize is what bounds how much that costs.
		//
		// Getting the bytes into that buffer is a different question, and it is
		// the one a public upload endpoint cannot afford to get wrong: a
		// "download everything, then check Length" copy pulls an arbitrarily
		// large object fully into memory before an oversized upload is refused,
		// which is itself a denial-of-service surface. CopyBounded checks
		// the running total as bytes arrive and stops reading the source the
		// moment the limit is exceeded - the rest of an oversized object is
		// never requested at all.
		using var original = new MemoryStream();
		bool exceedsLimit;

		await using (var source = await _blobStore.OpenRead(quarantineKey, cancellationToken).ConfigureAwait(false))
		{
			exceedsLimit = await CopyBounded(source, original, _policy.MaxByteSize, cancellationToken).ConfigureAwait(false);
		}

		if (exceedsLimit)
		{
			return MediaIngestOutcome.Rejected(MediaRejectionReason.TooLarge);
		}

		var byteSize = original.Length;

		if (byteSize <= 0)
		{
			return MediaIngestOutcome.Rejected(MediaRejectionReason.Empty);
		}

		original.Position = 0;
		var sniffed = await _sniffer.Sniff(original, cancellationToken).ConfigureAwait(false);

		// The declared type was checked against the sniff when the file was
		// uploaded (Inspect). Stored bytes carry no declaration of their own, so
		// the sniff stands in for it here and only the format and size rules can
		// still refuse.
		var verdict = _policy.Validate(sniffed?.ContentType, sniffed, byteSize);
		if (!verdict.IsAccepted)
		{
			return MediaIngestOutcome.Rejected(verdict.RejectionReason);
		}

		var sha256 = Convert.ToHexStringLower(SHA256.HashData(original.GetBuffer().AsSpan(0, (int)byteSize)));

		// The report file's id rather than the upload id: the upload id was a
		// capability the browser held, and a report's keys must not be derivable
		// from it. The row and its bytes then share one identifier (ADR-0097).
		var originalKey = BlobKey.For(reportId.Value, MediaCompartment.Original, fileId.Value);
		original.Position = 0;
		await _blobStore.Write(originalKey, original, verdict.Type.ContentType, cancellationToken).ConfigureAwait(false);

		// Video is remuxed rather than decoded: the packets move into a fresh
		// container and every tag and non-audiovisual track is left behind
		// (ADR-0094). A file that cannot be remuxed into a verified derivative
		// is kept anyway — the reporter does not lose their footage because our
		// toolchain could not clean the container (REQ-MED-015).
		if (verdict.Type.Kind is MediaKind.Video)
		{
			original.Position = 0;
			using var remuxed = new MemoryStream();
			var produced = await _remuxer
				.TryRemux(original, remuxed, verdict.Type, cancellationToken)
				.ConfigureAwait(false);

			if (!produced)
			{
				return MediaIngestOutcome.Retained(verdict.Type, byteSize, sha256, originalKey);
			}

			var remuxedKey = originalKey.In(MediaCompartment.Stripped);
			remuxed.Position = 0;
			await _blobStore
				.Write(remuxedKey, remuxed, verdict.Type.ContentType, cancellationToken)
				.ConfigureAwait(false);

			return MediaIngestOutcome.Ingested(
				verdict.Type, byteSize, sha256, originalKey, remuxedKey, _clock.GetUtcNow());
		}

		if (verdict.Type.StrippedForm is not { } derivativeType)
		{
			// A document is retained and deliberately not viewable: the original
			// is the only record and it is never transformed.
			return MediaIngestOutcome.Retained(verdict.Type, byteSize, sha256, originalKey);
		}

		original.Position = 0;
		using var stripped = new MemoryStream();
		await _stripper.Strip(original, stripped, verdict.Type, cancellationToken).ConfigureAwait(false);

		var derivativeKey = originalKey.In(MediaCompartment.Stripped);
		stripped.Position = 0;
		await _blobStore.Write(derivativeKey, stripped, derivativeType.ContentType, cancellationToken).ConfigureAwait(false);

		return MediaIngestOutcome.Ingested(verdict.Type, byteSize, sha256, originalKey, derivativeKey, _clock.GetUtcNow());
	}

	/// <summary>
	///     Copies <paramref name="source" /> into <paramref name="destination" />,
	///     stopping as soon as more than <paramref name="maxByteSize" /> bytes have
	///     been read rather than after the whole stream has been consumed.
	/// </summary>
	/// <returns><see langword="true" /> when the source exceeded the limit.</returns>
	private static async Task<bool> CopyBounded(
		Stream source,
		MemoryStream destination,
		long maxByteSize,
		CancellationToken cancellationToken)
	{
		var buffer = new byte[ReadBufferSize];
		long total = 0;

		while (true)
		{
			// maxByteSize + 1: a file of exactly the limit must still succeed,
			// and reading one byte past it is enough to know the limit was
			// exceeded without reading a whole extra chunk to find out.
			var toRead = (int)Math.Min(buffer.Length, maxByteSize + 1 - total);

			if (toRead <= 0)
			{
				return true;
			}

			var read = await source.ReadAsync(buffer.AsMemory(0, toRead), cancellationToken).ConfigureAwait(false);

			if (read == 0)
			{
				return false;
			}

			total += read;
			await destination.WriteAsync(buffer.AsMemory(0, read), cancellationToken).ConfigureAwait(false);
		}
	}
}
