namespace HpacSafety.Core.SharedKernel;

/// <summary>
/// The name of one object in private storage, in one of exactly three shapes:
/// <code>
/// quarantine/&lt;report id&gt;/&lt;file&gt;   unverified, expired by lifecycle rule
/// &lt;report id&gt;/original/&lt;file&gt;     the private source record
/// &lt;report id&gt;/stripped/&lt;file&gt;     what a reviewer is shown
/// </code>
/// <para>
/// All of a report's media lives in a directory named with that report's id, so
/// everything belonging to one report is a single literal prefix. Quarantine is
/// the deliberate exception and sits at the top level, because an S3 lifecycle
/// filter matches a literal prefix and cannot express <c>*/quarantine/</c>. The
/// report id is still the next segment, so per-report enumeration stays one
/// prefix either way. See ADR-0026.
/// </para>
/// <para>
/// A value object rather than a <c>string</c> for two reasons. A key is
/// attacker-influenced — it reaches a bucket in production and a directory in
/// development, and <c>../</c> means something very different in the second. And
/// the layout is a rule, not a convention: parsing is the only way to make a
/// key, so one that is not namespaced by a report id is unrepresentable rather
/// than merely discouraged.
/// </para>
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
    /// Length of a report id. Identifiers in this system are "tiny ids": 11
    /// characters of <c>A-Za-z0-9-_</c>, cryptographically random, encoding no
    /// timestamp.
    /// </summary>
    public const int ReportIdLength = 11;

    /// <summary>The longest file-name segment a key may carry.</summary>
    public const int MaxFileNameLength = 128;

    private BlobKey(string reportId, MediaCompartment compartment, string fileName)
    {
        ReportId = reportId;
        Compartment = compartment;
        FileName = fileName;
    }

    /// <summary>The report every byte under this key belongs to.</summary>
    public string ReportId { get; }

    /// <summary>Which compartment the object lives in.</summary>
    public MediaCompartment Compartment { get; }

    /// <summary>The final path segment.</summary>
    public string FileName { get; }

    /// <summary>The key as stored.</summary>
    public string Value =>
        Compartment == MediaCompartment.Quarantine
            ? $"{QuarantineSegment}/{ReportId}/{FileName}"
            : $"{ReportId}/{SegmentFor(Compartment)}/{FileName}";

    /// <summary>Builds a key for one report's media in one compartment.</summary>
    public static BlobKey For(string reportId, MediaCompartment compartment, string fileName)
    {
        ArgumentNullException.ThrowIfNull(reportId);
        ArgumentNullException.ThrowIfNull(fileName);

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
    public static BlobKey Parse(string? candidate) =>
        TryParse(candidate, out var key)
            ? key
            // The candidate is deliberately not echoed. It is client-influenced,
            // unbounded, may contain control characters, and encodes a report
            // identifier - none of which belongs in an exception that something
            // downstream will log.
            : throw new DomainRuleViolationException("The value is not a valid blob key.");

    /// <summary>Parses a stored key without throwing.</summary>
    public static bool TryParse(string? candidate, out BlobKey key)
    {
        key = default;

        if (string.IsNullOrEmpty(candidate))
        {
            return false;
        }

        var segments = candidate.Split('/');
        if (segments.Length != 3)
        {
            return false;
        }

        if (string.Equals(segments[0], QuarantineSegment, StringComparison.Ordinal))
        {
            return TryBuild(segments[1], MediaCompartment.Quarantine, segments[2], out key);
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
    public BlobKey In(MediaCompartment compartment) => For(ReportId, compartment, FileName);

    /// <inheritdoc />
    public override string ToString() => Value;

    private static bool TryBuild(string reportId, MediaCompartment compartment, string fileName, out BlobKey key)
    {
        key = default;

        if (!IsReportId(reportId) || !IsFileName(fileName))
        {
            return false;
        }

        key = new BlobKey(reportId, compartment, fileName);
        return true;
    }

    private static string SegmentFor(MediaCompartment compartment) => compartment switch
    {
        MediaCompartment.Quarantine => QuarantineSegment,
        MediaCompartment.Original => OriginalSegment,
        MediaCompartment.Stripped => StrippedSegment,
        _ => throw new DomainRuleViolationException("The value is not a known media compartment."),
    };

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
            var allowed = character is (>= 'a' and <= 'z') or (>= 'A' and <= 'Z') or (>= '0' and <= '9')
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
        // A leading dot would make a hidden file on disk, and "." and ".." are
        // the traversal FileSystemBlobStore must never see.
        if (candidate is not { Length: > 0 } || candidate.Length > MaxFileNameLength || candidate[0] == '.')
        {
            return false;
        }

        foreach (var character in candidate)
        {
            var allowed = character is (>= 'a' and <= 'z') or (>= 'A' and <= 'Z') or (>= '0' and <= '9')
                or '-' or '_' or '.';

            if (!allowed)
            {
                return false;
            }
        }

        return true;
    }
}
