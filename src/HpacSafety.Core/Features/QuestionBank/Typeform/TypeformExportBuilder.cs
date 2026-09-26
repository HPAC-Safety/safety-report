using HpacSafety.Core;

namespace HpacSafety.Core.Features.QuestionBank.Typeform;

/// <summary>
///     Turns the live question bank into an English/French Typeform-shaped
///     document pair — the reverse of <see cref="TypeformQuestionMapper" />.
///     See ADR-0077.
/// </summary>
/// <remarks>
///     <para>
///         Every field is exported flat, one per HPAC question — a
///         <see cref="QuestionType.Group" /> is not re-nested into Typeform's own
///         <c>group</c>/<c>contact_info</c> shape. The plain Typeform type chosen
///         for a type Typeform has no native equivalent for
///         (<see cref="QuestionType.Group" />, <see cref="QuestionType.Statement" />,
///         <see cref="QuestionType.Autocomplete" />, <see cref="QuestionType.Checkbox" />,
///         <see cref="QuestionType.Time" />) is a syntactically valid placeholder
///         only; <see cref="TypeformHpacExtension.Type" /> carries the real type.
///     </para>
///     <para>
///         A field's <see cref="TypeformField.Ref" /> is its own stable
///         <see cref="Question.Key" /> — the same identity <see cref="TypeformQuestionMapper" />
///         derives a key from on import, so a hand-authored question exports with
///         a ref of its own rather than needing one invented.
///     </para>
/// </remarks>
public static class TypeformExportBuilder
{
	/// <summary>Builds the English and French documents for the given live questions.</summary>
	/// <param name="questions">Every live question, in display order, with its choices loaded.</param>
	public static (TypeformDocument English, TypeformDocument French) Build(IReadOnlyList<Question> questions)
	{
		ArgumentNullException.ThrowIfNull(questions);

		var keysByQuestionId = questions.ToDictionary(question => question.Id, question => question.Key);
		var questionsById = questions.ToDictionary(question => question.Id);

		var englishFields = new List<TypeformField>();
		var frenchFields = new List<TypeformField>();

		foreach (var question in questions)
		{
			var revision = question.CurrentRevision;
			var choices = question.Choices;

			var hpac = new TypeformHpacExtension(
				EnumCode.Of(revision.Type),
				revision.IsPrivate,
				revision.IsRequired,
				NameOf(revision.DependsOnQuestionId, keysByQuestionId),
				RequiredCodeOf(revision, questionsById),
				NameOf(revision.GroupedUnderQuestionId, keysByQuestionId));

			englishFields.Add(Field(question.Key, revision.LabelEn, revision.HelpTextEn, revision.Type, choices, hpac, english: true));
			frenchFields.Add(Field(question.Key, revision.LabelFr, revision.HelpTextFr, revision.Type, choices, hpac, english: false));
		}

		return (new TypeformDocument(englishFields, []), new TypeformDocument(frenchFields, []));
	}

	/// <summary>
	///     The code of the parent's choice this revision requires — the choice that
	///     stands for it today, so a replaced option exports as its replacement. The
	///     file names choices by code, the one identifier both language files share
	///     (ADR-0077, ADR-0128).
	/// </summary>
	private static string? RequiredCodeOf(QuestionRevision revision,
										  Dictionary<TinyId, Question> questionsById)
	{
		return revision is { DependsOnQuestionId: { } parentId, DependsOnChoiceId: { } choiceId }
			   && questionsById.TryGetValue(parentId, out var parent)
			? parent.CurrentChoice(choiceId)?.Code
			: null;
	}

	private static string? NameOf(TinyId? questionId,
								  Dictionary<TinyId, string> keysByQuestionId)
	{
		return questionId is { } id && keysByQuestionId.TryGetValue(id, out var key) ? key : null;
	}

	private static TypeformField Field(
		string key,
		string label,
		string? helpText,
		QuestionType type,
		IReadOnlyList<QuestionChoice> choices,
		TypeformHpacExtension hpac,
		bool english)
	{
		var properties = new TypeformFieldProperties(
			helpText,
			AllowMultipleSelection: type == QuestionType.MultiSelect ? true : null,
			AllowOtherChoice: null,
			Choices: choices.Count == 0
				? null
				: [.. choices.Select(choice => new TypeformChoice(choice.Code, choice.Code, choice.Label(english ? Locale.EnCa : Locale.FrCa)))],
			Fields: null,
			Hpac: hpac);

		return new TypeformField(key, key, label, NativeType(type), SubfieldKey: null, properties);
	}

	/// <summary>
	///     The closest plain Typeform field type. Several HPAC types have no
	///     native Typeform equivalent and share a placeholder — see this class's
	///     remarks; <see cref="TypeformHpacExtension.Type" /> is the real type.
	/// </summary>
	private static string NativeType(QuestionType type)
	{
		return type switch
		{
			QuestionType.ShortText => "short_text",
			QuestionType.LongText => "long_text",
			QuestionType.Email => "email",
			QuestionType.Phone => "phone_number",
			QuestionType.Date => "date",
			QuestionType.Number => "number",
			QuestionType.SingleSelect => "multiple_choice",
			QuestionType.MultiSelect => "multiple_choice",
			QuestionType.YesNo => "yes_no",
			QuestionType.FileUpload => "file_upload",
			QuestionType.Autocomplete => "multiple_choice",
			QuestionType.Statement => "statement",
			QuestionType.Group => "statement",
			QuestionType.Checkbox => "short_text",
			QuestionType.Time => "short_text",
			_ => throw new ArgumentOutOfRangeException(nameof(type), type, "Unknown question type."),
		};
	}
}
