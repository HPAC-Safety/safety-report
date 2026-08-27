using HpacSafety.Core.Features.Moderation;
using HpacSafety.Core.Features.Outbox;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HpacSafety.Infrastructure.Persistence.Configurations;

/// <summary>
/// The <c>admin_users</c> table — the allowlist that admin access is.
/// </summary>
/// <remarks>
/// The member identifier is stored in plaintext, unlike a reporter's contact
/// details, because it is the lookup key at sign-in and a randomized-nonce
/// ciphertext cannot be looked up. It is an administrator's own working
/// identity, not a reporter's, and it is never part of a published summary.
/// See ADR-0019.
/// </remarks>
public sealed class AdminUserConfiguration : IEntityTypeConfiguration<AdminUser>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<AdminUser> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("admin_users");
        builder.HasKey(user => user.Id);

        builder.Property(user => user.MemberIdentifier).HasMaxLength(256).IsRequired();
        builder.Property(user => user.Role).IsRequired();

        // Unique among live rows only: a deleted administrator must not
        // permanently block re-adding the same upstream member identifier.
        // The query filter already hides deleted rows from ordinary reads,
        // but a plain unique index is still checked against them by the
        // database — this partial index is what actually frees the
        // identifier back up once its row is soft-deleted.
        builder.HasIndex(user => user.MemberIdentifier)
            .IsUnique()
            .HasFilter("deleted IS NULL");

        builder.ToTable(t => t.HasCheckConstraint(
            "ck_admin_users_role",
            "role IN ('safety_officer', 'administrator')"));
    }
}

/// <summary>
/// The <c>audit_log</c> table. Every moderation action, with who and when —
/// see <c>docs/data-handling.md</c>, "Access and audit".
/// </summary>
public sealed class AuditLogEntryConfiguration : IEntityTypeConfiguration<AuditLogEntry>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<AuditLogEntry> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("audit_log");
        builder.HasKey(entry => entry.Id);

        builder.Property(entry => entry.Action).IsRequired();
        builder.Property(entry => entry.TargetType).HasMaxLength(128).IsRequired();
        builder.Property(entry => entry.Detail).HasMaxLength(2000);

        builder.HasIndex(entry => entry.OccurredAt);
        builder.HasIndex(entry => new { entry.TargetType, entry.TargetId });

        // An audit entry outlives the administrator's access; revoking someone
        // must never erase what they did.
        builder.HasOne<AdminUser>()
            .WithMany()
            .HasForeignKey(entry => entry.AdminUserId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

/// <summary>
/// The <c>outbox_messages</c> table. The report and its outbox row commit in one
/// transaction — see ADR-0002.
/// </summary>
public sealed class OutboxMessageConfiguration : IEntityTypeConfiguration<OutboxMessage>
{
    /// <summary>
    /// The rows the worker is allowed to claim: not yet processed, and not set
    /// aside as poison. A partial index keeps the claim query reading only
    /// those, however long the processed history grows.
    /// </summary>
    public const string ClaimableFilter = "processed_at IS NULL AND poisoned_at IS NULL";

    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<OutboxMessage> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("outbox_messages");
        builder.HasKey(message => message.Id);

        builder.Property(message => message.Type).IsRequired();
        builder.Property(message => message.Payload).IsRequired();
        builder.Property(message => message.LastError).HasMaxLength(2000);

        // The claim query: the oldest due message that nobody else holds, found
        // with FOR UPDATE SKIP LOCKED.
        builder.HasIndex(message => message.NextAttemptAt)
            .HasDatabaseName("ix_outbox_messages_claimable")
            .HasFilter(ClaimableFilter);

        builder.HasIndex(message => message.AggregateId);

        builder.ToTable(t => t.HasCheckConstraint(
            "ck_outbox_messages_type",
            "type IN ('summarize_report')"));
    }
}
