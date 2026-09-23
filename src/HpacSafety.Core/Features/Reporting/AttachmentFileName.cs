using System.Globalization;
using System.Text;

namespace HpacSafety.Core.Features.Reporting;

/// <summary>
///     The reporter's own name for an attachment: sanitized once, on the way in, and
///     used for exactly one thing — the name a reviewer's download saves as
///     (ADR-0097).
/// </summary>
/// <remarks>
///     A filename is reporter-supplied text that ends up in a response header, so it
///     is treated the way any such text is: only the last path segment survives, and
///     every character that could break out of a header, a quoted string, or a file
///     system is removed rather than escaped.
/// </remarks>
public static class AttachmentFileName
{
	/// <summary>The longest name kept, in characters.</summary>
	public const int MaxLength = 255;

	private const string Removed = "\"\\/:*?<>|;";

	/// <summary>
	///     Sanitizes a reporter-supplied name, or returns <see langword="null" /> when
	///     nothing usable remains.
	/// </summary>
	public static string? Sanitize(string? candidate)
	{
		if (string.IsNullOrWhiteSpace(candidate))
		{
			return null;
		}

		// A browser sends only the base name, but nothing obliges a client to.
		var lastSeparator = candidate.LastIndexOfAny(['/', '\\']);
		var baseName = lastSeparator >= 0 ? candidate[(lastSeparator + 1)..] : candidate;

		var cleaned = new StringBuilder(baseName.Length);
		var pendingSpace = false;

		foreach (var character in baseName)
		{
			// Format characters include the bidirectional overrides that can make
			// "photo\u202Egpj.exe" display as "photoexe.jpg".
			if (char.IsControl(character)
				|| Removed.Contains(character, StringComparison.Ordinal)
				|| char.GetUnicodeCategory(character) == UnicodeCategory.Format)
			{
				continue;
			}

			if (char.IsWhiteSpace(character))
			{
				pendingSpace = cleaned.Length > 0;
				continue;
			}

			if (pendingSpace)
			{
				cleaned.Append(' ');
				pendingSpace = false;
			}

			cleaned.Append(character);
		}

		// Leading dots would make a hidden file, and "." or ".." name no file.
		var result = cleaned.ToString().TrimStart('.');

		if (result.Length > MaxLength)
		{
			result = result[..MaxLength];

			// Never cut a surrogate pair in half.
			if (char.IsHighSurrogate(result[^1]))
			{
				result = result[..^1];
			}
		}

		return result.Length == 0 ? null : result;
	}

	/// <summary>
	///     The name a reviewer's download saves as: the reporter's stem with the
	///     extension of the bytes actually served, or <c>&lt;file id&gt;.&lt;ext&gt;</c>
	///     when the reporter's name is unknown. The extension always follows the
	///     served type, so a name can never make a file look like something it is not.
	/// </summary>
	public static string ForDownload(string? originalFileName,
									 TinyId fileId,
									 MediaType served)
	{
		var stem = Sanitize(originalFileName) is { } name
			? Path.GetFileNameWithoutExtension(name)
			: null;

		if (string.IsNullOrWhiteSpace(stem))
		{
			stem = fileId.Value;
		}

		var extension = $".{served.Extension}";
		if (stem.Length + extension.Length > MaxLength)
		{
			stem = stem[..(MaxLength - extension.Length)];
		}

		return stem + extension;
	}
}
