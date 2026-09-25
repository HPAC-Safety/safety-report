namespace HpacSafety.Core.Features.QuestionBank;

/// <summary>
///     What a question asks for. The picker style — dropdown versus radio buttons —
///     is presentation, not domain: both are <see cref="SingleSelect" />.
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

	/// <summary>A local wall-clock time, with no date. Stored as <c>TimeOnly</c> — ADR-0035.</summary>
	Time = 11,

	/// <summary>
	///     A type-ahead over a list that grows. Like <see cref="SingleSelect" />, its
	///     answer names one of the question's choices (ADR-0128); unlike it, a
	///     reporter may type a value it does not offer yet, which becomes a new
	///     choice (ADR-0129). An aerodrome list is an autocomplete.
	/// </summary>
	Autocomplete = 12,

	/// <summary>
	///     Instructional copy shown to the reporter. Collects no answer. See
	///     ADR-0076.
	/// </summary>
	Statement = 13,

	/// <summary>
	///     A section heading that owns nested questions and, like
	///     <see cref="Statement" />, collects no answer itself. See ADR-0076.
	/// </summary>
	Group = 14,
}
