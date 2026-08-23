using HpacSafety.Core.Features.Reporting;
using HpacSafety.Core.SharedKernel;
using HpacSafety.Infrastructure.Persistence.Encryption;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HpacSafety.Infrastructure.Persistence.Configurations;

/// <summary>Maps reports and their two child collections.</summary>
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
        builder.Property(report => report.ConsentPublish);
        builder.Property(report => report.SummaryError).HasMaxLength(2000);
        builder.HasIndex(report => new { report.Status, report.SubmittedAt });

        builder.HasMany(report => report.Answers)
            .WithOne()
            .HasForeignKey(answer => answer.ReportId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(report => report.Files)
            .WithOne()
            .HasForeignKey(file => file.ReportId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(report => report.Summaries)
            .WithOne()
            .HasForeignKey(summary => summary.ReportId)
            .OnDelete(DeleteBehavior.Cascade);

        foreach (var navigation in builder.Metadata.GetNavigations())
        {
            navigation.SetPropertyAccessMode(PropertyAccessMode.Field);
        }
    }
}

/// <summary>Maps encrypted nullable answers to exact question revisions.</summary>
public sealed class ReportAnswerConfiguration(IFieldCipher cipher) : IEntityTypeConfiguration<ReportAnswer>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<ReportAnswer> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("report_answers");
        builder.HasKey(answer => answer.Id);
        builder.Property(answer => answer.QuestionKey).HasMaxLength(128).IsRequired();
        builder.Property(answer => answer.Value).HasConversion(EncryptedStringConverter.For(cipher));
        builder.PrimitiveCollection(answer => answer.SelectedOptionCodes)
            .UsePropertyAccessMode(PropertyAccessMode.Field);

        builder.HasIndex(answer => answer.ReportId);
        builder.HasIndex(answer => answer.QuestionId);

        builder.HasOne<Core.Features.QuestionBank.Question>()
            .WithMany()
            .HasForeignKey(answer => answer.QuestionId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

/// <summary>
/// The <c>report_files</c> table. This project owns the table's shape; the blob
/// storage that fills it is <see cref="HpacSafety.Core.SharedKernel.IBlobStore"/>.
/// </summary>
public sealed class ReportFileConfiguration : IEntityTypeConfiguration<ReportFile>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<ReportFile> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("report_files");
        builder.HasKey(file => file.Id);

        builder.Property(file => file.BlobKey).HasMaxLength(512).IsRequired();
        builder.Property(file => file.StrippedBlobKey).HasMaxLength(512);
        builder.Property(file => file.ContentType).HasMaxLength(128).IsRequired();

        builder.HasIndex(file => file.ReportId);

        // The EXIF stripper claims work by looking for what it has not done yet.
        builder.HasIndex(file => file.ExifStrippedAt).HasFilter("exif_stripped_at IS NULL");
    }
}

/// <summary>Maps the candidate summary, one row per official locale.</summary>
public sealed class SummaryConfiguration : IEntityTypeConfiguration<Summary>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<Summary> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("summaries");
        builder.HasKey(summary => summary.Id);
        builder.Property(summary => summary.Locale).HasColumnName("language").IsRequired();
        builder.Property(summary => summary.Text).IsRequired();
        builder.Property(summary => summary.Model).HasMaxLength(200).IsRequired();
        builder.Property(summary => summary.PromptVersion).HasMaxLength(50).IsRequired();

        // One summary per language per report, so a reviewer never has to pick
        // between two English drafts.
        builder.HasIndex(summary => new { summary.ReportId, summary.Locale }).IsUnique();

        builder.HasOne<Core.Features.Moderation.AdminUser>()
            .WithMany()
            .HasForeignKey(summary => summary.ApprovedBy)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
