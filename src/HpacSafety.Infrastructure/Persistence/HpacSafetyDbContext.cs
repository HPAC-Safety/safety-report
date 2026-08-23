using HpacSafety.Core.Features.Moderation;
using HpacSafety.Core.Features.Outbox;
using HpacSafety.Core.Features.QuestionBank;
using HpacSafety.Core.Features.Reporting;
using HpacSafety.Core.SharedKernel;
using HpacSafety.Infrastructure.Persistence.Configurations;
using HpacSafety.Infrastructure.Persistence.Conventions;
using HpacSafety.Infrastructure.Persistence.Conversions;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;

using Npgsql;

namespace HpacSafety.Infrastructure.Persistence;

/// <summary>
/// The one database context. Owns the schema, the migrations, and the
/// application-side encryption of report values.
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
    /// <summary>
    /// How many times a save will mint fresh identifiers and try again. Three
    /// consecutive collisions at sixty-six bits is not a run of bad luck; it is
    /// a broken random source, and failing is the right answer.
    /// </summary>
    private const int IdentifierAttempts = 3;

    /// <summary>PostgreSQL's <c>unique_violation</c>.</summary>
    private const string UniqueViolation = "23505";

    /// <summary>
    /// Columns that name a row without a foreign key to it, because they point
    /// at more than one kind of thing. EF cannot fix these up, so a retry does.
    /// </summary>
    private static readonly string[] LooseReferences = ["AggregateId", "TargetId"];

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

    /// <summary>
    /// Saves, and mints a new identifier for anything that lost a collision.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Sixty-six bits of entropy makes a collision vanishingly unlikely. That is
    /// not the same as handled: a unique constraint turns one into a rejected
    /// write rather than a silently overwritten report, and this turns the
    /// rejected write into a second attempt with a fresh identifier. See
    /// ADR-0034.
    /// </para>
    /// <para>
    /// PostgreSQL abandons the whole transaction on any error, so when a caller
    /// has opened one — as the report endpoint does, writing the report and its
    /// outbox row together — a savepoint is taken first and the retry rolls back
    /// to it. The outer transaction, and ADR-0002's guarantee, survive.
    /// </para>
    /// </remarks>
    /// <param name="cancellationToken">Cancels the save.</param>
    public override async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        for (var attempt = 1; ; attempt++)
        {
            var transaction = Database.CurrentTransaction;
            var savepoint = transaction is not null && attempt < IdentifierAttempts
                ? $"tiny_id_attempt_{attempt}"
                : null;

            if (savepoint is not null)
            {
                await transaction!.CreateSavepointAsync(savepoint, cancellationToken).ConfigureAwait(false);
            }

            try
            {
                return await base.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            }
            catch (DbUpdateException cause)
                when (attempt < IdentifierAttempts && IsIdentifierCollision(cause))
            {
                if (savepoint is not null)
                {
                    await transaction!.RollbackToSavepointAsync(savepoint, cancellationToken).ConfigureAwait(false);
                }

                MintNewIdentifiers(cause);
            }
        }
    }

    /// <inheritdoc />
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        ArgumentNullException.ThrowIfNull(modelBuilder);

        modelBuilder.ApplyConfiguration(new ReportConfiguration(_cipher));
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

        // Every identifier in every table is the same eleven-character shape.
        // One convention, no mixed-type joins, and nothing in it that says when
        // a row was created. See ADR-0034.
        configurationBuilder.Properties<TinyId>()
            .HaveConversion<TinyIdConverter>()
            .HaveMaxLength(TinyId.Length)
            .AreFixedLength()
            .HaveColumnType($"char({TinyId.Length})");

        configurationBuilder.Properties<Locale>().HaveConversion<LocaleConverter>().HaveMaxLength(8);

        // Domain values are stored as invariant codes and localized only at the
        // edge, so a row is readable on its own and a reordered enum cannot
        // silently reinterpret history.
        configurationBuilder.Properties<ReportStatus>().HaveConversion<EnumCodeConverter<ReportStatus>>().HaveMaxLength(64);
        configurationBuilder.Properties<Province>().HaveConversion<EnumCodeConverter<Province>>().HaveMaxLength(64);
        configurationBuilder.Properties<TimeOfDay>().HaveConversion<EnumCodeConverter<TimeOfDay>>().HaveMaxLength(64);
        configurationBuilder.Properties<InjurySeverity>().HaveConversion<EnumCodeConverter<InjurySeverity>>().HaveMaxLength(64);
        configurationBuilder.Properties<Discipline>().HaveConversion<EnumCodeConverter<Discipline>>().HaveMaxLength(64);
        configurationBuilder.Properties<QuestionType>().HaveConversion<EnumCodeConverter<QuestionType>>().HaveMaxLength(64);
        configurationBuilder.Properties<QuestionRole>().HaveConversion<EnumCodeConverter<QuestionRole>>().HaveMaxLength(64);
        configurationBuilder.Properties<AdminRole>().HaveConversion<EnumCodeConverter<AdminRole>>().HaveMaxLength(64);
        configurationBuilder.Properties<AuditAction>().HaveConversion<EnumCodeConverter<AuditAction>>().HaveMaxLength(64);
    }

    /// <summary>
    /// Whether a failed write was a primary key that already existed — as
    /// opposed to a unique constraint the domain put there on purpose, such as
    /// one summary per language, which is a real conflict and not luck.
    /// </summary>
    private static bool IsIdentifierCollision(DbUpdateException cause) =>
        cause.InnerException is PostgresException postgres
        && postgres.SqlState == UniqueViolation
        && postgres.ConstraintName?.StartsWith("pk_", StringComparison.Ordinal) == true;

    /// <summary>
    /// Mints a fresh identifier for every row the failed write was inserting,
    /// and repoints anything that referred to the old one by value.
    /// </summary>
    /// <remarks>
    /// The identifier property is <c>private init</c>, so it is set through EF's
    /// own accessor rather than by the domain — nothing outside persistence may
    /// change an identifier once a row has one.
    /// <para>
    /// EF fixes up real relationships itself. What it cannot fix up is a
    /// reference held as a bare value: <c>outbox_messages.aggregate_id</c> and
    /// <c>audit_log.target_id</c> both name a row without a foreign key to it,
    /// deliberately, because they point at more than one kind of thing. Those
    /// are rewritten here, or a retried report would commit alongside an outbox
    /// message pointing at an identifier that no longer exists.
    /// </para>
    /// </remarks>
    private void MintNewIdentifiers(DbUpdateException cause)
    {
        var entries = cause.Entries.Count > 0
            ? cause.Entries
            : [.. ChangeTracker.Entries().Where(entry => entry.State == EntityState.Added)];

        var replacements = new Dictionary<TinyId, TinyId>();

        foreach (var entry in entries)
        {
            if (entry.State != EntityState.Added)
            {
                continue;
            }

            if (entry.Metadata.FindProperty("Id") is not { ClrType: var clrType } || clrType != typeof(TinyId))
            {
                continue;
            }

            var property = entry.Property("Id");
            var replacement = TinyId.New();
            replacements[(TinyId)property.CurrentValue!] = replacement;
            property.CurrentValue = replacement;
        }

        if (replacements.Count == 0)
        {
            return;
        }

        foreach (var entry in ChangeTracker.Entries().Where(entry => entry.State == EntityState.Added))
        {
            foreach (var name in LooseReferences)
            {
                if (entry.Metadata.FindProperty(name) is not { ClrType: var clrType } || clrType != typeof(TinyId))
                {
                    continue;
                }

                var property = entry.Property(name);
                if (property.CurrentValue is TinyId pointed && replacements.TryGetValue(pointed, out var replacement))
                {
                    property.CurrentValue = replacement;
                }
            }
        }
    }
}
