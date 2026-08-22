using HpacSafety.Core.Features.Reporting;

namespace HpacSafety.Core.Features.Anonymization;

/// <summary>
/// The working state passed along the chain of responsibility: the fields as
/// they stand, plus the identifiers harvested from the structured answers that
/// later stages hunt for in free text.
/// </summary>
internal sealed class ScrubDocument
{
    internal ScrubDocument(ScrubRequest request, ScrubVocabulary vocabulary)
    {
        Province = request.Province;
        OccurredOn = request.OccurredOn;
        TimeOfDay = request.TimeOfDay;
        Vocabulary = vocabulary;
        Fields = [.. request.Fields.Where(field => field is not null)];
    }

    /// <summary>The region a location field is generalized to.</summary>
    internal Province Province { get; }

    /// <summary>The occurrence date, narrowed on the way out.</summary>
    internal DateOnly? OccurredOn { get; }

    /// <summary>The coarse time-of-day bucket, the only form that travels onward.</summary>
    internal TimeOfDay TimeOfDay { get; }

    /// <summary>The role words a name is replaced with.</summary>
    internal ScrubVocabulary Vocabulary { get; }

    /// <summary>The fields, as the chain has left them so far.</summary>
    internal List<ScrubField> Fields { get; }

    /// <summary>
    /// Name tokens taken from the structured name answers, longest first, each
    /// with the role word that stands in for it.
    /// </summary>
    internal List<NameSubstitution> Names { get; } = [];

    /// <summary>
    /// Site and aircraft tokens taken from the structured answers, longest
    /// first. These are removed rather than replaced — a launch has no role.
    /// </summary>
    internal List<string> Terms { get; } = [];

    /// <summary>Rewrites every surviving field value through <paramref name="rewrite"/>.</summary>
    internal void RewriteValues(Func<string, string> rewrite)
    {
        for (var i = 0; i < Fields.Count; i++)
        {
            var field = Fields[i];

            if (!string.IsNullOrWhiteSpace(field.Value))
            {
                Fields[i] = field with { Value = rewrite(field.Value) };
            }
        }
    }
}

/// <summary>One name token and the role word that replaces it.</summary>
internal sealed record NameSubstitution(string Token, string Replacement);
