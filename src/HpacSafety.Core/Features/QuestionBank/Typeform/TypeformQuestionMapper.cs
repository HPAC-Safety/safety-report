using System.Text.Json;
using HpacSafety.Core;

namespace HpacSafety.Core.Features.QuestionBank.Typeform;

/// <summary>
///     Turns a matched English/French Typeform export pair into review drafts.
///     See ADR-0077 for the full type-mapping table and rationale.
/// </summary>
/// <remarks>
///     <para>
///         <b>English-led, not hard-reject-on-mismatch.</b> An earlier version of
///         this design rejected the whole pair the moment one field's <c>ref</c>
///         was missing from either file. Reviewing the organization's own real
///         export pair found exactly that case — the publication-consent field
///         carries a different <c>ref</c> in each language — which would make
///         this importer unable to import the organization's actual form.
///         Instead, the English file drives which fields exist and their order;
///         a field or choice with no French counterpart by <c>ref</c> defaults
///         its French text to the English text and is flagged
///         <see cref="ImportedQuestionDraft.FrenchDefaultedToEnglish" />, so an
///         Administrator sees exactly what still needs real French wording
///         before saving. A French-only field (no English counterpart) has
///         nothing to anchor it and is not imported.
///     </para>
///     <para>
///         <b>Every real branching rule is pending, none are auto-mapped.</b>
///         Typeform's own logic model is "jump to a different field next," not
///         "show or hide this one question" — translating one correctly into
///         the other requires reasoning about the whole flow graph, not one
///         field's rule in isolation. Getting that subtly wrong would silently
///         wire the wrong dependency, which is worse than not wiring one at
///         all. This first pass captures every field with a real (non-"always")
///         condition as a <see cref="PendingTypeformLogic" /> note instead.
///     </para>
///     <para>
///         <b>The <c>hpac</c> extension, when present, wins.</b> A field
///         <see cref="TypeformExportBuilder" /> wrote carries a namespaced
///         <c>hpac</c> object naming the exact HpacSafety type (disambiguating
///         <see cref="QuestionType.Group" /> from <see cref="QuestionType.Statement" />,
///         both plain <c>statement</c>; <see cref="QuestionType.Autocomplete" />
///         from <see cref="QuestionType.SingleSelect" />, both plain
///         <c>multiple_choice</c>), privacy, required, and the
///         depends-on/grouped-under relationship by key — an export is flat, so
///         that relationship cannot be recovered from Typeform's own
///         <c>group</c>/<c>contact_info</c> nesting the way it is for a real
///         Typeform export. A file with no <c>hpac</c> object — a real Typeform
///         export, or a hand-authored fixture — keeps today's native-type-derived
///         behavior exactly as before.
///     </para>
/// </remarks>
public static class TypeformQuestionMapper
{
	/// <summary>Maps a matched English/French pair into drafts, rejections, and pending-logic notes.</summary>
	public static TypeformImportResult Map(TypeformDocument english, TypeformDocument french)
	{
		ArgumentNullException.ThrowIfNull(english);
		ArgumentNullException.ThrowIfNull(french);

		var frenchByRef = IndexByRef(french.Fields);
		var logicByRef = english.Logic.ToDictionary(rule => rule.Ref);

		var drafts = new List<ImportedQuestionDraft>();
		var rejected = new List<RejectedTypeformField>();
		var pendingLogic = new List<PendingTypeformLogic>();

		foreach (var field in english.Fields)
		{
			MapField(field, frenchByRef, logicByRef, groupedUnderKey: null, drafts, rejected, pendingLogic);
		}

		return new TypeformImportResult(drafts, rejected, pendingLogic);
	}

	private static void MapField(
		TypeformField field,
		IReadOnlyDictionary<string, TypeformField> frenchByRef,
		IReadOnlyDictionary<string, TypeformLogicRule> logicByRef,
		string? groupedUnderKey,
		List<ImportedQuestionDraft> drafts,
		List<RejectedTypeformField> rejected,
		List<PendingTypeformLogic> pendingLogic)
	{
		frenchByRef.TryGetValue(field.Ref, out var frenchField);
		RecordPendingLogic(field, logicByRef, pendingLogic);

		switch (field.Type)
		{
			case "statement":
				if (IsGeneratedRecapScreen(field))
				{
					return;
				}

				drafts.Add(HeadingDraft(field, frenchField, QuestionType.Statement, groupedUnderKey));
				return;

			case "group":
			case "contact_info":
				var groupKey = QuestionKey.Normalize(field.Ref);
				drafts.Add(HeadingDraft(field, frenchField, QuestionType.Group, groupedUnderKey));

				foreach (var child in field.Properties.Fields ?? [])
				{
					MapField(child, frenchByRef, logicByRef, groupKey, drafts, rejected, pendingLogic);
				}

				return;

			case "short_text":
				drafts.Add(SimpleDraft(field, frenchField, QuestionType.ShortText, groupedUnderKey));
				return;

			case "long_text":
				drafts.Add(SimpleDraft(field, frenchField, QuestionType.LongText, groupedUnderKey));
				return;

			case "email":
				drafts.Add(SimpleDraft(field, frenchField, QuestionType.Email, groupedUnderKey));
				return;

			case "phone_number":
				drafts.Add(SimpleDraft(field, frenchField, QuestionType.Phone, groupedUnderKey));
				return;

			case "date":
				drafts.Add(SimpleDraft(field, frenchField, QuestionType.Date, groupedUnderKey));
				return;

			case "number":
				drafts.Add(SimpleDraft(field, frenchField, QuestionType.Number, groupedUnderKey));
				return;

			case "file_upload":
				drafts.Add(SimpleDraft(field, frenchField, QuestionType.FileUpload, groupedUnderKey));
				return;

			case "yes_no":
				drafts.Add(SimpleDraft(field, frenchField, QuestionType.YesNo, groupedUnderKey));
				return;

			case "dropdown":
				drafts.Add(ChoiceDraft(field, frenchField, QuestionType.SingleSelect, groupedUnderKey));
				return;

			case "multiple_choice":
				// allow_other_choice is ignored: only a type-ahead takes a
				// reporter's added choice (ADR-0095).
				var multiSelect = field.Properties.AllowMultipleSelection == true;
				drafts.Add(
					ChoiceDraft(
						field, frenchField, multiSelect ? QuestionType.MultiSelect : QuestionType.SingleSelect,
						groupedUnderKey));
				return;

			default:
				rejected.Add(new RejectedTypeformField(field.Ref, field.Title, field.Type));
				return;
		}
	}

	/// <summary>
	///     Typeform appends a statement summarizing prior answers with
	///     <c>{{field:...}}</c> merge tags. It carries no authored content of its
	///     own and would only ever show a reporter literal, unresolved tag text.
	/// </summary>
	private static bool IsGeneratedRecapScreen(TypeformField field)
	{
		return field.Properties.Description?.Contains("{{field:", StringComparison.Ordinal) == true;
	}

	private static void RecordPendingLogic(
		TypeformField field,
		IReadOnlyDictionary<string, TypeformLogicRule> logicByRef,
		List<PendingTypeformLogic> pendingLogic)
	{
		if (!logicByRef.TryGetValue(field.Ref, out var rule) || !rule.HasRealCondition())
		{
			return;
		}

		var raw = JsonSerializer.Serialize(rule.Actions);
		pendingLogic.Add(new PendingTypeformLogic(field.Ref, field.Title, raw));
	}

	private static ImportedQuestionDraft HeadingDraft(
		TypeformField field, TypeformField? frenchField, QuestionType type, string? groupedUnderKey)
	{
		var (labelEn, labelFr, defaulted) = Pair(field.Title, frenchField?.Title);
		var (helpEn, helpFr, _) = PairHelp(field.Properties.Description, frenchField?.Properties.Description);

		var draft = new ImportedQuestionDraft(
			QuestionKey.Normalize(field.Ref), type, labelEn, labelFr, defaulted, helpEn, helpFr, groupedUnderKey,
			Options: []);

		return ApplyHpac(draft, field, groupedUnderKey);
	}

	private static ImportedQuestionDraft SimpleDraft(
		TypeformField field, TypeformField? frenchField, QuestionType type, string? groupedUnderKey)
	{
		var (labelEn, labelFr, defaulted) = Pair(field.Title, frenchField?.Title);
		var (helpEn, helpFr, _) = PairHelp(field.Properties.Description, frenchField?.Properties.Description);

		var draft = new ImportedQuestionDraft(
			QuestionKey.Normalize(field.Ref), type, labelEn, labelFr, defaulted, helpEn, helpFr, groupedUnderKey,
			Options: []);

		return ApplyHpac(draft, field, groupedUnderKey);
	}

	private static ImportedQuestionDraft ChoiceDraft(
		TypeformField field,
		TypeformField? frenchField,
		QuestionType type,
		string? groupedUnderKey)
	{
		var (labelEn, labelFr, defaulted) = Pair(field.Title, frenchField?.Title);
		var (helpEn, helpFr, _) = PairHelp(field.Properties.Description, frenchField?.Properties.Description);

		var frenchChoicesByRef = (frenchField?.Properties.Choices ?? [])
			.ToDictionary(choice => choice.Ref);

		var options = (field.Properties.Choices ?? [])
			.Select(choice =>
			{
				frenchChoicesByRef.TryGetValue(choice.Ref, out var frenchChoice);
				var (optionEn, optionFr, optionDefaulted) = Pair(choice.Label, frenchChoice?.Label);

				return new ImportedOption(choice.Ref, optionEn, optionFr, optionDefaulted);
			})
			.ToList();

		var draft = new ImportedQuestionDraft(
			QuestionKey.Normalize(field.Ref), type, labelEn, labelFr, defaulted, helpEn, helpFr, groupedUnderKey,
			options);

		return ApplyHpac(draft, field, groupedUnderKey);
	}

	/// <summary>
	///     Overrides a draft's type, privacy, required, reporter-additions, and
	///     depends-on/grouped-under relationship from the field's <c>hpac</c>
	///     extension, when present. See the class remarks.
	/// </summary>
	private static ImportedQuestionDraft ApplyHpac(ImportedQuestionDraft draft, TypeformField field, string? groupedUnderKey)
	{
		var hpac = field.Properties.Hpac;

		if (hpac is null)
		{
			return draft with { IsPrivate = draft.Type is QuestionType.Statement or QuestionType.Group ? false : draft.IsPrivate };
		}

		var type = EnumCode.TryParse<QuestionType>(hpac.Type, out var realType) ? realType : draft.Type;

		return draft with
		{
			Type = type,
			IsPrivate = hpac.IsPrivate,
			IsRequired = hpac.IsRequired,
			DependsOnKey = hpac.DependsOnKey,
			DependsOnOptionCode = hpac.DependsOnOptionCode,
			GroupedUnderKey = hpac.GroupedUnderKey ?? groupedUnderKey,
		};
	}

	/// <summary>
	///     Pairs a required English value with its French counterpart, if one
	///     was found by <c>ref</c>. Defaults to the English text, flagged, when
	///     French is missing — see the class remarks.
	/// </summary>
	private static (string En, string Fr, bool Defaulted) Pair(string english, string? french)
	{
		return string.IsNullOrWhiteSpace(french) ? (english, english, true) : (english, french, false);
	}

	/// <summary>
	///     Pairs optional help text the same way <see cref="Pair" /> does,
	///     except a missing English side stays <c>null</c> rather than
	///     defaulting anything.
	/// </summary>
	private static (string? En, string? Fr, bool Defaulted) PairHelp(string? english, string? french)
	{
		if (string.IsNullOrWhiteSpace(english))
		{
			return (null, null, false);
		}

		return string.IsNullOrWhiteSpace(french) ? (english, english, true) : (english, french, false);
	}

	private static Dictionary<string, TypeformField> IndexByRef(IReadOnlyList<TypeformField> fields)
	{
		var index = new Dictionary<string, TypeformField>();
		Index(fields, index);
		return index;
	}

	private static void Index(IReadOnlyList<TypeformField> fields, Dictionary<string, TypeformField> index)
	{
		foreach (var field in fields)
		{
			index[field.Ref] = field;

			if (field.Properties.Fields is { } nested)
			{
				Index(nested, index);
			}
		}
	}
}
