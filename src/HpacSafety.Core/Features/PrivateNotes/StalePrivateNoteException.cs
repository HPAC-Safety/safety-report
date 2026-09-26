namespace HpacSafety.Core.Features.PrivateNotes;

/// <summary>
///     An edit was based on a revision that is no longer a private note's latest:
///     another reviewer edited it since (ADR-0133).
/// </summary>
public sealed class StalePrivateNoteException()
	: Exception("This private note changed since it was opened.");
