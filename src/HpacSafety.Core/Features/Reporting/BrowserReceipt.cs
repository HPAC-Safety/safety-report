using System.Buffers.Text;
using System.Security.Cryptography;

namespace HpacSafety.Core.Features.Reporting;

/// <summary>
///     The receipt a successful submission hands the reporter's browser: 256 random
///     bits, base64url. The report keeps only the SHA-256 hash of it, so the
///     server can recognise a receipt it is shown and cannot produce one
///     (ADR-0196).
/// </summary>
/// <remarks>
///     It proves "this browser filed it", never "this member filed it": nothing about
///     the member, the report, or the request goes into it, and the same member
///     filing twice gets two unrelated receipts. A hash of a random token cannot be
///     reversed by trying every member, which is the objection ADR-0067 made to
///     hashing the subject.
/// </remarks>
public static class BrowserReceipt
{
	/// <summary>How many random bytes a receipt carries: 256 bits.</summary>
	public const int EntropyBytes = 32;

	/// <summary>How many characters a receipt, and a receipt's hash, have in base64url.</summary>
	public const int Length = 43;

	/// <summary>Mints a new receipt from a cryptographically secure source.</summary>
	/// <returns>The receipt text, for the browser, and its hash, for the report.</returns>
	public static (string Receipt, string Hash) New()
	{
		var bytes = RandomNumberGenerator.GetBytes(EntropyBytes);

		return (Base64Url.EncodeToString(bytes), Base64Url.EncodeToString(SHA256.HashData(bytes)));
	}

	/// <summary>
	///     Hashes a receipt a browser sent, without throwing. False for anything that
	///     is not exactly a receipt's shape, which can never match a stored hash.
	/// </summary>
	/// <param name="receipt">The text the browser sent.</param>
	/// <param name="hash">The hash that a report would store for it.</param>
	public static bool TryHash(string? receipt,
							   out string hash)
	{
		hash = string.Empty;

		if (receipt is null
			|| receipt.Length != Length
			|| !Base64Url.IsValid(receipt))
		{
			return false;
		}

		var bytes = Base64Url.DecodeFromChars(receipt);

		if (bytes.Length != EntropyBytes)
		{
			return false;
		}

		hash = Base64Url.EncodeToString(SHA256.HashData(bytes));
		return true;
	}
}
