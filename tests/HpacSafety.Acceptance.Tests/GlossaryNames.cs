using HpacSafety.Core.Features.Moderation;
using HpacSafety.Core.Features.Reporting;

namespace HpacSafety.Acceptance.Tests;

/// <summary>
///     Reads a role or a report status as a step names it in the glossary's words
///     (<c>.spec/glossary.md</c>, CONV-003) — "Safety Officer", "Summary failed" —
///     into the enum value the code uses.
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
}
