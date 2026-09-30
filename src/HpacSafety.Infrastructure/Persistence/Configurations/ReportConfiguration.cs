using HpacSafety.Core.Features.QuestionBank;
using HpacSafety.Core.Features.Reporting;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HpacSafety.Infrastructure.Persistence.Configurations;

/// <summary>The <c>reports</c> table and everything hanging off it.</summary>
public sealed class ReportConfiguration : IEntityTypeConfiguration<Report>
{
	/// <inheritdoc />
	public void Configure(EntityTypeBuilder<Report> builder)
	{
		ArgumentNullException.ThrowIfNull(builder);

		builder.ToTable("reports");
		builder.HasKey(report => report.Id);

		builder.Property(report => report.Language).IsRequired();
		builder.Property(report => report.Status).IsRequired();

		// Deliberately nullable, and deliberately not defaulted. An unanswered
		// consent is not a "no" — see ADR-0016.
		builder.Property(report => report.ConsentPublish);

		// Nullable for the same reason, and because the form asks it only when
		// there is media to share (ADR-0117).
		builder.Property(report => report.ConsentMedia);

		// Nullable too: only a media-consent answer to the wording that names
		// documents sets it (ADR-0119).
		builder.Property(report => report.ConsentDocuments);

		builder.Property(report => report.SummaryError).HasMaxLength(2000);

		// Reviewer-authored, reviewer-only (REQ-MOD-058).
		builder.Property(report => report.UnpublishNote).HasMaxLength(Report.UnpublishNoteMaxLength);

		// PostgreSQL's own row version: a stale review command is refused rather
		// than overwriting another reviewer's work (ADR-0105, CON-IF-006).
		builder.Property<uint>(ConcurrencyToken.PropertyName).HasColumnName("xmin").IsRowVersion();

		// The review queue reads by status, oldest first.
		builder.HasIndex(report => new { report.Status, report.SubmittedAt });

		builder.ToTable(t => t.HasCheckConstraint(
			"ck_reports_language",
			"language IN ('en-CA', 'fr-CA')"));

		builder.ToTable(t => t.HasCheckConstraint(
			"ck_reports_status",
			"status IN ('submitted', 'summarizing', 'summary_failed', 'pending', 'published', 'unpublished')"));

		builder.HasMany(report => report.Answers)
			.WithOne()
			.HasForeignKey(answer => answer.ReportId)
			.OnDelete(DeleteBehavior.Cascade);

		builder.HasMany(report => report.Files)
			.WithOne()
			.HasForeignKey(file => file.ReportId)
			.OnDelete(DeleteBehavior.Cascade);

		builder.HasOne(report => report.Summary)
			.WithOne()
			.HasForeignKey<Summary>(summary => summary.ReportId)
			.OnDelete(DeleteBehavior.Cascade);

		// The collections are backed by private fields; the Summary reference
		// is an ordinary auto-property and needs no field access mode.
		builder.Metadata.FindNavigation(nameof(Report.Answers))!.SetPropertyAccessMode(PropertyAccessMode.Field);
		builder.Metadata.FindNavigation(nameof(Report.Files))!.SetPropertyAccessMode(PropertyAccessMode.Field);
	}
}

/// <summary>The <c>report_answers</c> table.</summary>
public sealed class ReportAnswerConfiguration : IEntityTypeConfiguration<ReportAnswer>
{
	/// <inheritdoc />
	public void Configure(EntityTypeBuilder<ReportAnswer> builder)
	{
		ArgumentNullException.ThrowIfNull(builder);

		builder.ToTable("report_answers");
		builder.HasKey(answer => answer.Id);

		builder.Property(answer => answer.QuestionKey).HasMaxLength(128).IsRequired();
		builder.Property(answer => answer.IsPrivate).IsRequired();

		builder.Property(answer => answer.Locale).IsRequired();

		// Named beside `value`, the text form it stands in for (ADR-0130).
		builder.Property(answer => answer.BooleanValue).HasColumnName("value_boolean");

		// Decided once, when the answer is recorded (ADR-0112). Existing rows are
		// backfilled from their revision by the migration that added it.
		builder.Property(answer => answer.TranslationMode).IsRequired();
		builder.ToTable(t => t.HasCheckConstraint(
			"ck_report_answers_translation_mode",
			"translation_mode IN ('none', 'choice', 'machine')"));

		// A yes/no or checkbox answer is a boolean, never words, and has no second
		// language (ADR-0130). A row carries text or a boolean, never both.
		builder.ToTable(t => t.HasCheckConstraint(
			"ck_report_answers_text_or_boolean",
			"value IS NULL OR value_boolean IS NULL"));
		builder.ToTable(t => t.HasCheckConstraint(
			"ck_report_answers_boolean_has_no_words",
			"value_boolean IS NULL OR (translated_value IS NULL AND translation_source IS NULL AND translation_mode = 'none')"));

		// Not unique on (report, question): a multi-select records one row per
		// chosen value, so a report legitimately holds several answers to one
		// question (ADR-0072).
		builder.HasIndex(answer => new { answer.ReportId, answer.QuestionId });
		builder.HasIndex(answer => answer.QuestionRevisionId);

		// The translation queue: every answer awaiting machine translation (ADR-0080,
		// ADR-0112). Ordered by when it was answered, so the queue reads oldest
		// first without a sort at query time.
		builder.HasIndex(answer => answer.AnsweredAt)
			.HasFilter("value IS NOT NULL AND translated_value IS NULL AND translation_mode = 'machine' AND choice_id IS NULL");

		// Lets a report_files row enforce, at the database level, that the
		// answer it links to belongs to the same report — see
		// ReportFileConfiguration below.
		builder.HasAlternateKey(answer => new { answer.ReportId, answer.Id });

		// An answer references the revision it was answered under, and that
		// revision may never be deleted out from under it.
		builder.HasOne<QuestionRevision>()
			.WithMany()
			.HasForeignKey(answer => answer.QuestionRevisionId)
			.OnDelete(DeleteBehavior.Restrict);

		builder.HasOne<Question>()
			.WithMany()
			.HasForeignKey(answer => answer.QuestionId)
			.OnDelete(DeleteBehavior.Restrict);

		// A choice answer names its choice, which is never erased while an answer
		// names it (ADR-0128). The choice is loaded with the answer wherever its
		// wording is read.
		builder.HasOne(answer => answer.Choice)
			.WithMany()
			.HasForeignKey(answer => answer.ChoiceId)
			.OnDelete(DeleteBehavior.Restrict);
		builder.HasIndex(answer => answer.ChoiceId);
	}
}

/// <summary>
///     The <c>report_files</c> table. This project owns the table's shape; the blob
///     storage that fills it is issue #16.
/// </summary>
public sealed class ReportFileConfiguration : IEntityTypeConfiguration<ReportFile>
{
	/// <inheritdoc />
	public void Configure(EntityTypeBuilder<ReportFile> builder)
	{
		ArgumentNullException.ThrowIfNull(builder);

		builder.ToTable("report_files");
		builder.HasKey(file => file.Id);

		builder.Property(file => file.Kind).IsRequired();
		builder.Property(file => file.BlobKey).HasMaxLength(512).IsRequired();
		builder.Property(file => file.StrippedBlobKey).HasMaxLength(512);
		builder.Property(file => file.ContentType).HasMaxLength(128).IsRequired();
		builder.Property(file => file.OriginalFileName).HasMaxLength(AttachmentFileName.MaxLength);
		builder.Property(file => file.ProcessingErrorCode).HasMaxLength(128);

		// An opaque token subject, never a key (ADR-0065), and never public.
		builder.Property(file => file.HiddenBySubject).HasMaxLength(256);

		builder.HasIndex(file => file.ReportId);
		builder.HasIndex(file => new { file.ReportId, file.ReportAnswerId });

		// A file belongs to exactly one file-upload answer on the same
		// report — never one on a different report. An independent FK on
		// report_answer_id alone cannot express that: it would accept
		// ReportId = A with an answer that belongs to report B. The composite
		// FK below, against the compound alternate key on ReportAnswer, is
		// what actually enforces it. A null ReportAnswerId still satisfies
		// the constraint (Postgres MATCH SIMPLE), so the not-yet-linked
		// window before AddFile's answer is known is unaffected.
		builder.HasOne<ReportAnswer>()
			.WithMany()
			.HasForeignKey(file => new { file.ReportId, file.ReportAnswerId })
			.HasPrincipalKey(answer => new { answer.ReportId, answer.Id })
			.OnDelete(DeleteBehavior.Restrict);

		// The EXIF stripper claims work by looking for what it has not done yet.
		builder.HasIndex(file => file.ExifStrippedAt).HasFilter("exif_stripped_at IS NULL");

		builder.ToTable(t => t.HasCheckConstraint(
			"ck_report_files_kind",
			"kind IN ('image', 'video', 'document')"));

		// AwaitsStripping treats these two as one fact: a stripped-at time
		// with no key, or a key with no stripped-at time, would read as a
		// half-finished stripping nobody can act on.
		builder.ToTable(t => t.HasCheckConstraint(
			"ck_report_files_exif_stripped_coherence",
			"(exif_stripped_at IS NULL) = (stripped_blob_key IS NULL)"));

		builder.ToTable(t => t.HasCheckConstraint(
			"ck_report_files_hidden_coherence",
			"(hidden_at IS NULL) = (hidden_by_subject IS NULL)"));

		// Only a document records validation; an image or video is proven by
		// its derivative instead (ADR-0119).
		builder.ToTable(t => t.HasCheckConstraint(
			"ck_report_files_validated_document",
			"validated_at IS NULL OR kind = 'document'"));
	}
}

/// <summary>
///     The <c>summaries</c> table: exactly one row per report, holding nothing but
///     the identity its <c>summary_revisions</c> hang from and its soft deletion.
///     The text, sources, provenance, and approval all live on the revisions
///     (ADR-0177).
/// </summary>
public sealed class SummaryConfiguration : IEntityTypeConfiguration<Summary>
{
	/// <inheritdoc />
	public void Configure(EntityTypeBuilder<Summary> builder)
	{
		ArgumentNullException.ThrowIfNull(builder);

		builder.ToTable("summaries");
		builder.HasKey(summary => summary.Id);

		// Exactly one summary row per report.
		builder.HasIndex(summary => summary.ReportId).IsUnique();

		builder.HasMany(summary => summary.Revisions)
			.WithOne()
			.HasForeignKey(revision => revision.SummaryId)
			.OnDelete(DeleteBehavior.Cascade);

		// Every read of a summary wants its revisions: the domain's Latest and
		// LatestApproved are computed from them.
		builder.Navigation(summary => summary.Revisions).AutoInclude();
		builder.Metadata.FindNavigation(nameof(Summary.Revisions))!.SetPropertyAccessMode(PropertyAccessMode.Field);
	}
}

/// <summary>
///     The <c>summary_revisions</c> table: an append-only list of a summary's
///     saved versions. A stored row's text, sources, author, and lineage never
///     change; only its approval is set and cleared, and its deletion stamped
///     with its report's (ADR-0177).
/// </summary>
public sealed class SummaryRevisionConfiguration : IEntityTypeConfiguration<SummaryRevision>
{
	/// <inheritdoc />
	public void Configure(EntityTypeBuilder<SummaryRevision> builder)
	{
		ArgumentNullException.ThrowIfNull(builder);

		builder.ToTable("summary_revisions");
		builder.HasKey(revision => revision.Id);

		builder.Property(revision => revision.AiSummaryEn).IsRequired();
		builder.Property(revision => revision.AiSummaryFr).IsRequired();

		// Every published sentence traces back to exactly what produced it.
		builder.Property(revision => revision.Model).HasMaxLength(200).IsRequired();
		builder.Property(revision => revision.PromptVersion).HasMaxLength(50).IsRequired();

		// How each language was produced (ADR-0108).
		builder.Property(revision => revision.SourceEn).IsRequired();
		builder.Property(revision => revision.SourceFr).IsRequired();

		builder.ToTable(t => t.HasCheckConstraint(
			"ck_summary_revisions_source_en",
			"source_en IN ('generated', 'human', 'machine')"));

		builder.ToTable(t => t.HasCheckConstraint(
			"ck_summary_revisions_source_fr",
			"source_fr IN ('generated', 'human', 'machine')"));

		// The author and approver are token subjects, not keys. There is no user
		// table to point a foreign key at — see ADR-0065. A null author is the
		// Worker, or a revision written before authors were recorded.
		builder.Property(revision => revision.AuthorSubject).HasMaxLength(256);
		builder.Property(revision => revision.ApprovedBySubject).HasMaxLength(256);

		// The sequence orders revisions, and its uniqueness is what refuses two
		// reviewers appending the same next revision at once.
		builder.HasIndex(revision => new { revision.SummaryId, revision.Sequence }).IsUnique();
		builder.ToTable(t => t.HasCheckConstraint("ck_summary_revisions_sequence", "sequence >= 1"));

		// A rollback names the earlier revision it copies. Never cascades: an
		// earlier revision is never deleted.
		builder.HasOne<SummaryRevision>()
			.WithMany()
			.HasForeignKey(revision => revision.RestoredFromId)
			.OnDelete(DeleteBehavior.Restrict);

		// Approval is set and cleared, never half-set: Approve()/ClearApproval()
		// always change both together.
		builder.ToTable(t => t.HasCheckConstraint(
			"ck_summary_revisions_approval_coherence",
			"(approved_by_subject IS NULL) = (approved_at IS NULL)"));

		// Approval lands on this row, so it carries its own token: a stale
		// approve or unpublish is refused rather than overwriting (ADR-0105).
		builder.Property<uint>(ConcurrencyToken.PropertyName).HasColumnName("xmin").IsRowVersion();
	}
}
