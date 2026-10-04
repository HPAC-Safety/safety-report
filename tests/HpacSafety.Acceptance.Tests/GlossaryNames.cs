using HpacSafety.Core;
using HpacSafety.Core.Features.Moderation;
using HpacSafety.Core.Features.QuestionBank;
using HpacSafety.Core.Features.Reporting;

namespace HpacSafety.Acceptance.Tests;

/// <summary>
///     Reads a role, a report status, or a question type as a step names it in the
///     glossary's words (<c>.spec/glossary.md</c>, CONV-003, CONV-004) — "Safety
///     Officer", "Summary failed", "type-ahead" — into the enum value the code uses.
/// </summary>
internal static class GlossaryNames
{
	public static MemberRole Role(string name)
	{
		ArgumentNullException.ThrowIfNull(name);
		return Enum.Parse<MemberRole>(name.Replace(" ", string.Empty, StringComparison.Ordinal));
	}

	public static ReportStatus Status(string name)
	{
		ArgumentNullException.ThrowIfNull(name);
		return Enum.Parse<ReportStatus>(name.Replace(" ", string.Empty, StringComparison.Ordinal), ignoreCase: true);
	}

	/// <summary>A question type by its glossary name: "short text", "paragraph", "single-select", "type-ahead", "yes/no", "file upload".</summary>
	public static QuestionType QuestionType(string name)
	{
		return name switch
		{
			"short text" => Core.Features.QuestionBank.QuestionType.ShortText,
			"paragraph" => Core.Features.QuestionBank.QuestionType.LongText,
			"email" => Core.Features.QuestionBank.QuestionType.Email,
			"phone" => Core.Features.QuestionBank.QuestionType.Phone,
			"date" => Core.Features.QuestionBank.QuestionType.Date,
			"time" => Core.Features.QuestionBank.QuestionType.Time,
			"number" => Core.Features.QuestionBank.QuestionType.Number,
			"single-select" => Core.Features.QuestionBank.QuestionType.SingleSelect,
			"multi-select" => Core.Features.QuestionBank.QuestionType.MultiSelect,
			"type-ahead" => Core.Features.QuestionBank.QuestionType.Autocomplete,
			"yes/no" => Core.Features.QuestionBank.QuestionType.YesNo,
			"checkbox" => Core.Features.QuestionBank.QuestionType.Checkbox,
			"file upload" => Core.Features.QuestionBank.QuestionType.FileUpload,
			"statement" => Core.Features.QuestionBank.QuestionType.Statement,
			"group" => Core.Features.QuestionBank.QuestionType.Group,
			_ => throw new ArgumentOutOfRangeException(nameof(name), name, "Not a question type the glossary names."),
		};
	}

	/// <summary>The wire code of a question type named in the glossary's words: "type-ahead" is <c>autocomplete</c>.</summary>
	public static string QuestionTypeCode(string name)
	{
		return EnumCode.Of(QuestionType(name));
	}
}
