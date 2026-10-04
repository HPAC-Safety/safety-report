using HpacSafety.Core.Features.Reporting;
using HpacSafety.Infrastructure.Persistence;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using Reqnroll;
using Shouldly;

namespace HpacSafety.Acceptance.Tests;

/// <summary>
///     The database refuses changes to the reporter's account and to summary
///     revisions — REQ-DOM-018 to REQ-DOM-030 (#669, ADR-0178). Every statement
///     is real SQL against the booted host's real PostgreSQL, past the domain, so
///     what is proven is the trigger and not the entity's <c>private init</c>. A
///     report's own soft-delete cascade is REQ-DOM-007 in
///     <see cref="DomainAndLifecycleSteps" />, which runs against the same triggers.
/// </summary>
[Binding]
public sealed class ReporterImmutabilitySteps
{
#pragma warning disable CA1822 // Reqnroll step bindings must be instance methods to be discovered.

	private string _reportId = string.Empty;
	private string _table = string.Empty;
	private string _rowId = string.Empty;
	private string _fingerprint = string.Empty;
	private PostgresException? _refusal;
	private int? _rows;
	private ScratchHost? _scratch;
	private Guarded.StoredPart? _part;

	[Given(@"a submitted report with answers, a file, and a summary")]
	public async Task GivenASubmittedReport()
	{
		_reportId = await BootedReports.Seed(ReportStatus.Published, true, arrange: report => BootedReports.AddProcessedImage(report));
	}

	[Given(@"^a stored (answer|attachment|report|summary revision)'s (.+) has been written$")]
	public async Task GivenAStoredPartHasBeenWritten(string record,
													 string part)
	{
		_part = Guarded.Part(record, part);
		await Update(_part.Table, _part.Written());
		_refusal.ShouldBeNull("The setup write was refused.");
	}

	[When(@"^a stored (answer|attachment|report|summary revision)'s (.+) is (changed|cleared|written)$")]
	public async Task WhenAStoredPartIsChanged(string record,
											   string part,
											   string change)
	{
		_part = Guarded.Part(record, part);
		await Update(_part.Table, _part.Assignment(change));
	}

	[When(@"^it is (changed|cleared)$")]
	public async Task WhenItIsChanged(string change)
	{
		var part = _part ?? throw new InvalidOperationException("No stored part was written first.");
		await Update(part.Table, part.Assignment(change));
	}

	[When(@"^a stored (report|answer|attachment|summary revision) is erased$")]
	public async Task WhenAStoredRecordIsErased(string record)
	{
		var table = Guarded.Table(record);
		await Run(table, $"DELETE FROM {table} WHERE id = @id");
	}

	[When(@"^every stored (report|answer|attachment|summary revision) is erased at once$")]
	public async Task WhenEveryStoredRecordIsErased(string record)
	{
		var table = Guarded.Table(record);
		// CASCADE, so a foreign key cannot refuse it before the trigger does. It
		// runs in a transaction that is always rolled back: should the trigger
		// ever be missing, the database still keeps every row.
		//
		// On a database of its own, never the shared one. TRUNCATE takes an ACCESS
		// EXCLUSIVE lock on the table and, through CASCADE, on every table pointing
		// at it, before any trigger runs. Against the shared database that queued
		// behind, and deadlocked with, whichever scenario was inserting a child row
		// (a private attachment locks its own table, then wants a share lock on its
		// report), failing that scenario with 40P01 (#741).
		_scratch ??= await BootedApi.Scratch();
		_reportId = await BootedReports.Seed(ReportStatus.Published, true, report => BootedReports.AddProcessedImage(report), host: _scratch.Host);
		_table = string.Empty;
		await Run(table, $"TRUNCATE {table} CASCADE", rollBack: true);
	}

	[AfterScenario]
	public async Task DropTheScratchDatabase()
	{
		if (_scratch is not null)
		{
			await _scratch.DisposeAsync();
			_scratch = null;
		}
	}

	[When(@"a stored report's language and submission time are written back unchanged, with a new review state")]
	public async Task WhenALockedPartIsWrittenBackUnchanged()
	{
		await Update("reports", "language = language, submitted_at = submitted_at, status = 'unpublished'");
	}

	[When(@"a migration lifts the report's guard, changes its language to French, and restores the guard in one transaction")]
	public async Task WhenAMigrationChangesALockedColumn()
	{
		_rows = await PastTheImmutabilityTriggers.Write("reports", $"UPDATE reports SET language = 'fr-CA' WHERE id = {_reportId}");
		_refusal = null;
	}

	/// <summary>The trigger refused the write with SQLSTATE 23000, its message naming the guarded column (ADR-0178).</summary>
	[Then(@"the change is refused, naming that part")]
	public void ThenTheChangeIsRefused()
	{
		var part = _part ?? throw new InvalidOperationException("No stored part was changed.");
		_refusal.ShouldNotBeNull("The statement was not refused.");
		_refusal.SqlState.ShouldBe("23000");
		_refusal.MessageText.ShouldStartWith($"{part.Table}.{part.Column} ");
	}

	/// <summary>The trigger refused a DELETE or TRUNCATE with SQLSTATE 23000: "&lt;table&gt; rows are never deleted".</summary>
	[Then(@"the erasure is refused, saying that kind of record is never erased")]
	public void ThenTheErasureIsRefused()
	{
		_refusal.ShouldNotBeNull("The statement was not refused.");
		_refusal.SqlState.ShouldBe("23000");
		_refusal.MessageText.ShouldBe($"{_table} rows are never deleted");
	}

	[Then(@"^the (?:report|answer|attachment|summary revision) is as it was$")]
	public async Task ThenTheRowIsAsItWas()
	{
		(await Fingerprint()).ShouldBe(_fingerprint);
	}

	[Then(@"the change is kept")]
	public void ThenTheWriteSucceeds()
	{
		_refusal.ShouldBeNull($"The statement was refused: {_refusal?.MessageText}");
		_rows.ShouldBe(1);
	}

	[Then(@"^the (?:report|answer|attachment|summary revision) now reads differently$")]
	public async Task ThenTheRowNowReadsDifferently()
	{
		(await Fingerprint()).ShouldNotBe(_fingerprint);
	}

	[Then(@"the report's language is French")]
	public async Task ThenTheReportsLanguageIsFrCa()
	{
		(await Scalar("SELECT language FROM reports WHERE id = @id", _reportId)).ShouldBe("fr-CA");
	}

	[Then(@"a later change to the report's language is refused, naming that part")]
	public async Task ThenALaterStatementIsRefused()
	{
		_part = Guarded.Part("report", "language");
		await Update("reports", "language = 'en-CA'");
		ThenTheChangeIsRefused();
	}

	/// <summary>The host the scenario's row lives on: its own scratch database once a truncation has moved it, else the shared one.</summary>
	private async Task<WebApplicationFactory<Program>> HostOfTheRow()
	{
		return _scratch?.Host ?? await BootedApi.Factory();
	}

	private async Task Update(string table,
							  string assignment)
	{
		await Run(table, $"UPDATE {table} SET {assignment} WHERE id = @id");
	}

	private async Task Run(string table,
						   string sql,
						   bool rollBack = false)
	{
		if (_table != table)
		{
			_table = table;
			_rowId = await Scalar(RowQuery(table), _reportId) ?? throw new InvalidOperationException($"The seeded report has no {table} row.");
		}

		_fingerprint = await Fingerprint();
		_refusal = null;
		_rows = null;

		var factory = await HostOfTheRow();
		await using var scope = factory.Services.CreateAsyncScope();
		var database = scope.ServiceProvider.GetRequiredService<HpacSafetyDbContext>();

		await using var transaction = rollBack ? await database.Database.BeginTransactionAsync() : null;
		try
		{
			_rows = await database.Database.ExecuteSqlRawAsync(sql, new NpgsqlParameter("id", _rowId));
		}
		catch (PostgresException refused)
		{
			_refusal = refused;
		}
		finally
		{
			if (transaction is not null)
			{
				await transaction.RollbackAsync();
			}
		}
	}

	/// <summary>The one row the scenario writes to: for an answer, the reporter's own words.</summary>
	private static string RowQuery(string table)
	{
		return table switch
		{
			"reports" => "SELECT id FROM reports WHERE id = @id",
			"report_answers" => "SELECT id FROM report_answers WHERE report_id = @id AND value IS NOT NULL ORDER BY id LIMIT 1",
			"report_files" => "SELECT id FROM report_files WHERE report_id = @id ORDER BY id LIMIT 1",
			"summary_revisions" =>
				"SELECT revision.id FROM summary_revisions AS revision JOIN summaries AS summary ON summary.id = revision.summary_id WHERE summary.report_id = @id ORDER BY revision.sequence DESC LIMIT 1",
			_ => throw new ArgumentOutOfRangeException(nameof(table), table, "Not a table the immutability triggers guard."),
		};
	}

	private async Task<string> Fingerprint()
	{
		return _table.Length == 0
			? string.Empty
			: await Scalar($"SELECT to_jsonb(subject)::text FROM {_table} AS subject WHERE id = @id", _rowId) ?? string.Empty;
	}

	private async Task<string?> Scalar(string sql,
									   string id)
	{
		var factory = await HostOfTheRow();
		await using var scope = factory.Services.CreateAsyncScope();
		var database = scope.ServiceProvider.GetRequiredService<HpacSafetyDbContext>();
		var connection = database.Database.GetDbConnection();
		await database.Database.OpenConnectionAsync();

		await using var command = connection.CreateCommand();
		command.CommandText = sql;
		var parameter = command.CreateParameter();
		parameter.ParameterName = "id";
		parameter.Value = id;
		command.Parameters.Add(parameter);
		return (await command.ExecuteScalarAsync())?.ToString();
	}
}

/// <summary>
///     The stored parts REQ-DOM-018 to REQ-DOM-029 name in the reader's words,
///     each mapped to the table and column the immutability triggers guard and
///     to the SQL that changes, clears, or writes it (ADR-0178, J13 on #815).
///     The column names live here and in CON-DP-013 to CON-DP-016, not in the
///     scenarios.
/// </summary>
internal static class Guarded
{
	/// <summary>One stored part: its table and column, and each write a scenario makes to it.</summary>
	internal sealed record StoredPart(string Table, string Column, string? Changed = null, string? Cleared = null, string? Write = null)
	{
		public string Assignment(string change)
		{
			return change switch
			{
				"changed" => Changed,
				"cleared" => Cleared,
				"written" => Write,
				_ => null,
			} ?? throw new ArgumentOutOfRangeException(nameof(change), change, $"No {change} write for {Table}.{Column}.");
		}

		public string Written()
		{
			return Assignment("written");
		}
	}

	private static readonly Dictionary<(string Record, string Part), StoredPart> Parts = new()
	{
		[("answer", "identifier")] = new("report_answers", "id", "id = 'xxxxxxxxxx1'"),
		[("answer", "report")] = new("report_answers", "report_id", "report_id = 'xxxxxxxxxx1'"),
		[("answer", "question")] = new("report_answers", "question_id", "question_id = 'xxxxxxxxxx1'"),
		[("answer", "question revision")] = new("report_answers", "question_revision_id", "question_revision_id = 'xxxxxxxxxx1'"),
		[("answer", "question key")] = new("report_answers", "question_key", "question_key = 'another_key'"),
		[("answer", "privacy")] = new("report_answers", "is_private", "is_private = NOT is_private"),
		[("answer", "wording")] = new("report_answers", "value", "value = 'A different account.'", "value = NULL"),
		[("answer", "yes or no")] = new("report_answers", "value_boolean", "value_boolean = true"),
		[("answer", "choice")] = new("report_answers", "choice_id", "choice_id = 'xxxxxxxxxx1'"),
		[("answer", "language")] = new("report_answers", "locale", "locale = 'fr-CA'"),
		[("answer", "translation need")] = new("report_answers", "translation_mode", "translation_mode = 'machine'"),
		[("answer", "answer time")] = new("report_answers", "answered_at", "answered_at = answered_at + interval '1 day'"),
		[("answer", "second language")] = new("report_answers", "translated_value", "translated_value = 'Second.'", "translated_value = NULL", "translated_value = 'First.'"),
		[("answer", "second language source")] = new("report_answers", "translation_source", "translation_source = 'human'", "translation_source = NULL", "translation_source = 'auto'"),
		[("answer", "deletion time")] = new("report_answers", "deleted", "deleted = now() + interval '1 day'", "deleted = NULL", "deleted = now()"),

		[("attachment", "identifier")] = new("report_files", "id", "id = 'xxxxxxxxxx1'"),
		[("attachment", "report")] = new("report_files", "report_id", "report_id = 'xxxxxxxxxx1'"),
		[("attachment", "answer")] = new("report_files", "report_answer_id", "report_answer_id = 'xxxxxxxxxx1'"),
		[("attachment", "kind")] = new("report_files", "kind", "kind = 'video'"),
		[("attachment", "stored original")] = new("report_files", "blob_key", "blob_key = 'another/original/key'"),
		[("attachment", "file name")] = new("report_files", "original_file_name", "original_file_name = 'another.jpg'"),
		[("attachment", "content type")] = new("report_files", "content_type", "content_type = 'image/png'"),
		[("attachment", "size")] = new("report_files", "byte_size", "byte_size = byte_size + 1"),
		[("attachment", "upload time")] = new("report_files", "uploaded_at", "uploaded_at = uploaded_at + interval '1 day'"),
		[("attachment", "stripped copy")] = new("report_files", "stripped_blob_key", Cleared: "exif_stripped_at = NULL, stripped_blob_key = NULL", Write: "stripped_blob_key = 'report/stripped/other', exif_stripped_at = now()"),
		[("attachment", "processing error")] = new("report_files", "processing_error_code", Write: "processing_error_code = 'unreadable'"),
		[("attachment", "hidden state")] = new("report_files", "hidden_at", Write: "hidden_at = now(), hidden_by_subject = 'synthetic-officer'"),
		[("attachment", "deletion time")] = new("report_files", "deleted", Write: "deleted = now()"),

		[("report", "identifier")] = new("reports", "id", "id = 'xxxxxxxxxx1'"),
		[("report", "language")] = new("reports", "language", "language = 'fr-CA'"),
		[("report", "submission time")] = new("reports", "submitted_at", "submitted_at = submitted_at + interval '1 day'"),
		[("report", "publication consent")] = new("reports", "consent_publish", "consent_publish = NOT consent_publish", "consent_publish = NULL"),
		[("report", "media consent")] = new("reports", "consent_media", "consent_media = true"),
		[("report", "document consent")] = new("reports", "consent_documents", "consent_documents = true"),
		[("report", "review state")] = new("reports", "status", "status = 'unpublished'"),
		[("report", "publish time")] = new("reports", "published_at", Write: "published_at = now()"),
		[("report", "unpublish note")] = new("reports", "unpublish_note", Write: "unpublish_note = 'Out of scope.'"),
		[("report", "summary error")] = new("reports", "summary_error", Write: "summary_error = 'Provider was down'"),
		[("report", "deletion time")] = new("reports", "deleted", Write: "deleted = now()"),

		[("summary revision", "identifier")] = new("summary_revisions", "id", "id = 'xxxxxxxxxx1'"),
		[("summary revision", "summary")] = new("summary_revisions", "summary_id", "summary_id = 'xxxxxxxxxx1'"),
		[("summary revision", "number")] = new("summary_revisions", "sequence", "sequence = sequence + 1"),
		[("summary revision", "English text")] = new("summary_revisions", "ai_summary_en", "ai_summary_en = 'Rewritten.'"),
		[("summary revision", "French text")] = new("summary_revisions", "ai_summary_fr", "ai_summary_fr = 'Réécrit.'"),
		[("summary revision", "English source")] = new("summary_revisions", "source_en", "source_en = 'human'"),
		[("summary revision", "French source")] = new("summary_revisions", "source_fr", "source_fr = 'human'"),
		[("summary revision", "model")] = new("summary_revisions", "model", "model = 'another-model'"),
		[("summary revision", "prompt version")] = new("summary_revisions", "prompt_version", "prompt_version = 'another.v9'"),
		[("summary revision", "author")] = new("summary_revisions", "author_subject", "author_subject = 'someone-else'"),
		[("summary revision", "creation time")] = new("summary_revisions", "created_at", "created_at = created_at + interval '1 day'"),
		[("summary revision", "restored revision")] = new("summary_revisions", "restored_from_id", "restored_from_id = 'xxxxxxxxxx1'"),
		[("summary revision", "approval")] = new("summary_revisions", "approved_at", Cleared: "approved_at = NULL, approved_by_subject = NULL", Write: "approved_at = now(), approved_by_subject = 'another-approver'"),
		[("summary revision", "deletion time")] = new("summary_revisions", "deleted", Cleared: "deleted = NULL", Write: "deleted = now()"),
	};

	/// <summary>The guarded table a record kind is stored in.</summary>
	public static string Table(string record)
	{
		return record switch
		{
			"report" => "reports",
			"answer" => "report_answers",
			"attachment" => "report_files",
			"summary revision" => "summary_revisions",
			_ => throw new ArgumentOutOfRangeException(nameof(record), record, "Not a record the immutability triggers guard."),
		};
	}

	/// <summary>The stored part a scenario names.</summary>
	public static StoredPart Part(string record,
								  string part)
	{
		return Parts.TryGetValue((record, part), out var found)
			? found
			: throw new ArgumentOutOfRangeException(nameof(part), part, $"No guarded part \"{part}\" of a stored {record}.");
	}
}

/// <summary>
///     Writes to a locked column the one way the database allows it: the way a
///     migration would, by disabling the table's immutability trigger, making the
///     change, and enabling it again, all in one transaction (ADR-0178). The
///     scenarios that build an impossible row on purpose — to prove a view still
///     refuses it — use this instead of a bypass the database does not have.
/// </summary>
internal static class PastTheImmutabilityTriggers
{
	/// <summary>Runs one write with the table's trigger off, and the trigger back on before the transaction ends.</summary>
	/// <param name="table">The guarded table the statement writes to.</param>
	/// <param name="sql">The one write.</param>
	public static async Task<int> Write(string table,
										FormattableString sql)
	{
		// The table is one of this suite's own four literals, never input.
		var (disable, enable) = table switch
		{
			"reports" => ("ALTER TABLE reports DISABLE TRIGGER reports_immutable", "ALTER TABLE reports ENABLE TRIGGER reports_immutable"),
			"report_answers" => ("ALTER TABLE report_answers DISABLE TRIGGER report_answers_immutable", "ALTER TABLE report_answers ENABLE TRIGGER report_answers_immutable"),
			"report_files" => ("ALTER TABLE report_files DISABLE TRIGGER report_files_immutable", "ALTER TABLE report_files ENABLE TRIGGER report_files_immutable"),
			"summary_revisions" => ("ALTER TABLE summary_revisions DISABLE TRIGGER summary_revisions_immutable", "ALTER TABLE summary_revisions ENABLE TRIGGER summary_revisions_immutable"),
			_ => throw new ArgumentOutOfRangeException(nameof(table), table, "Not a table the immutability triggers guard."),
		};

		var factory = await BootedApi.Factory();
		await using var scope = factory.Services.CreateAsyncScope();
		var database = scope.ServiceProvider.GetRequiredService<HpacSafetyDbContext>();

		await using var transaction = await database.Database.BeginTransactionAsync();
		await database.Database.ExecuteSqlRawAsync(disable);
		var rows = await database.Database.ExecuteSqlInterpolatedAsync(sql);
		await database.Database.ExecuteSqlRawAsync(enable);
		await transaction.CommitAsync();
		return rows;
	}
}
