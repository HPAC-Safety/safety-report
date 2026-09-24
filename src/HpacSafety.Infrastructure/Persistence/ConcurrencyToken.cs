using HpacSafety.Core.Features.Reporting;
using Microsoft.EntityFrameworkCore;

namespace HpacSafety.Infrastructure.Persistence;

/// <summary>
///     The version a reviewer's view was loaded at, built from PostgreSQL's
///     <c>xmin</c> on the report and its summary row (ADR-0105). Kept out of the
///     domain: it is a persistence fact, mapped as a shadow property.
/// </summary>
public static class ConcurrencyToken
{
	/// <summary>The shadow property Npgsql maps to <c>xmin</c>.</summary>
	public const string PropertyName = "Version";

	/// <summary>The version of a loaded report and its summary, as one opaque string.</summary>
	public static string Of(HpacSafetyDbContext database,
							Report report)
	{
		ArgumentNullException.ThrowIfNull(database);
		ArgumentNullException.ThrowIfNull(report);

		var reportVersion = database.Entry(report).Property<uint>(PropertyName).CurrentValue;
		var summaryVersion = report.Summary is { } summary
			? database.Entry(summary).Property<uint>(PropertyName).CurrentValue
			: 0u;

		return $"{reportVersion}.{summaryVersion}";
	}

	/// <summary>
	///     Tells EF that the rows were loaded at <paramref name="expected" />, so the
	///     save fails with <see cref="DbUpdateConcurrencyException" /> if either row
	///     changed since. Returns false when the version is not one this system issued.
	/// </summary>
	public static bool Expect(HpacSafetyDbContext database,
							  Report report,
							  string? expected)
	{
		ArgumentNullException.ThrowIfNull(database);
		ArgumentNullException.ThrowIfNull(report);

		var parts = expected?.Split('.');

		if (parts is not { Length: 2 }
			|| !uint.TryParse(parts[0], out var reportVersion)
			|| !uint.TryParse(parts[1], out var summaryVersion))
		{
			return false;
		}

		database.Entry(report).Property<uint>(PropertyName).OriginalValue = reportVersion;

		if (report.Summary is { } summary)
		{
			database.Entry(summary).Property<uint>(PropertyName).OriginalValue = summaryVersion;
		}

		return true;
	}
}
