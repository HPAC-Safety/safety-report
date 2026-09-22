namespace HpacSafety.Api.Authentication;

/// <summary>
///     Configuration for <see cref="MembersSiteCredentialSource" />, bound from
///     the <c>MembersSiteLogin</c> configuration section. Development-only —
///     see ADR-0079.
/// </summary>
/// <remarks>
///     The email lists here are a throwaway, Development-only convenience —
///     the same shape as <see cref="HpacAuthenticationOptions.DevelopmentSigningKey" />
///     (ADR-0066). They name nobody's production role and are never consulted
///     outside Development.
/// </remarks>
public sealed class MembersSiteLoginOptions
{
	/// <summary>The configuration section this binds from.</summary>
	public const string SectionName = "MembersSiteLogin";

	/// <summary>The members site to verify a login against.</summary>
	public string BaseUrl { get; set; } = "https://members.hpac.ca";

	/// <summary>How long to wait for the members site before giving up.</summary>
	public TimeSpan Timeout { get; set; } = TimeSpan.FromSeconds(10);

	/// <summary>
	///     Emails that resolve to <see cref="Core.Features.Moderation.MemberRole.Administrator" />
	///     when the members site verifies them. Checked before
	///     <see cref="SafetyOfficerEmails" />.
	/// </summary>
	public IReadOnlyList<string> AdministratorEmails { get; set; } = [];

	/// <summary>
	///     Emails that resolve to <see cref="Core.Features.Moderation.MemberRole.SafetyOfficer" />
	///     when the members site verifies them and the email is not on
	///     <see cref="AdministratorEmails" />.
	/// </summary>
	public IReadOnlyList<string> SafetyOfficerEmails { get; set; } = [];
}
