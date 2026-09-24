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
		builder.Property(report => report.RejectionNote).HasMaxLength(Report.RejectionNoteMaxLength);

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
			"status IN ('submitted', 'summarizing', 'pending_review', 'summary_failed', 'approved', 'rejected', 'published')"));

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

		// Decided once, when the answer is recorded (ADR-0112). Existing rows are
		// backfilled from their revision by the migration that added it.
		builder.Property(answer => answer.TranslationMode).IsRequired();
		builder.ToTable(t => t.HasCheckConstraint(
			"ck_report_answers_translation_mode",
			"translation_mode IN ('none', 'choice', 'machine')"));

		// Not unique on (report, question): a multi-select records one row per
		// chosen value, so a report legitimately holds several answers to one
		// question (ADR-0072).
		builder.HasIndex(answer => new { answer.ReportId, answer.QuestionId });
		builder.HasIndex(answer => answer.QuestionRevisionId);

		// The translation queue: every answer awaiting machine translation (ADR-0080,
		// ADR-0112). Ordered by when it was answered, so the queue reads oldest
		// first without a sort at query time.
		builder.HasIndex(answer => answer.AnsweredAt)
			.HasFilter("value IS NOT NULL AND translated_value IS NULL AND translation_mode = 'machine'");

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
///     The <c>summaries</c> table. Exactly one bilingual row per report, with shared
///     provenance and one approval covering both languages.
/// </summary>
public sealed class SummaryConfiguration : IEntityTypeConfiguration<Summary>
{
	/// <inheritdoc />
	public void Configure(EntityTypeBuilder<Summary> builder)
	{
		ArgumentNullException.ThrowIfNull(builder);

		builder.ToTable("summaries");
		builder.HasKey(summary => summary.Id);

		builder.Property(summary => summary.AiSummaryEn).IsRequired();
		builder.Property(summary => summary.AiSummaryFr).IsRequired();

		// Every published sentence traces back to exactly what produced it.
		builder.Property(summary => summary.Model).HasMaxLength(200).IsRequired();
		builder.Property(summary => summary.PromptVersion).HasMaxLength(50).IsRequired();

		// Exactly one summary row per report.
		builder.HasIndex(summary => summary.ReportId).IsUnique();

		// How each language was produced (ADR-0108).
		builder.Property(summary => summary.SourceEn).IsRequired();
		builder.Property(summary => summary.SourceFr).IsRequired();

		builder.ToTable(t => t.HasCheckConstraint(
			"ck_summaries_source_en",
			"source_en IN ('generated', 'human', 'machine')"));

		builder.ToTable(t => t.HasCheckConstraint(
			"ck_summaries_source_fr",
			"source_fr IN ('generated', 'human', 'machine')"));

		// Edits land on this row, not the report's, so it carries its own token.
		builder.Property<uint>(ConcurrencyToken.PropertyName).HasColumnName("xmin").IsRowVersion();

		// IsApproved reads ApprovedAt alone, but a row with one of the pair
		// set and not the other is not a state the domain can represent —
		// Approve()/ClearApproval() always set or clear both together.
		builder.ToTable(t => t.HasCheckConstraint(
			"ck_summaries_approval_coherence",
			"(approved_by_subject IS NULL) = (approved_at IS NULL)"));

		// The approver is a token subject, not a key. There is no user table
		// to point a foreign key at — see ADR-0065.
		builder.Property(summary => summary.ApprovedBySubject).HasMaxLength(256);
	}
}
