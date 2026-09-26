using HpacSafety.Core.Features.PrivateNotes;
using Shouldly;

namespace HpacSafety.Core.Tests;

/// <summary>
///     A staff-only note on a report (ADR-0133): any reviewer edits it, each edit
///     is a new revision recording its writer, and removal only soft-deletes.
/// </summary>
public sealed class PrivateNoteTests
{
	private const string Officer = "officer:synthetic";
	private const string Administrator = "admin:synthetic";
	private static readonly DateTimeOffset At = new(2026, 9, 26, 9, 0, 0, TimeSpan.Zero);

	[Fact]
	public void GivenText_WhenWritten_ThenFirstRevisionIsTrimmedWithItsWriter()
	{
		// When
		var note = Written("  Synthetic: called the reporter.  ");

		// Then
		note.Current.Number.ShouldBe(1);
		note.Current.Text.ShouldBe("Synthetic: called the reporter.");
		note.Current.AuthorSubject.ShouldBe(Officer);
		note.Current.CreatedAt.ShouldBe(At);
		note.CreatedAt.ShouldBe(At);
		note.Deleted.ShouldBeNull();
	}

	[Theory]
	[InlineData(null)]
	[InlineData("")]
	[InlineData("   ")]
	public void GivenNoText_WhenWritten_ThenRefused(string? text)
	{
		// Then
		Should.Throw<DomainRuleViolationException>(() => Written(text));
	}

	[Fact]
	public void GivenTextAtLimit_WhenWritten_ThenKept()
	{
		// Then
		Written(new string('a', PrivateNote.MaxLength)).Current.Text.Length.ShouldBe(PrivateNote.MaxLength);
	}

	[Fact]
	public void GivenTextOverLimit_WhenWritten_ThenRefused()
	{
		// Then
		Should.Throw<DomainRuleViolationException>(() => Written(new string('a', PrivateNote.MaxLength + 1)));
	}

	[Fact]
	public void GivenOfficersNote_WhenAdministratorEdits_ThenNewRevisionRecordsAdministratorAndOldIsKept()
	{
		// Given
		var note = Written("Synthetic: first.");

		// When
		var revision = note.Edit(Administrator, "Synthetic: second.", 1, At.AddMinutes(5));

		// Then
		note.Current.ShouldBeSameAs(revision);
		revision.Number.ShouldBe(2);
		revision.AuthorSubject.ShouldBe(Administrator);
		revision.CreatedAt.ShouldBe(At.AddMinutes(5));
		note.Revisions.Single(earlier => earlier.Number == 1).Text.ShouldBe("Synthetic: first.");
		note.Revisions.Single(earlier => earlier.Number == 1).AuthorSubject.ShouldBe(Officer);
	}

	[Fact]
	public void GivenEditBasedOnEarlierRevision_WhenEdited_ThenRefusedAsStale()
	{
		// Given
		var note = Written("Synthetic: first.");
		note.Edit(Administrator, "Synthetic: second.", 1, At);

		// Then
		Should.Throw<StalePrivateNoteException>(() => note.Edit(Officer, "Synthetic: from an old view.", 1, At));
		note.Revisions.Count.ShouldBe(2);
	}

	[Fact]
	public void GivenBlankEdit_WhenEdited_ThenRefusedAndNothingAdded()
	{
		// Given
		var note = Written("Synthetic: first.");

		// Then
		Should.Throw<DomainRuleViolationException>(() => note.Edit(Officer, " ", 1, At));
		note.Revisions.Count.ShouldBe(1);
	}

	[Fact]
	public void GivenEditedNote_WhenRemoved_ThenNoteAndEveryRevisionShareOneDeletedTime()
	{
		// Given
		var note = Written("Synthetic: first.");
		note.Edit(Administrator, "Synthetic: second.", 1, At);

		// When
		note.Remove(At.AddHours(1));

		// Then
		note.Deleted.ShouldBe(At.AddHours(1));
		note.Revisions.ShouldAllBe(revision => revision.Deleted == At.AddHours(1));
	}

	[Fact]
	public void GivenRemovedNote_WhenRemovedAgain_ThenFirstTimeIsKept()
	{
		// Given
		var note = Written("Synthetic: first.");
		note.Remove(At);

		// When
		note.Remove(At.AddHours(1));

		// Then
		note.Deleted.ShouldBe(At);
		note.Current.Deleted.ShouldBe(At);
	}

	[Fact]
	public void GivenRemovedNote_WhenEdited_ThenRefused()
	{
		// Given
		var note = Written("Synthetic: first.");
		note.Remove(At);

		// Then
		Should.Throw<DomainRuleViolationException>(() => note.Edit(Officer, "Synthetic: too late.", 1, At));
	}

	[Theory]
	[InlineData("")]
	[InlineData("  ")]
	public void GivenNoWriter_WhenWrittenOrEdited_ThenRefused(string writer)
	{
		// Then
		Should.Throw<ArgumentException>(() => PrivateNote.Write(TinyId.New(), writer, "Synthetic.", At));
		Should.Throw<ArgumentException>(() => Written("Synthetic.").Edit(writer, "Synthetic: second.", 1, At));
	}

	private static PrivateNote Written(string? text)
	{
		return PrivateNote.Write(TinyId.New(), Officer, text, At);
	}
}
