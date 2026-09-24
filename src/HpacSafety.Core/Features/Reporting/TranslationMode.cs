namespace HpacSafety.Core.Features.Reporting;

/// <summary>
///     How an answer gets its second official language, decided when it is
///     recorded and never changed. Stored as an invariant code. See ADR-0112.
/// </summary>
public enum TranslationMode
{
	/// <summary>
	///     Never has one: unmarked text, email, phone, date, time, number, yes/no,
	///     checkbox, file. Nothing is sent, queued, or shown for it.
	/// </summary>
	None = 0,

	/// <summary>
	///     Copied at submission from the other-language label of the choice the
	///     answer names (<see cref="TranslationSource.Choice" />).
	/// </summary>
	Choice = 1,

	/// <summary>
	///     Filled off the submission path by the Worker's machine translation, or
	///     by an administrator by hand.
	/// </summary>
	Machine = 2,
}
