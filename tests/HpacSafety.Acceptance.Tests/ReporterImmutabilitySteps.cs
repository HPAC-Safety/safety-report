using HpacSafety.Core.Features.Reporting;
using HpacSafety.Infrastructure.Persistence;
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

	[Given(@"a submitted report with answers, a file, and a summary")]
	public async Task GivenASubmittedReport()
	{
		_reportId = await BootedReports.Seed(ReportStatus.Published, true, arrange: report => BootedReports.AddProcessedImage(report));
	}

	[Given(@"a statement has set (.+) on a (report_answers|report_files|reports|summary_revisions) row")]
	public async Task GivenAStatementHasSet(string assignment,
											string table)
	{
		await Update(table, assignment);
		_refusal.ShouldBeNull("The setup write was refused.");
	}

	[When(@"a statement sets (.+) on a (report_answers|report_files|reports|summary_revisions) row")]
	public async Task WhenAStatementSets(string assignment,
										 string table)
	{
		await Update(table, assignment);
	}

	[When(@"a statement sets (.+) on that row")]
	public async Task WhenAStatementSetsOnThatRow(string assignment)
	{
		await Update(_table, assignment);
	}

	[When(@"a statement deletes a (.+) row")]
	public async Task WhenAStatementDeletes(string table)
	{
		await Run(table, $"DELETE FROM {table} WHERE id = @id");
	}

	[When(@"^a statement truncates (report_answers|report_files|reports|summary_revisions)$")]
	public async Task WhenAStatementTruncates(string table)
	{
		// CASCADE, so a foreign key cannot refuse it before the trigger does. It
		// runs in a transaction that is always rolled back: should the trigger
		// ever be missing, the shared test database still keeps every row.
		await Run(table, $"TRUNCATE {table} CASCADE", rollBack: true);
	}

	[When(@"a migration disables the reports trigger, sets language = 'fr-CA', and enables it again in one transaction")]
	public async Task WhenAMigrationChangesALockedColumn()
	{
		_rows = await PastTheImmutabilityTriggers.Write("reports", $"UPDATE reports SET language = 'fr-CA' WHERE id = {_reportId}");
		_refusal = null;
	}

	[Then(@"Postgres refuses it, naming (.+)")]
	public void ThenPostgresRefusesIt(string column)
	{
		_refusal.ShouldNotBeNull("The statement was not refused.");
		_refusal.SqlState.ShouldBe("23000");
		_refusal.MessageText.ShouldStartWith(column + " ");
	}

	[Then(@"Postgres refuses it, saying (.+) rows are never deleted")]
	public void ThenPostgresRefusesTheDelete(string table)
	{
		_refusal.ShouldNotBeNull("The statement was not refused.");
		_refusal.SqlState.ShouldBe("23000");
		_refusal.MessageText.ShouldBe($"{table} rows are never deleted");
	}

	[Then(@"the row is as it was")]
	public async Task ThenTheRowIsAsItWas()
	{
		(await Fingerprint()).ShouldBe(_fingerprint);
	}

	[Then(@"the write succeeds")]
	public void ThenTheWriteSucceeds()
	{
		_refusal.ShouldBeNull($"The statement was refused: {_refusal?.MessageText}");
		_rows.ShouldBe(1);
	}

	[Then(@"the row now reads differently")]
	public async Task ThenTheRowNowReadsDifferently()
	{
		(await Fingerprint()).ShouldNotBe(_fingerprint);
	}

	[Then(@"the report's language is fr-CA")]
	public async Task ThenTheReportsLanguageIsFrCa()
	{
		(await Scalar("SELECT language FROM reports WHERE id = @id", _reportId)).ShouldBe("fr-CA");
	}

	[Then(@"a later statement setting language = 'en-CA' on a reports row is refused, naming reports.language")]
	public async Task ThenALaterStatementIsRefused()
	{
		await Update("reports", "language = 'en-CA'");
		ThenPostgresRefusesIt("reports.language");
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

		var factory = await BootedApi.Factory();
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

	private static async Task<string?> Scalar(string sql,
											  string id)
	{
		var factory = await BootedApi.Factory();
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
