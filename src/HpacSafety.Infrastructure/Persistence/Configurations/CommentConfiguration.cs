using HpacSafety.Core.Features.Comments;
using HpacSafety.Core.Features.Reporting;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HpacSafety.Infrastructure.Persistence.Configurations;

/// <summary>The <c>report_comments</c> table: members' comments on published reports (ADR-0114).</summary>
public sealed class ReportCommentConfiguration : IEntityTypeConfiguration<ReportComment>
{
	/// <inheritdoc />
	public void Configure(EntityTypeBuilder<ReportComment> builder)
	{
		ArgumentNullException.ThrowIfNull(builder);

		builder.ToTable("report_comments");
		builder.HasKey(comment => comment.Id);

		// Opaque token subjects, never keys (ADR-0065).
		builder.Property(comment => comment.AuthorSubject).HasMaxLength(ReportComment.SubjectMaxLength).IsRequired();
		builder.Property(comment => comment.HiddenBySubject).HasMaxLength(ReportComment.SubjectMaxLength);

		builder.ToTable(t => t.HasCheckConstraint(
			"ck_report_comments_hidden_coherence",
			"(hidden_at IS NULL) = (hidden_by_subject IS NULL)"));

		// A report's comments, oldest first, is the one read.
		builder.HasIndex(comment => new { comment.ReportId, comment.CreatedAt });

		// A report is never physically deleted, and a comment must never outlive
		// a report row it points at.
		builder.HasOne<Report>()
			.WithMany()
			.HasForeignKey(comment => comment.ReportId)
			.OnDelete(DeleteBehavior.Restrict);

		builder.HasMany(comment => comment.Revisions)
			.WithOne()
			.HasForeignKey(revision => revision.CommentId)
			.OnDelete(DeleteBehavior.Cascade);

		builder.Ignore(comment => comment.Current);
		builder.Metadata.FindNavigation(nameof(ReportComment.Revisions))!.SetPropertyAccessMode(PropertyAccessMode.Field);
	}
}

/// <summary>The <c>report_comment_revisions</c> table: every version of a comment's text.</summary>
public sealed class ReportCommentRevisionConfiguration : IEntityTypeConfiguration<ReportCommentRevision>
{
	/// <inheritdoc />
	public void Configure(EntityTypeBuilder<ReportCommentRevision> builder)
	{
		ArgumentNullException.ThrowIfNull(builder);

		builder.ToTable("report_comment_revisions");
		builder.HasKey(revision => revision.Id);

		builder.Property(revision => revision.Text).HasMaxLength(ReportComment.MaxLength).IsRequired();
		builder.Property(revision => revision.TranslatedText);
		builder.Property(revision => revision.Locale).IsRequired();

		builder.HasIndex(revision => new { revision.CommentId, revision.Number }).IsUnique();

		builder.ToTable(t => t.HasCheckConstraint(
			"ck_report_comment_revisions_locale",
			"locale IN ('en-CA', 'fr-CA')"));

		builder.ToTable(t => t.HasCheckConstraint(
			"ck_report_comment_revisions_translation_source",
			"translation_source IS NULL OR translation_source = 'auto'"));

		builder.ToTable(t => t.HasCheckConstraint(
			"ck_report_comment_revisions_translation_coherence",
			"(translated_text IS NULL) = (translation_source IS NULL)"));

		// The Worker's queue: revisions still waiting for their translation.
		builder.HasIndex(revision => revision.CreatedAt)
			.HasFilter("translated_text IS NULL AND deleted IS NULL");
	}
}
