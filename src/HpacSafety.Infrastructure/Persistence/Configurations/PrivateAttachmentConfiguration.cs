using HpacSafety.Core.Features.PrivateAttachments;
using HpacSafety.Core.Features.Reporting;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HpacSafety.Infrastructure.Persistence.Configurations;

/// <summary>
///     The <c>report_private_attachments</c> table: staff-only files on a report
///     (ADR-0135). Deliberately not <c>report_files</c>, so no reporter-attachment
///     reader — the Worker, the model input, a public view, a count — can reach it
///     by forgetting a flag. No view, public query, or Worker reads it.
/// </summary>
public sealed class PrivateAttachmentConfiguration : IEntityTypeConfiguration<PrivateAttachment>
{
	/// <inheritdoc />
	public void Configure(EntityTypeBuilder<PrivateAttachment> builder)
	{
		ArgumentNullException.ThrowIfNull(builder);

		builder.ToTable("report_private_attachments");
		builder.HasKey(attachment => attachment.Id);

		builder.Property(attachment => attachment.BlobKey).HasMaxLength(512).IsRequired();
		builder.Property(attachment => attachment.OriginalFileName).HasMaxLength(AttachmentFileName.MaxLength).IsRequired();
		builder.Property(attachment => attachment.ContentType).HasMaxLength(PrivateAttachmentPolicy.ContentTypeMaxLength).IsRequired();
		builder.Property(attachment => attachment.Description).HasMaxLength(PrivateAttachment.DescriptionMaxLength);

		// Opaque token subjects, never keys (ADR-0065).
		builder.Property(attachment => attachment.AddedBySubject).HasMaxLength(PrivateAttachment.SubjectMaxLength).IsRequired();
		builder.Property(attachment => attachment.DeletedBySubject).HasMaxLength(PrivateAttachment.SubjectMaxLength);

		// A report's attachments, newest first, is the one read.
		builder.HasIndex(attachment => new { attachment.ReportId, attachment.AddedAt });
		builder.HasIndex(attachment => attachment.BlobKey).IsUnique();

		// A report is never physically deleted, and an attachment must never
		// outlive the report row it points at.
		builder.HasOne<Report>()
			.WithMany()
			.HasForeignKey(attachment => attachment.ReportId)
			.OnDelete(DeleteBehavior.Restrict);

		builder.ToTable(t => t.HasCheckConstraint("ck_report_private_attachments_byte_size", "byte_size > 0"));
	}
}
