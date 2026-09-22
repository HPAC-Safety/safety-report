namespace HpacSafety.Core.Features.Moderation;

/// <summary>
///     Who is acting, for the duration of one request. Established from a
///     validated token and never written to the database.
/// </summary>
/// <param name="Subject">
///     The token's subject claim. Opaque: never parsed, never split, and never
///     assumed to be an email address. It is stored on an audit entry or a summary
///     approval as a plain string that joins to nothing, because there is no user
///     table to join to.
/// </param>
/// <param name="Role">What this identity may do.</param>
public sealed record MemberIdentity(string Subject, MemberRole Role);
