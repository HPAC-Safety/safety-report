namespace HpacSafety.Core.Features.Anonymization;

/// <summary>
/// One link in the deterministic scrub's <b>chain of responsibility</b>. Each
/// stage removes one category of identifier and hands the document to the next.
/// </summary>
/// <remarks>
/// The chain is deliberately <b>internal and closed</b>. Anonymization is an
/// invariant of this system, not a policy a caller configures: there is no way
/// to construct a scrub that is missing the email stage, and no extension point
/// that would let a caller opt out of one. See AGENTS.md, "the invariants above
/// are deliberately closed".
/// </remarks>
internal abstract class ScrubStage
{
    private ScrubStage? _next;

    /// <summary>Appends <paramref name="next"/> to the end of this chain, and returns the head.</summary>
    internal ScrubStage Then(ScrubStage next)
    {
        ArgumentNullException.ThrowIfNull(next);

        var tail = this;

        while (tail._next is not null)
        {
            tail = tail._next;
        }

        tail._next = next;
        return this;
    }

    /// <summary>Runs this stage, then the rest of the chain.</summary>
    internal ScrubDocument Scrub(ScrubDocument document)
    {
        Handle(document);
        return _next is null ? document : _next.Scrub(document);
    }

    /// <summary>Removes this stage's category of identifier.</summary>
    protected abstract void Handle(ScrubDocument document);
}
