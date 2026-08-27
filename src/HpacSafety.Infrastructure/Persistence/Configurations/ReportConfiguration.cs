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

        builder.Property(report => report.SummaryError).HasMaxLength(2000);

        // The review queue reads by status, oldest first.
        builder.HasIndex(report => new { report.Status, report.SubmittedAt });

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

        builder.PrimitiveCollection(answer => answer.SelectedOptionCodes)
            .UsePropertyAccessMode(PropertyAccessMode.Field);

        // At most one revision of the same stable key per report, and one
        // answer per report + question revision.
        builder.HasIndex(answer => new { answer.ReportId, answer.QuestionId }).IsUnique();
        builder.HasIndex(answer => answer.QuestionRevisionId);

        // An answer references the revision it was answered under, and that
        // revision may never be deleted out from under it.
        builder.HasOne<Core.Features.QuestionBank.QuestionRevision>()
            .WithMany()
            .HasForeignKey(answer => answer.QuestionRevisionId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<Core.Features.QuestionBank.Question>()
            .WithMany()
            .HasForeignKey(answer => answer.QuestionId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

/// <summary>
/// The <c>report_files</c> table. This project owns the table's shape; the blob
/// storage that fills it is issue #16.
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
        builder.Property(file => file.ProcessingErrorCode).HasMaxLength(128);

        builder.HasIndex(file => file.ReportId);

        // A file belongs to exactly one file-upload answer on the same report.
        builder.HasOne<Core.Features.Reporting.ReportAnswer>()
            .WithMany()
            .HasForeignKey(file => file.ReportAnswerId)
            .OnDelete(DeleteBehavior.Restrict);

        // The EXIF stripper claims work by looking for what it has not done yet.
        builder.HasIndex(file => file.ExifStrippedAt).HasFilter("exif_stripped_at IS NULL");
    }
}

/// <summary>
/// The <c>summaries</c> table. Exactly one bilingual row per report, with shared
/// provenance and one approval covering both languages.
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

        builder.HasOne<Core.Features.Moderation.AdminUser>()
            .WithMany()
            .HasForeignKey(summary => summary.ApprovedBy)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
