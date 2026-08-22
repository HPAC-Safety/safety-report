using HpacSafety.Core.SharedKernel;

namespace HpacSafety.Core.Features.QuestionBank;

/// <summary>
/// What a question asks for. The picker style — dropdown versus radio buttons —
/// is presentation, not domain: both are <see cref="SingleSelect"/>.
/// </summary>
public enum QuestionType
{
    ShortText = 0,
    LongText = 1,
    Email = 2,
    Phone = 3,
    Date = 4,
    Number = 5,
    SingleSelect = 6,
    MultiSelect = 7,
    YesNo = 8,
    Checkbox = 9,
    FileUpload = 10,

    /// <summary>Copy shown to the reporter that collects no answer.</summary>
    Statement = 11,

    /// <summary>A heading that owns nested questions and collects no answer itself.</summary>
    Group = 12,
}
