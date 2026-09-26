namespace HpacSafety.Core;

/// <summary>
///     The name of one object in private storage, in one of exactly three shapes:
///     <code>
/// quarantine/&lt;upload id&gt;              an unclaimed upload, expired by lifecycle rule
/// &lt;report id&gt;/original/&lt;file&gt;     the private source record
/// &lt;report id&gt;/stripped/&lt;file&gt;     what a reviewer is shown
/// </code>
///     <para>
///         All of a report's media lives in a directory named with that report's id, so
///         everything belonging to one report is a single literal prefix. Quarantine is
///         the deliberate exception: an upload waits there before any report exists,
///         named only by its <see cref="Core.UploadId" />, and it sits at the top level
///         because an S3 lifecycle filter matches a literal prefix and cannot express
///         <c>*/quarantine/</c>. See ADR-0026 and ADR-0096.
///     </para>
///     <para>
///         A value object rather than a <c>string</c> for two reasons. A key is
///         attacker-influenced — it reaches a bucket in production and a directory in
///         development, and <c>../</c> means something very different in the second. And
///         the layout is a rule, not a convention: parsing is the only way to make a
///         key, so one that is not namespaced by a report id is unrepresentable rather
///         than merely discouraged.
///     </para>
/// </summary>
public readonly record struct BlobKey
{
	/// <summary>The path segment quarantined uploads live under.</summary>
	public const string QuarantineSegment = "quarantine";

	/// <summary>The path segment the private original lives under.</summary>
	public const string OriginalSegment = "original";

	/// <summary>The path segment the stripped derivative lives under.</summary>
	public const string StrippedSegment = "stripped";

	/// <summary>
	///     Length of a report id. Identifiers in this system are "tiny ids": 11
	///     characters of <c>A-Za-z0-9-_</c>, cryptographically random, encoding no
	///     timestamp.
	/// </summary>
	public const int ReportIdLength = 11;

	/// <summary>The longest file-name segment a key may carry.</summary>
	public const int MaxFileNameLength = 128;

	private BlobKey(string? reportId,
					MediaCompartment compartment,
					string fileName)
	{
		ReportId = reportId;
		Compartment = compartment;
		FileName = fileName;
	}

	/// <summary>
	///     The report every byte under this key belongs to, or <see langword="null" />
	///     for an unclaimed upload in quarantine, which belongs to no report yet.
	/// </summary>
	public string? ReportId { get; }

	/// <summary>Which compartment the object lives in.</summary>
	public MediaCompartment Compartment { get; }

	/// <summary>The final path segment. For a quarantined upload, its upload id.</summary>
	public string FileName { get; }

	/// <summary>
	///     Whether a browser may be handed a pre-signed PUT to this key. Only a
	///     quarantined upload, named by nothing but its minted upload id, may be
	///     (ADR-0126): a report's own compartments are written by this system alone.
	///     A compartment that one day takes direct uploads is added here, in the one
	///     place every adapter asks.
	/// </summary>
	public bool AcceptsDirectUpload => Compartment == MediaCompartment.Quarantine;

	/// <summary>The key as stored.</summary>
	public string Value =>
		Compartment == MediaCompartment.Quarantine
			? $"{QuarantineSegment}/{FileName}"
			: $"{ReportId}/{SegmentFor(Compartment)}/{FileName}";

	/// <summary>The quarantine key an unclaimed upload waits under.</summary>
	public static BlobKey ForUpload(UploadId uploadId)
	{
		if (uploadId.Value.Length != UploadId.Length)
		{
			throw new DomainRuleViolationException("A quarantine key needs a minted upload id.");
		}

		return new BlobKey(null, MediaCompartment.Quarantine, uploadId.Value);
	}

	/// <summary>Builds a key for one report's media in its original or stripped compartment.</summary>
	public static BlobKey For(string reportId,
							  MediaCompartment compartment,
							  string fileName)
	{
		ArgumentNullException.ThrowIfNull(reportId);
		ArgumentNullException.ThrowIfNull(fileName);

		if (compartment == MediaCompartment.Quarantine)
		{
			throw new DomainRuleViolationException("A quarantine key is named by an upload id, not a report.");
		}

		if (!IsReportId(reportId))
		{
			// Not echoed: a report id identifies a real report, and this message
			// may end up in a log. docs/data-handling.md — log identifiers only
			// where they belong, never by accident.
			throw new DomainRuleViolationException("A blob key must be namespaced by a well-formed report id.");
		}

		if (!IsFileName(fileName))
		{
			throw new DomainRuleViolationException("The value is not a valid blob file name.");
		}

		if (!Enum.IsDefined(compartment))
		{
			throw new DomainRuleViolationException("The value is not a known media compartment.");
		}

		return new BlobKey(reportId, compartment, fileName);
	}

	/// <summary>Parses a stored key, throwing when it is not one of the three shapes.</summary>
	public static BlobKey Parse(string? candidate)
	{
		return TryParse(candidate, out var key)
			? key
			// The candidate is deliberately not echoed. It is client-influenced,
			// unbounded, may contain control characters, and encodes a report
			// identifier - none of which belongs in an exception that something
			// downstream will log.
			: throw new DomainRuleViolationException("The value is not a valid blob key.");
	}

	/// <summary>Parses a stored key without throwing.</summary>
	public static bool TryParse(string? candidate,
								out BlobKey key)
	{
		key = default;

		if (string.IsNullOrEmpty(candidate))
		{
			return false;
		}

		var segments = candidate.Split('/');

		if (segments.Length == 2
			&& string.Equals(segments[0], QuarantineSegment, StringComparison.Ordinal))
		{
			if (!UploadId.TryParse(segments[1], out var uploadId))
			{
				return false;
			}

			key = ForUpload(uploadId);
			return true;
		}

		if (segments.Length != 3)
		{
			return false;
		}

		var compartment = segments[1] switch
		{
			OriginalSegment => (MediaCompartment?)MediaCompartment.Original,
			StrippedSegment => MediaCompartment.Stripped,
			_ => null,
		};

		return compartment is { } known && TryBuild(segments[0], known, segments[2], out key);
	}

	/// <summary>The same report's same file, in another compartment.</summary>
	public BlobKey In(MediaCompartment compartment)
	{
		if (ReportId is not { } reportId)
		{
			throw new DomainRuleViolationException("An unclaimed upload belongs to no report and has no other compartment.");
		}

		return For(reportId, compartment, FileName);
	}

	/// <inheritdoc />
	public override string ToString()
	{
		return Value;
	}

	private static bool TryBuild(string? reportId,
								 MediaCompartment compartment,
								 string fileName,
								 out BlobKey key)
	{
		key = default;

		if (!IsReportId(reportId)
			|| !IsFileName(fileName))
		{
			return false;
		}

		key = new BlobKey(reportId, compartment, fileName);
		return true;
	}

	private static string SegmentFor(MediaCompartment compartment)
	{
		// Only a report's compartments reach here: For refuses quarantine and
		// any undefined value, and Value writes a quarantine key itself.
		return compartment == MediaCompartment.Original ? OriginalSegment : StrippedSegment;
	}

	// TEMPORARY: the shape is duplicated here only because the shared TinyId
	// value object does not exist on this branch yet. Switch this to TinyId when
	// #62 lands - two implementations of one format is how they drift apart.
	private static bool IsReportId(string? candidate)
	{
		if (candidate is not { Length: ReportIdLength })
		{
			return false;
		}

		foreach (var character in candidate)
		{
			var allowed = character is >= 'a' and <= 'z' or >= 'A' and <= 'Z' or >= '0' and <= '9'
				or '-' or '_';

			if (!allowed)
			{
				return false;
			}
		}

		return true;
	}

	private static bool IsFileName(string? candidate)
	{
		// A leading dot would make a hidden object in a listing, and "." and ".."
		// are path traversal to anything that ever maps a key onto a directory.
		if (candidate is not { Length: > 0 }
			|| candidate.Length > MaxFileNameLength
			|| candidate[0] == '.')
		{
			return false;
		}

		foreach (var character in candidate)
		{
			var allowed = character is >= 'a' and <= 'z' or >= 'A' and <= 'Z' or >= '0' and <= '9'
				or '-' or '_' or '.';

			if (!allowed)
			{
				return false;
			}
		}

		return true;
	}
}
