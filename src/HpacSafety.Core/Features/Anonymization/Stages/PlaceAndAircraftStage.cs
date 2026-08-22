namespace HpacSafety.Core.Features.Anonymization.Stages;

/// <summary>
/// Removes the launch, landing zone, and aircraft make and model from free text,
/// using the words the reporter typed into the structured answers.
/// </summary>
/// <remarks>
/// A place and an aircraft get no role word. "The pilot" is still a person doing
/// something in a sentence; there is no equivalent noun for a launch that does
/// not narrow it down, and "a mountain launch in British Columbia" is exactly
/// the kind of detail that names one person to the fifty people who fly there.
/// The aircraft is described by its certification class, which comes from the
/// reporter's own answer and never from the model name — see
/// docs/aircraft-classification.md.
/// </remarks>
internal sealed class PlaceAndAircraftStage : ScrubStage
{
    protected override void Handle(ScrubDocument document)
    {
        if (document.Terms.Count == 0)
        {
            return;
        }

        var matchers = document.Terms.Select(ScrubPatterns.Token).ToList();

        document.RewriteValues(value =>
        {
            foreach (var matcher in matchers)
            {
                value = matcher.Replace(value, _ => ScrubMarker.Removed);
            }

            return value;
        });
    }
}
