using System.Globalization;
using System.Text;
using HpacSafety.Core;
using Microsoft.EntityFrameworkCore.Migrations;

namespace HpacSafety.Infrastructure.Persistence.Seeding;

/// <summary>
///     Writes <see cref="QuestionBankSeed" /> into a fresh database, so a clean
///     install asks exactly the question set HPAC has been collecting.
/// </summary>
/// <remarks>
///     <para>
///         The rows are written by the migration rather than declared with
///         <c>HasData</c>. <c>HasData</c> makes seed rows part of the model snapshot,
///         and the whole point of ADR-0016 is that an administrator edits these rows
///         after deployment — every one of those edits would then show up as a model
///         difference that the next migration tries to undo. See ADR-0020.
///     </para>
///     <para>
///         Identifiers are derived from the question key rather than drawn at random,
///         so the same migration produces the same rows on every database and in a
///         generated SQL script. See <see cref="SeedIds" />.
///     </para>
///     <para>
///         Every row is written with an <c>INSERT ... SELECT ... WHERE NOT EXISTS</c>
///         guard on its own identifier, the same shape <see cref="DevelopmentAdminSeed" />
///         uses for its one row — not EF's <c>InsertData</c>, which has no guard and
///         errors on a second write. That makes re-applying this seed a safe no-op:
///         against a database that only lost its <c>__EFMigrationsHistory</c> row, or
///         against one a future migration deliberately re-seeds because an
///         administrator emptied the question bank by hand. Deleting and re-inserting
///         was considered and rejected — a seeded question a report has already
///         answered is referenced by <c>report_answers</c> with
///         <c>DeleteBehavior.Restrict</c>, so a delete would fail once real answers
///         exist. See ADR-0020.
///     </para>
/// </remarks>
public static class QuestionBankSeedWriter
{
	/// <summary>Writes every seeded row through the migration.</summary>
	/// <param name="migrationBuilder">The migration being applied.</param>
	public static void Write(MigrationBuilder migrationBuilder)
	{
		Write(migrationBuilder, QuestionBankSeed.Questions);
	}

	/// <summary>
	///     Writes an arbitrary question list through the migration. Exposed so a
	///     test can exercise the guarded-insert SQL actually being scheduled,
	///     without depending on what <see cref="QuestionBankSeed" /> currently
	///     seeds.
	/// </summary>
	public static void Write(MigrationBuilder migrationBuilder,
							 IReadOnlyList<SeededQuestion> questions)
	{
		ArgumentNullException.ThrowIfNull(migrationBuilder);
		AppendIfAny(migrationBuilder, Sql(questions));
	}

	/// <summary>
	///     Writes the seed against the schema shape used by the original,
	///     already-shipped <c>InitialSchema</c> migration — <c>questions</c>,
	///     <c>question_versions</c>, <c>question_translations</c>, and
	///     <c>question_options</c>, before the fork-on-answer redesign replaced them
	///     with <c>questions</c> and <c>question_revisions</c> (ADR-0071). Only that
	///     migration calls this; a migration that already shipped is never edited, so
	///     this stays exactly as it was regardless of how <see cref="SeededQuestion" />
	///     or the current schema evolve.
	/// </summary>
	public static void WriteLegacySensitivitySchema(MigrationBuilder migrationBuilder)
	{
		ArgumentNullException.ThrowIfNull(migrationBuilder);
		AppendIfAny(migrationBuilder, LegacySql(QuestionBankSeed.Questions));
	}

	/// <summary>
	///     <see cref="MigrationBuilder.Sql(string, bool)" /> refuses an empty
	///     string, which an empty <see cref="QuestionBankSeed" /> produces.
	/// </summary>
	private static void AppendIfAny(MigrationBuilder migrationBuilder,
									string sql)
	{
		if (sql.Length > 0)
		{
			migrationBuilder.Sql(sql);
		}
	}

	/// <summary>
	///     The guarded SQL for an arbitrary question list, against the current
	///     <c>questions</c>/<c>question_revisions</c>/<c>question_revision_options</c>
	///     schema. Exposed so a test can exercise every row this writer produces —
	///     the question, its first revision, both languages, dependency and
	///     grouping, and any options — without depending on what
	///     <see cref="QuestionBankSeed" /> currently seeds.
	/// </summary>
	public static string Sql(IReadOnlyList<SeededQuestion> questions)
	{
		ArgumentNullException.ThrowIfNull(questions);

		var sql = new StringBuilder();
		var at = QuestionBankSeed.SeededAt;

		for (var displayOrder = 0; displayOrder < questions.Count; displayOrder++)
		{
			var question = questions[displayOrder];
			var questionId = SeedIds.For($"question:{question.Key}");
			// Named "question_version", not "question_revision": a fresh
			// database seeds through InitialSchema's legacy schema first, and
			// MigrateCanonicalDomainAndPersistence carries that row forward by
			// reusing its id verbatim as the question_revisions row's id. This
			// name has to keep matching that carried-forward derivation, or a
			// from-scratch database seeds every revision twice under two
			// different ids and violates the one-revision-per-number index.
			var revisionId = SeedIds.For($"question_version:{question.Key}:1");

			AppendGuardedInsert(
				sql,
				"questions",
				["id", "key", "is_system", "role", "created_at", "deleted"],
				[Id(questionId), Str(question.Key), Bool(question.IsSystem), Str(EnumCode.Of(question.Role)), Timestamp(at), "NULL"],
				"id",
				Id(questionId));

			AppendGuardedInsert(
				sql,
				"question_revisions",
				[
					"id", "question_id", "revision_number", "type", "label_en", "label_fr", "help_text_en", "help_text_fr",
					"placeholder_en", "placeholder_fr", "is_system", "is_required", "is_private", "is_active",
					"display_order", "depends_on_question_id", "depends_on_option_code", "option_set_id",
					"grouped_under_question_id", "allows_reporter_additions", "created_at", "deleted",
				],
				[
					Id(revisionId), Id(questionId), Int(1), Str(EnumCode.Of(question.Type)), Str(question.LabelEn),
					Str(question.LabelFr), StrOrNull(question.HelpEn), StrOrNull(question.HelpFr),
					StrOrNull(question.PlaceholderEn), StrOrNull(question.PlaceholderFr), Bool(question.IsSystem),
					Bool(question.IsRequired), Bool(question.IsPrivate), Bool(true), Int(displayOrder),
					IdOrNull(question.DependsOnKey, key => SeedIds.For($"question:{key}")),
					StrOrNull(question.DependsOnOptionCode), "NULL",
					IdOrNull(question.GroupedUnderKey, key => SeedIds.For($"question:{key}")),
					Bool(question.AllowsReporterAdditions), Timestamp(at), "NULL",
				],
				"id",
				Id(revisionId));

			for (var optionOrder = 0; optionOrder < question.Options.Count; optionOrder++)
			{
				var option = question.Options[optionOrder];
				// Same reasoning as revisionId above: "question_option", not
				// "question_revision_option", to match what
				// MigrateCanonicalDomainAndPersistence carries forward.
				var optionId = SeedIds.For($"question_option:{question.Key}:{option.Code}");

				AppendGuardedInsert(
					sql,
					"question_revision_options",
					["id", "question_revision_id", "code", "label_en", "label_fr", "display_order", "source_item_id", "deleted"],
					[Id(optionId), Id(revisionId), Str(option.Code), Str(option.LabelEn), Str(option.LabelFr), Int(optionOrder), "NULL", "NULL"],
					"id",
					Id(optionId));
			}
		}

		return sql.ToString();
	}

	/// <summary>
	///     The frozen SQL <see cref="WriteLegacySensitivitySchema" /> writes, against
	///     the <c>sensitivity</c>-column shape the original <c>InitialSchema</c>
	///     migration created. Exposed so a test can exercise it directly with a
	///     synthetic question list, the same way <see cref="Sql" /> is, without
	///     depending on <see cref="QuestionBankSeed" />.
	/// </summary>
	public static string LegacySql(IReadOnlyList<SeededQuestion> questions)
	{
		ArgumentNullException.ThrowIfNull(questions);

		var sql = new StringBuilder();
		var at = QuestionBankSeed.SeededAt;

		for (var order = 0; order < questions.Count; order++)
		{
			var question = questions[order];
			var questionId = SeedIds.For($"question:{question.Key}");
			var versionId = SeedIds.For($"question_version:{question.Key}:1");

			AppendGuardedInsert(
				sql,
				"questions",
				["id", "key", "is_system", "role", "sensitivity", "display_order", "is_active", "created_at", "deleted_at"],
				[Id(questionId), Str(question.Key), Bool(question.IsSystem), Str(EnumCode.Of(question.Role)), Str(question.IsPrivate ? "restricted" : "publishable"), Int(order), Bool(true), Timestamp(at), "NULL"],
				"id",
				Id(questionId));

			AppendGuardedInsert(
				sql,
				"question_versions",
				["id", "question_id", "version_number", "type", "is_required", "created_at"],
				[Id(versionId), Id(questionId), Int(1), Str(EnumCode.Of(question.Type)), Bool(question.IsRequired), Timestamp(at)],
				"id",
				Id(versionId));

			AppendLegacyQuestionTranslation(sql, question, versionId, Locale.EnCa, question.LabelEn, question.HelpEn, true);
			AppendLegacyQuestionTranslation(sql, question, versionId, Locale.FrCa, question.LabelFr, question.HelpFr, false);

			for (var optionOrder = 0; optionOrder < question.Options.Count; optionOrder++)
			{
				var option = question.Options[optionOrder];
				var optionId = SeedIds.For($"question_option:{question.Key}:{option.Code}");

				AppendGuardedInsert(
					sql,
					"question_options",
					["id", "question_version_id", "code", "display_order"],
					[Id(optionId), Id(versionId), Str(option.Code), Int(optionOrder)],
					"id",
					Id(optionId));

				AppendLegacyOptionTranslation(sql, question, option, optionId, Locale.EnCa, option.LabelEn, true);
				AppendLegacyOptionTranslation(sql, question, option, optionId, Locale.FrCa, option.LabelFr, false);
			}
		}

		return sql.ToString();
	}

	private static void AppendLegacyQuestionTranslation(
		StringBuilder sql,
		SeededQuestion question,
		TinyId versionId,
		Locale locale,
		string label,
		string? helpText,
		bool isSource)
	{
		var at = QuestionBankSeed.SeededAt;
		var id = SeedIds.For($"question_translation:{question.Key}:1:{locale.Code}");

		AppendGuardedInsert(
			sql,
			"question_translations",
			["id", "question_version_id", "locale", "label", "help_text", "placeholder", "is_source", "is_machine_translated", "translated_at", "updated_at"],
			[Id(id), Id(versionId), Str(locale.Code), Str(label), StrOrNull(helpText), "NULL", Bool(isSource), Bool(!isSource), isSource ? "NULL" : Timestamp(at), Timestamp(at)],
			"id",
			Id(id));
	}

	private static void AppendLegacyOptionTranslation(
		StringBuilder sql,
		SeededQuestion question,
		SeededOption option,
		TinyId optionId,
		Locale locale,
		string label,
		bool isSource)
	{
		var at = QuestionBankSeed.SeededAt;
		var id = SeedIds.For($"question_option_translation:{question.Key}:{option.Code}:{locale.Code}");

		AppendGuardedInsert(
			sql,
			"question_option_translations",
			["id", "question_option_id", "locale", "label", "is_source", "is_machine_translated", "translated_at", "updated_at"],
			[Id(id), Id(optionId), Str(locale.Code), Str(label), Bool(isSource), Bool(!isSource), isSource ? "NULL" : Timestamp(at), Timestamp(at)],
			"id",
			Id(id));
	}

	/// <summary>
	///     <c>
	///         INSERT INTO table (columns) SELECT values WHERE NOT EXISTS (SELECT 1
	///         FROM table WHERE guardColumn = guardValue);
	///     </c>
	///     — one row, written once,
	///     however many times this statement runs.
	/// </summary>
	private static void AppendGuardedInsert(
		StringBuilder sql,
		string table,
		string[] columns,
		string[] values,
		string guardColumn,
		string guardValue)
	{
		sql.Append("INSERT INTO ").Append(table)
			.Append(" (").AppendJoin(", ", columns).Append(')')
			.Append(" SELECT ").AppendJoin(", ", values)
			.Append(" WHERE NOT EXISTS (SELECT 1 FROM ").Append(table)
			.Append(" WHERE ").Append(guardColumn).Append(" = ").Append(guardValue).Append(");")
			.Append('\n');
	}

	private static string Id(TinyId id)
	{
		return Str(id.Value);
	}

	private static string IdOrNull(string? key,
								   Func<string, TinyId> resolve)
	{
		return key is null ? "NULL" : Id(resolve(key));
	}

	private static string Str(string value)
	{
		return "'" + value.Replace("'", "''", StringComparison.Ordinal) + "'";
	}

	private static string StrOrNull(string? value)
	{
		return value is null ? "NULL" : Str(value);
	}

	private static string Bool(bool value)
	{
		return value ? "TRUE" : "FALSE";
	}

	private static string Int(int value)
	{
		return value.ToString(CultureInfo.InvariantCulture);
	}

	private static string Timestamp(DateTimeOffset value)
	{
		return $"TIMESTAMPTZ '{value.ToString("yyyy-MM-dd HH:mm:sszzz", CultureInfo.InvariantCulture)}'";
	}
}
