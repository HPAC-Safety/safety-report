using HpacSafety.Core.Features.PrivateAttachments;
using HpacSafety.Core.Features.PrivateNotes;
using HpacSafety.Core.Features.Reporting;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HpacSafety.Infrastructure.Persistence.Configurations;

/// <summary>
///     The <c>report_private_notes</c> table: staff-only notes on a report
///     (ADR-0133). No view, public query, or Worker reads it.
/// </summary>
public sealed class PrivateNoteConfiguration : IEntityTypeConfiguration<PrivateNote>
{
	/// <inheritdoc />
	public void Configure(EntityTypeBuilder<PrivateNote> builder)
	{
		ArgumentNullException.ThrowIfNull(builder);

		builder.ToTable("report_private_notes");
		builder.HasKey(note => note.Id);

		// A report's notes, newest first, is the one read.
		builder.HasIndex(note => new { note.ReportId, note.CreatedAt });

		// A report is never physically deleted, and a note must never outlive a
		// report row it points at.
		builder.HasOne<Report>()
			.WithMany()
			.HasForeignKey(note => note.ReportId)
			.OnDelete(DeleteBehavior.Restrict);

		builder.HasMany(note => note.Revisions)
			.WithOne()
			.HasForeignKey(revision => revision.NoteId)
			.OnDelete(DeleteBehavior.Cascade);

		builder.Ignore(note => note.Current);
		builder.Metadata.FindNavigation(nameof(PrivateNote.Revisions))!.SetPropertyAccessMode(PropertyAccessMode.Field);
	}
}

/// <summary>The <c>report_private_note_revisions</c> table: every version of a note, with its writer.</summary>
public sealed class PrivateNoteRevisionConfiguration : IEntityTypeConfiguration<PrivateNoteRevision>
{
	/// <inheritdoc />
	public void Configure(EntityTypeBuilder<PrivateNoteRevision> builder)
	{
		ArgumentNullException.ThrowIfNull(builder);

		builder.ToTable("report_private_note_revisions");
		builder.HasKey(revision => revision.Id);

		builder.Property(revision => revision.Text).HasMaxLength(PrivateNote.MaxLength).IsRequired();

		// An opaque token subject, never a key (ADR-0065).
		builder.Property(revision => revision.AuthorSubject).HasMaxLength(PrivateNote.SubjectMaxLength).IsRequired();

		// Two reviewers editing the same revision at once: one wins, the other is
		// refused rather than both writing the same number.
		builder.HasIndex(revision => new { revision.NoteId, revision.Number }).IsUnique();

		// The private attachment a revision refers to (ADR-0135). Never physically
		// deleted, so a revision's reference always resolves, even once removed.
		// That it is on the note's own report is the domain's rule.
		builder.HasOne<PrivateAttachment>()
			.WithMany()
			.HasForeignKey(revision => revision.AttachmentId)
			.OnDelete(DeleteBehavior.Restrict);
		builder.HasIndex(revision => revision.AttachmentId);

		builder.ToTable(t => t.HasCheckConstraint("ck_report_private_note_revisions_number", "number >= 1"));
	}
}
