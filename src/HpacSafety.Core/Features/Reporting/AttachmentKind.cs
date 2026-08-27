namespace HpacSafety.Core.Features.Reporting;

/// <summary>
/// The broad category of an uploaded attachment. Images and video are
/// anonymized and may grow a reviewer derivative; documents are validated,
/// malware-checked, and kept private — never transformed, sent to the model, or
/// published. See product invariant #5 and <c>docs/data-and-persistence.md</c>.
/// </summary>
public enum AttachmentKind
{
    Image = 0,
    Video = 1,
    Document = 2,
}
