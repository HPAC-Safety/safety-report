using HpacSafety.Core.Features.Reporting;
using HpacSafety.Core.SharedKernel;
using HpacSafety.Infrastructure.Persistence.Encryption;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HpacSafety.Infrastructure.Persistence.Configurations;

/// <summary>The <c>reports</c> table and everything hanging off it.</summary>
/// <param name="cipher">The cipher its Restricted column is bound to.</param>
public sealed class ReportConfiguration(IFieldCipher cipher) : IEntityTypeConfiguration<Report>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<Report> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("reports");
        builder.HasKey(report => report.Id);

        builder.Property(report => report.Language).IsRequired();
        builder.Property(report => report.Status).IsRequired();
        builder.Property(report => report.Province).IsRequired();
        builder.Property(report => report.TimeOfDay).IsRequired();
        builder.Property(report => report.PilotInjury).IsRequired();
        builder.Property(report => report.PassengerInjury).IsRequired();

        // Deliberately nullable, and deliberately not defaulted. An unanswered
        // consent is not a "no" — see ADR-0016.
        builder.Property(report => report.ConsentPublish);

        // The day the reporter says it happened: a date, so no session timezone
        // can shift it across midnight. The clock time at the site: Restricted,
        // so encrypted, and never published — the coarse `time_of_day` bucket
        // beside it is what anything downstream reads. See ADR-0019.
        builder.Property(report => report.OccurredAtLocal)
            .HasConversion(EncryptedTimeOnlyConverter.For(cipher));

        builder.Property(report => report.SummaryError).HasMaxLength(2000);

        // The review queue reads by status, oldest first.
        builder.HasIndex(report => new { report.Status, report.SubmittedAt });

        builder.HasMany(report => report.Answers)
            .WithOne()
            .HasForeignKey(answer => answer.ReportId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(report => report.Aircraft)
            .WithOne()
            .HasForeignKey(aircraft => aircraft.ReportId)
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

/// <summary>
/// The <c>report_answers</c> table. Its <c>value</c> column is encrypted by the
/// application — see <see cref="EncryptedStringConverter"/> and ADR-0019.
/// </summary>
/// <param name="cipher">The cipher the encrypted column is bound to.</param>
public sealed class ReportAnswerConfiguration(IFieldCipher cipher) : IEntityTypeConfiguration<ReportAnswer>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<ReportAnswer> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("report_answers");
        builder.HasKey(answer => answer.Id);

        builder.Property(answer => answer.QuestionKey).HasMaxLength(128).IsRequired();
        builder.Property(answer => answer.Sensitivity).IsRequired();

        // Restricted at rest. Encrypting the column rather than the row is what
        // makes that true regardless of how a question is later reclassified.
        builder.Property(answer => answer.Value).HasConversion(EncryptedStringConverter.For(cipher));

        builder.PrimitiveCollection(answer => answer.SelectedOptionCodes)
            .UsePropertyAccessMode(PropertyAccessMode.Field);

        builder.HasIndex(answer => answer.ReportId);
        builder.HasIndex(answer => answer.QuestionVersionId);

        // An answer references the version it was answered under, and that
        // version may never be deleted out from under it.
        builder.HasOne<Core.Features.QuestionBank.QuestionVersion>()
            .WithMany()
            .HasForeignKey(answer => answer.QuestionVersionId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<Core.Features.QuestionBank.Question>()
            .WithMany()
            .HasForeignKey(answer => answer.QuestionId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

/// <summary>The <c>report_aircraft</c> table.</summary>
public sealed class ReportAircraftConfiguration : IEntityTypeConfiguration<ReportAircraft>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<ReportAircraft> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("report_aircraft");
        builder.HasKey(aircraft => aircraft.Id);

        builder.Property(aircraft => aircraft.Discipline).IsRequired();

        // Internal tier: retained for trend analysis, never published.
        builder.Property(aircraft => aircraft.Manufacturer).HasMaxLength(200);
        builder.Property(aircraft => aircraft.Model).HasMaxLength(200);
        builder.Property(aircraft => aircraft.CertificationAnswer).HasMaxLength(200);

        builder.HasIndex(aircraft => aircraft.ReportId);
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

        builder.Property(file => file.BlobKey).HasMaxLength(512).IsRequired();
        builder.Property(file => file.StrippedBlobKey).HasMaxLength(512);
        builder.Property(file => file.ContentType).HasMaxLength(128).IsRequired();

        builder.HasIndex(file => file.ReportId);

        // The EXIF stripper claims work by looking for what it has not done yet.
        builder.HasIndex(file => file.ExifStrippedAt).HasFilter("exif_stripped_at IS NULL");
    }
}

/// <summary>The <c>summaries</c> table, one row per language.</summary>
public sealed class SummaryConfiguration : IEntityTypeConfiguration<Summary>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<Summary> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("summaries");
        builder.HasKey(summary => summary.Id);

        // The column is named for what it holds rather than for the CLR type
        // that carries it.
        builder.Property(summary => summary.Locale).HasColumnName("language").IsRequired();

        builder.Property(summary => summary.Text).IsRequired();

        // Every published sentence traces back to exactly what produced it.
        builder.Property(summary => summary.Model).HasMaxLength(200).IsRequired();
        builder.Property(summary => summary.PromptVersion).HasMaxLength(50).IsRequired();

        // One summary per language per report, so a reviewer never has to pick
        // between two French versions.
        builder.HasIndex(summary => new { summary.ReportId, summary.Locale }).IsUnique();

        // Which one was generated from the report, and which was translated
        // from that one, has to be answerable from the row itself.
        builder.HasOne<Summary>()
            .WithMany()
            .HasForeignKey(summary => summary.TranslatedFromSummaryId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<Core.Features.Moderation.AdminUser>()
            .WithMany()
            .HasForeignKey(summary => summary.ApprovedBy)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
