using System.Text.Json;
using System.Text.Json.Serialization;

namespace HpacSafety.Core.Features.QuestionBank.Typeform;

/// <summary>
///     One Typeform export, exactly as Typeform's own JSON shapes it. Verified
///     against the organization's real <c>formENG.json</c>/<c>formFR.json</c>
///     exports (checked in as test fixtures), not guessed. See ADR-0077.
/// </summary>
public sealed record TypeformDocument(
	IReadOnlyList<TypeformField> Fields,
	IReadOnlyList<TypeformLogicRule> Logic)
{
	/// <summary>Shared with <see cref="TypeformExportBuilder" />, so a written export uses the exact casing this reads back.</summary>
	public static readonly JsonSerializerOptions Options = new()
	{
		PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
		// The organization's real exports hold accented French text as plain
		// UTF-8, not \uXXXX escapes. An export matches that rather than
		// System.Text.Json's stricter default encoder.
		Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
	};

	/// <summary>Parses a Typeform export from its raw JSON text.</summary>
	public static TypeformDocument Parse(string json)
	{
		ArgumentNullException.ThrowIfNull(json);

		TypeformDocument? document;

		try
		{
			document = JsonSerializer.Deserialize<TypeformDocument>(json, Options);
		}
		catch (JsonException cause)
		{
			throw new DomainRuleViolationException("That file is not a Typeform export.", cause);
		}

		return document is { Fields: not null }
			? document
			: throw new DomainRuleViolationException("That file is not a Typeform export.");
	}

	/// <summary>Serializes this document back to the same JSON shape <see cref="Parse" /> reads.</summary>
	public string ToJson()
	{
		return JsonSerializer.Serialize(this, Options);
	}
}

/// <summary>
///     One Typeform field. <see cref="Properties" />.<see cref="TypeformFieldProperties.Fields" />
///     holds nested subfields for <c>contact_info</c> and <c>group</c> — the two
///     Typeform shapes that flatten into an HPAC <see cref="QuestionType.Group" />
///     heading plus children. See ADR-0077.
/// </summary>
public sealed record TypeformField(
	string Id,
	string Ref,
	string Title,
	string Type,
	string? SubfieldKey,
	TypeformFieldProperties Properties);

/// <summary>
///     The part of a Typeform field that varies by type. Every property is
///     optional because most field types use only a handful of these.
/// </summary>
public sealed record TypeformFieldProperties(
	string? Description,
	bool? AllowMultipleSelection,
	bool? AllowOtherChoice,
	IReadOnlyList<TypeformChoice>? Choices,
	IReadOnlyList<TypeformField>? Fields,
	TypeformHpacExtension? Hpac = null);

/// <summary>
///     Everything HPAC's own schema has that plain Typeform JSON has no field
///     for, namespaced so an export otherwise validates as a plain Typeform
///     file without it. References another field by its own <see cref="TypeformField.Ref" />
///     — the only identifier stable across a round trip — never by an internal
///     database id. See ADR-0077.
/// </summary>
public sealed record TypeformHpacExtension(
	string Type,
	bool IsPrivate,
	bool IsRequired,
	string? DependsOnKey,
	string? DependsOnOptionCode,
	string? GroupedUnderKey);

/// <summary>
///     One choice on a <c>multiple_choice</c> or <c>dropdown</c> field.
///     <see cref="Ref" />, not <see cref="Id" />, is what matches the same choice
///     across the English and French exports — <see cref="Id" /> differs per
///     language.
/// </summary>
public sealed record TypeformChoice(string Id, string Ref, string Label);

/// <summary>
///     One field's branching rule — Typeform's own model is "jump to a
///     different field next," not "show or hide this one question," so this is
///     kept as raw actions rather than interpreted. See ADR-0077.
/// </summary>
public sealed record TypeformLogicRule(string Type, string Ref, IReadOnlyList<JsonElement> Actions)
{
	/// <summary>
	///     Whether this rule branches on a real condition rather than being pure
	///     linear flow. A rule whose only action is the unconditional
	///     <c>"always"</c> fallback is just "continue to the next field" and is
	///     not worth flagging for manual review.
	/// </summary>
	public bool HasRealCondition()
	{
		foreach (var action in Actions)
		{
			if (!action.TryGetProperty("condition", out var condition))
			{
				continue;
			}

			if (!condition.TryGetProperty("op", out var op))
			{
				continue;
			}

			if (op.GetString() != "always")
			{
				return true;
			}
		}

		return false;
	}
}
