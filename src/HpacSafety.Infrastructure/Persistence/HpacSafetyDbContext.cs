using HpacSafety.Core.Features.Moderation;
using HpacSafety.Core.Features.Outbox;
using HpacSafety.Core.Features.QuestionBank;
using HpacSafety.Core.Features.Reporting;
using HpacSafety.Core.SharedKernel;
using HpacSafety.Infrastructure.Persistence.Configurations;
using HpacSafety.Infrastructure.Persistence.Conventions;
using HpacSafety.Infrastructure.Persistence.Conversions;

using Microsoft.EntityFrameworkCore;

namespace HpacSafety.Infrastructure.Persistence;

/// <summary>
/// The one database context. Owns the schema, the migrations, and the
/// application-side encryption of Restricted columns.
/// </summary>
/// <remarks>
/// <para>
/// A report and its outbox row are written through one
/// <see cref="DbContext.SaveChangesAsync(CancellationToken)"/>, which is a
/// single transaction. That is the whole of ADR-0002's guarantee, and it is why
/// the outbox is a table in this context rather than a queue somewhere else.
/// </para>
/// <para>
/// The context is created with a cipher because encryption is bound into the
/// model: see <see cref="Encryption.EncryptedStringConverter"/> and ADR-0019.
/// </para>
/// </remarks>
public class HpacSafetyDbContext : DbContext
{
    private readonly IFieldCipher _cipher;

    /// <summary>Creates the context.</summary>
    /// <param name="options">Provider and connection options.</param>
    /// <param name="cipher">The cipher encrypted columns are bound to.</param>
    public HpacSafetyDbContext(DbContextOptions<HpacSafetyDbContext> options, IFieldCipher cipher)
        : base(options)
    {
        _cipher = cipher ?? throw new ArgumentNullException(nameof(cipher));
    }

    /// <summary>Occurrence reports.</summary>
    public DbSet<Report> Reports => Set<Report>();

    /// <summary>Answers, each referencing the question version it was given under.</summary>
    public DbSet<ReportAnswer> ReportAnswers => Set<ReportAnswer>();

    /// <summary>Aircraft involved in a report.</summary>
    public DbSet<ReportAircraft> ReportAircraft => Set<ReportAircraft>();

    /// <summary>Uploaded media. Blob storage itself is issue #16.</summary>
    public DbSet<ReportFile> ReportFiles => Set<ReportFile>();

    /// <summary>Summaries, one row per language.</summary>
    public DbSet<Summary> Summaries => Set<Summary>();

    /// <summary>The question bank.</summary>
    public DbSet<Question> Questions => Set<Question>();

    /// <summary>Immutable question versions.</summary>
    public DbSet<QuestionVersion> QuestionVersions => Set<QuestionVersion>();

    /// <summary>Options on a question version.</summary>
    public DbSet<QuestionOption> QuestionOptions => Set<QuestionOption>();

    /// <summary>Per-locale question wording.</summary>
    public DbSet<QuestionTranslation> QuestionTranslations => Set<QuestionTranslation>();

    /// <summary>Per-locale option wording.</summary>
    public DbSet<QuestionOptionTranslation> QuestionOptionTranslations => Set<QuestionOptionTranslation>();

    /// <summary>The admin allowlist.</summary>
    public DbSet<AdminUser> AdminUsers => Set<AdminUser>();

    /// <summary>Who did what, and when.</summary>
    public DbSet<AuditLogEntry> AuditLog => Set<AuditLogEntry>();

    /// <summary>Outbox messages awaiting a worker.</summary>
    public DbSet<OutboxMessage> OutboxMessages => Set<OutboxMessage>();

    /// <summary>
    /// Names the key this context's model was built with, so two contexts
    /// holding different keys cannot share one cached model. Never the key.
    /// </summary>
    internal string CipherKeyId => _cipher.KeyId;

    /// <inheritdoc />
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        ArgumentNullException.ThrowIfNull(modelBuilder);

        modelBuilder.ApplyConfiguration(new ReportConfiguration());
        modelBuilder.ApplyConfiguration(new ReportAnswerConfiguration(_cipher));
        modelBuilder.ApplyConfiguration(new ReportAircraftConfiguration());
        modelBuilder.ApplyConfiguration(new ReportFileConfiguration());
        modelBuilder.ApplyConfiguration(new SummaryConfiguration());

        modelBuilder.ApplyConfiguration(new QuestionConfiguration());
        modelBuilder.ApplyConfiguration(new QuestionVersionConfiguration());
        modelBuilder.ApplyConfiguration(new QuestionOptionConfiguration());
        modelBuilder.ApplyConfiguration(new QuestionTranslationConfiguration());
        modelBuilder.ApplyConfiguration(new QuestionOptionTranslationConfiguration());

        modelBuilder.ApplyConfiguration(new AdminUserConfiguration());
        modelBuilder.ApplyConfiguration(new AuditLogEntryConfiguration());
        modelBuilder.ApplyConfiguration(new OutboxMessageConfiguration());

        // Last, so anything named explicitly above keeps the name it was given.
        SnakeCaseNames.Apply(modelBuilder);
    }

    /// <inheritdoc />
    protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
    {
        ArgumentNullException.ThrowIfNull(configurationBuilder);

        configurationBuilder.Properties<Locale>().HaveConversion<LocaleConverter>().HaveMaxLength(8);

        // Domain values are stored as invariant codes and localized only at the
        // edge, so a row is readable on its own and a reordered enum cannot
        // silently reinterpret history.
        configurationBuilder.Properties<ReportStatus>().HaveConversion<EnumCodeConverter<ReportStatus>>().HaveMaxLength(64);
        configurationBuilder.Properties<Province>().HaveConversion<EnumCodeConverter<Province>>().HaveMaxLength(64);
        configurationBuilder.Properties<TimeOfDay>().HaveConversion<EnumCodeConverter<TimeOfDay>>().HaveMaxLength(64);
        configurationBuilder.Properties<InjurySeverity>().HaveConversion<EnumCodeConverter<InjurySeverity>>().HaveMaxLength(64);
        configurationBuilder.Properties<Discipline>().HaveConversion<EnumCodeConverter<Discipline>>().HaveMaxLength(64);
        configurationBuilder.Properties<AircraftClass>().HaveConversion<EnumCodeConverter<AircraftClass>>().HaveMaxLength(64);
        configurationBuilder.Properties<SensitivityTier>().HaveConversion<EnumCodeConverter<SensitivityTier>>().HaveMaxLength(64);
        configurationBuilder.Properties<QuestionType>().HaveConversion<EnumCodeConverter<QuestionType>>().HaveMaxLength(64);
        configurationBuilder.Properties<QuestionRole>().HaveConversion<EnumCodeConverter<QuestionRole>>().HaveMaxLength(64);
        configurationBuilder.Properties<AdminRole>().HaveConversion<EnumCodeConverter<AdminRole>>().HaveMaxLength(64);
        configurationBuilder.Properties<AuditAction>().HaveConversion<EnumCodeConverter<AuditAction>>().HaveMaxLength(64);
    }
}
