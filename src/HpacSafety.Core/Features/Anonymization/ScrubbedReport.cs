namespace HpacSafety.Core.Features.Anonymization;

/// <summary>
/// What stage 1 produces: the fields that survived, and the text handed to the
/// summarizer. Nothing else in the pipeline may reach a model without passing
/// through here first.
/// </summary>
public sealed class ScrubbedReport
{
    internal ScrubbedReport(IReadOnlyList<ScrubField> fields)
    {
        Fields = fields;
        Text = string.Join(Environment.NewLine, fields.Select(field => $"{field.Label}: {field.Value}"));
    }

    /// <summary>The surviving fields, scrubbed. Dropped fields are simply absent.</summary>
    public IReadOnlyList<ScrubField> Fields { get; }

    /// <summary>The scrubbed report rendered as labelled lines.</summary>
    public string Text { get; }
}
