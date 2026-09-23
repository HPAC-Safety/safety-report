using System.Security.Cryptography;

namespace HpacSafety.Core;

/// <summary>
///     The name of one reporter upload waiting in quarantine: twenty-two characters
///     of URL-safe base64, carrying 128 bits from a cryptographically secure source.
/// </summary>
/// <remarks>
///     <para>
///         Not a <see cref="TinyId" />, on purpose. A tiny id names a row, and its
///         sixty-six bits only have to avoid collisions. An upload has no row: its id is
///         the only thing that lets anyone name, delete, or claim the file, so it is a
///         capability, and a capability has to be unguessable. See ADR-0096.
///     </para>
///     <para>
///         It carries nothing about who uploaded it (ADR-0067) or when.
///     </para>
/// </remarks>
public readonly record struct UploadId
{
	/// <summary>How many characters an upload id has. Never more, never fewer.</summary>
	public const int Length = 22;

	private const int EntropyBytes = 16;

	private readonly string? _value;

	private UploadId(string value)
	{
		_value = value;
	}

	/// <summary>The upload id as text. Empty for a default-constructed value.</summary>
	public string Value => _value ?? string.Empty;

	/// <summary>Mints a new upload id from a cryptographically secure source.</summary>
	public static UploadId New()
	{
		// 16 bytes is 128 bits, which base64 writes as 22 symbols and two padding
		// characters. The padding carries nothing and is dropped.
		var encoded = Convert.ToBase64String(RandomNumberGenerator.GetBytes(EntropyBytes))
			.TrimEnd('=')
			.Replace('+', '-')
			.Replace('/', '_');

		return new UploadId(encoded);
	}

	/// <summary>Reads an upload id back from text, without throwing.</summary>
	/// <param name="candidate">The text to read.</param>
	/// <param name="id">The upload id, if the text was one.</param>
	public static bool TryParse(string? candidate,
								out UploadId id)
	{
		id = default;

		if (candidate is null
			|| candidate.Length != Length)
		{
			return false;
		}

		foreach (var character in candidate)
		{
			if (!TinyId.Alphabet.Contains(character, StringComparison.Ordinal))
			{
				return false;
			}
		}

		id = new UploadId(candidate);
		return true;
	}

	/// <inheritdoc />
	public override string ToString()
	{
		return Value;
	}
}
