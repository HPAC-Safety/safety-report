using System.Reflection;

namespace HpacSafety.Infrastructure.Persistence.Sql;

/// <summary>
///     Reads a raw SQL file embedded from <c>Persistence/Sql/</c>. Raw SQL — a
///     view, a stored procedure, or a migration's data transform — is authored
///     as its own <c>.sql</c> file and loaded through here, never typed as a C#
///     string literal (ADR-0055).
/// </summary>
public static class SqlScript
{
	/// <summary>The contents of one embedded script.</summary>
	/// <param name="fileName">The file's name under <c>Persistence/Sql/</c>, extension included.</param>
	public static string Read(string fileName)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(fileName);

		using var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream($"Sql/{fileName}")
			?? throw new InvalidOperationException($"No embedded SQL script named '{fileName}'.");
		using var reader = new StreamReader(stream);
		return reader.ReadToEnd();
	}
}
