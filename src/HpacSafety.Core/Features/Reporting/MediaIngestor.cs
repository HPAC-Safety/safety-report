using System.Security.Cryptography;

namespace HpacSafety.Core.Features.Reporting;

/// <summary>
///     Judges a reporter's upload, then turns a claimed one into something this
///     system is willing to keep — and, where it can, into something a reviewer may
///     safely be shown.
///     <para>
///         The order is the point. A browser sends an upload straight to quarantine,
///         and <see cref="Inspect" /> sniffs and validates it when a submission claims
///         it, reading only what sniffing needs, so a refused file never reaches a
///         report. The submission copies an accepted upload into the report's original
///         compartment without decoding it, and <see cref="Process" /> runs later, in
///         the Worker: it judges the original again and writes the stripped derivative
///         beside it (ADR-0098, ADR-0126).
///     </para>
///     <para>
///         A format this system cannot strip is still kept, because the original is
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
	///     Judges an upload a submission claims: its size, its sniffed format, and
	///     whether that format is what the browser declared. The size is held to the
	///     limit of the kind the bytes really are.
	/// </summary>
	/// <param name="content">
	///     The whole upload, seekable, positioned anywhere. Only what sniffing asks for
	///     is read, so a stream that fetches on demand (<see cref="BlobRangeStream" />)
	///     never pulls the whole file.
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

		if (byteSize > _policy.Limits.Largest)
		{
			return MediaValidation.Rejected(MediaRejectionReason.TooLarge);
		}

		content.Position = 0;
		var sniffed = await _sniffer.Sniff(content, cancellationToken).ConfigureAwait(false);

		return _policy.Validate(declaredContentType, sniffed, byteSize);
	}

	/// <summary>
	///     Reads a report's stored original, judges it again, and — where its format
	///     allows one — writes the stripped derivative beside it. Run by the Worker,
	///     once per attachment, never on the submission path (ADR-0098).
	/// </summary>
	/// <param name="originalKey">The original, in the report's original compartment.</param>
	/// <param name="recorded">The type the file was recorded as when it was claimed.</param>
	/// <param name="cancellationToken">Cancels the work.</param>
	/// <remarks>
	///     Idempotent: the derivative's key is derived from the original's, so a
	///     second run overwrites rather than adds.
	/// </remarks>
	public async Task<MediaIngestOutcome> Process(
		BlobKey originalKey,
		MediaType recorded,
		CancellationToken cancellationToken)
	{
		if (originalKey.Compartment is not MediaCompartment.Original)
		{
			throw new DomainRuleViolationException("Processing reads a report's original and nothing else.");
		}

		// Spooled to a temporary file, not memory: the bytes are read several
		// times - to sniff, to strip or remux - and a 250 MB video must not become a
		// 250 MB buffer (#362, REQ-MED-024). They are hashed on the way in, in the
		// same bounded chunks, and CopyBounded stops the moment the limit is
		// exceeded, so an oversized object is never read in full.
		await using var original = TemporaryFile.Create();
		using var digest = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
		bool exceedsLimit;

		await using (var source = await _blobStore.OpenRead(originalKey, cancellationToken).ConfigureAwait(false))
		{
			exceedsLimit = await CopyBounded(source, original, _policy.Limits.Largest, cancellationToken, digest)
				.ConfigureAwait(false);
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

		// The recorded type stands in for a declaration: it is what the upload
		// was validated as, so bytes that no longer sniff as it are refused.
		var verdict = _policy.Validate(recorded.ContentType, sniffed, byteSize);
		if (!verdict.IsAccepted)
		{
			return MediaIngestOutcome.Rejected(verdict.RejectionReason);
		}

		var sha256 = Convert.ToHexStringLower(digest.GetHashAndReset());
		var derivativeKey = originalKey.In(MediaCompartment.Stripped);

		// Video is remuxed rather than decoded: the packets move into a fresh
		// container and every tag and non-audiovisual track is left behind
		// (ADR-0094). A file that cannot be remuxed into a verified derivative
		// is kept anyway — the reporter does not lose their footage because our
		// toolchain could not clean the container (REQ-MED-015).
		if (verdict.Type.Kind is MediaKind.Video)
		{
			original.Position = 0;
			await using var remuxed = TemporaryFile.Create();
			var produced = await _remuxer
				.TryRemux(original, remuxed, verdict.Type, cancellationToken)
				.ConfigureAwait(false);

			if (!produced)
			{
				return MediaIngestOutcome.Retained(verdict.Type, byteSize, sha256, originalKey);
			}

			// Always an MP4, whatever container the video arrived in (ADR-0122).
			remuxed.Position = 0;
			await _blobStore
				.Write(derivativeKey, remuxed, MediaType.Mp4.ContentType, cancellationToken)
				.ConfigureAwait(false);

			return MediaIngestOutcome.Ingested(
				verdict.Type, byteSize, sha256, originalKey, derivativeKey, _clock.GetUtcNow());
		}

		if (verdict.Type.StrippedForm is not { } derivativeType)
		{
			// A document is retained and deliberately not viewable: the original
			// is the only record and it is never transformed.
			return MediaIngestOutcome.Retained(verdict.Type, byteSize, sha256, originalKey);
		}

		original.Position = 0;
		await using var stripped = TemporaryFile.Create();

		try
		{
			await _stripper.Strip(original, stripped, verdict.Type, cancellationToken).ConfigureAwait(false);
		}
#pragma warning disable CA1031 // Any imaging failure on these bytes is the same outcome: no clean derivative exists.
		catch (Exception cause) when (cause is not OperationCanceledException)
#pragma warning restore CA1031
		{
			// Retrying would decode the same bytes the same way. Failing closed
			// here records it once, and the file stays unviewable (REQ-MED-013).
			return MediaIngestOutcome.Rejected(MediaRejectionReason.CouldNotStrip);
		}

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
		Stream destination,
		long maxByteSize,
		CancellationToken cancellationToken,
		IncrementalHash? digest = null)
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
			digest?.AppendData(buffer, 0, read);
			await destination.WriteAsync(buffer.AsMemory(0, read), cancellationToken).ConfigureAwait(false);
		}
	}
}
