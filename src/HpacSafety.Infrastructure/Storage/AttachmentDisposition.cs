using System.Text;

namespace HpacSafety.Infrastructure.Storage;

/// <summary>
///     Builds the <c>Content-Disposition</c> a pre-signed read URL forces: always
///     <c>attachment</c>, with an ASCII <c>filename</c> for old clients and a UTF-8
///     <c>filename*</c> (RFC 6266, RFC 8187) so a reporter's accented French name
///     survives the header (ADR-0097).
/// </summary>
public static class AttachmentDisposition
{
	/// <summary>The header value for a forced download saved as <paramref name="fileName" />.</summary>
	public static string For(string fileName)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(fileName);

		var fallback = new StringBuilder(fileName.Length);
		foreach (var character in fileName)
		{
			// Quotes and backslashes would end or escape the quoted string; a
			// non-ASCII character is not allowed in it at all.
			fallback.Append(character is >= ' ' and <= '~' and not '"' and not '\\' ? character : '_');
		}

		return $"attachment; filename=\"{fallback}\"; filename*=UTF-8''{Encode(fileName)}";
	}

	// RFC 8187 attr-char is narrower than what Uri.EscapeDataString leaves
	// alone: an apostrophe would end the charset prefix, so it and the other
	// sub-delimiters are escaped too.
	private static string Encode(string value)
	{
		return Uri.EscapeDataString(value)
			.Replace("'", "%27", StringComparison.Ordinal)
			.Replace("(", "%28", StringComparison.Ordinal)
			.Replace(")", "%29", StringComparison.Ordinal)
			.Replace("*", "%2A", StringComparison.Ordinal)
			.Replace("!", "%21", StringComparison.Ordinal);
	}
}
