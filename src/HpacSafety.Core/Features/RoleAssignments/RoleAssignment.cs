using HpacSafety.Core.Features.Moderation;

namespace HpacSafety.Core.Features.RoleAssignments;

/// <summary>
///     An Administrator's grant of the Safety Officer or Administrator role to an
///     email address (ADR-0199). It holds the email, the role, and who granted it
///     and when, and nothing else about the person. Removing access revokes the
///     assignment (stamps when, and by whom) and never deletes it: the database
///     refuses a delete and any update except that one revoke of an active row
///     (CON-DP-017). Re-adding an email creates a new assignment.
/// </summary>
public class RoleAssignment
{
	/// <summary>The longest email stored (RFC 5321 path limit).</summary>
	public const int EmailMaxLength = 320;

	/// <summary>The longest token subject stored, the same width as every other subject column.</summary>
	public const int SubjectMaxLength = 256;

	// For EF Core only; see ADR-0019.
#pragma warning disable CS8618 // Every mapped property is set by EF Core immediately after this runs.
	private RoleAssignment()
	{
	}
#pragma warning restore CS8618

	private RoleAssignment(string email,
						   MemberRole role,
						   string grantedBySubject,
						   DateTimeOffset at)
	{
		Id = TinyId.New();
		Email = email;
		Role = role;
		GrantedBySubject = grantedBySubject;
		GrantedAt = at;
	}

	/// <summary>Surrogate key.</summary>
	public TinyId Id { get; private init; }

	/// <summary>The email address, always in its <see cref="Normalize" /> form.</summary>
	public string Email { get; private init; }

	/// <summary>Safety Officer or Administrator; never User.</summary>
	public MemberRole Role { get; private init; }

	/// <summary>When it was granted.</summary>
	public DateTimeOffset GrantedAt { get; private init; }

	/// <summary>The granting Administrator's token subject (opaque to this class). Under the interim issuer it is <c>member:&lt;email&gt;</c>, so it is stored and never logged (CON-IF-011).</summary>
	public string GrantedBySubject { get; private init; }

	/// <summary>When it was revoked; null while it is active.</summary>
	public DateTimeOffset? RevokedAt { get; private set; }

	/// <summary>The revoking Administrator's token subject; null exactly when <see cref="RevokedAt" /> is.</summary>
	public string? RevokedBySubject { get; private set; }

	/// <summary>Whether it still grants its role.</summary>
	public bool IsActive => RevokedAt is null;

	/// <summary>
	///     The one form an email is stored and compared in: trimmed and lower-cased
	///     (ADR-0199). Every lookup, duplicate check, and revoke goes through it.
	/// </summary>
	/// <param name="email">As typed.</param>
	public static string Normalize(string email)
	{
		ArgumentNullException.ThrowIfNull(email);

		return AsciiLower(email.Trim());
	}

	/// <summary>
	///     Lower-cases A-Z only. Culture-invariant folding also maps non-ASCII
	///     characters (U+212A Kelvin sign to k), which would let a look-alike
	///     email match another member's.
	/// </summary>
	/// <param name="value">As given.</param>
	public static string AsciiLower(string value)
	{
		ArgumentNullException.ThrowIfNull(value);

		return string.Create(value.Length, value, static (span, source) =>
		{
			for (var index = 0; index < source.Length; index++)
			{
				var character = source[index];
				span[index] = character is >= 'A' and <= 'Z' ? (char)(character + 32) : character;
			}
		});
	}

	/// <summary>Grants a role to an email.</summary>
	/// <param name="email">As typed; stored normalised.</param>
	/// <param name="role">Safety Officer or Administrator.</param>
	/// <param name="grantedBySubject">The granting Administrator's token subject.</param>
	/// <param name="at">When.</param>
	/// <exception cref="DomainRuleViolationException">The email is not an address, or the role is not assignable.</exception>
	public static RoleAssignment Grant(string email,
									   MemberRole role,
									   string grantedBySubject,
									   DateTimeOffset at)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(grantedBySubject);

		var normalised = Normalize(email ?? string.Empty);

		if (normalised.Length is < 3 or > EmailMaxLength
			|| normalised.Any(character => character > '\u007F')
			|| normalised.Any(char.IsWhiteSpace)
			|| normalised.IndexOf('@', StringComparison.Ordinal) < 1
			|| normalised.IndexOf('@', StringComparison.Ordinal) != normalised.LastIndexOf('@')
			|| normalised.EndsWith('@'))
		{
			throw new DomainRuleViolationException("A role assignment needs an email address.");
		}

		if (role is not (MemberRole.SafetyOfficer or MemberRole.Administrator))
		{
			throw new DomainRuleViolationException("A role assignment grants Safety Officer or Administrator.");
		}

		return new RoleAssignment(normalised, role, grantedBySubject, at);
	}

	/// <summary>Revokes it: stamps when, and by whom.</summary>
	/// <param name="bySubject">The revoking Administrator's token subject.</param>
	/// <param name="at">When.</param>
	/// <exception cref="DomainRuleViolationException">It was already revoked.</exception>
	public void Revoke(string bySubject,
					   DateTimeOffset at)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(bySubject);

		if (RevokedAt is not null)
		{
			throw new DomainRuleViolationException("The role assignment was already revoked.");
		}

		RevokedAt = at;
		RevokedBySubject = bySubject;
	}
}
