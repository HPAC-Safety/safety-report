using HpacSafety.Core.Features.Moderation;
using HpacSafety.Core.Features.Outbox;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HpacSafety.Infrastructure.Persistence.Configurations;

/// <summary>
///     The <c>audit_log</c> table. Every moderation action, with who and when —
///     see <c>docs/data-handling.md</c>, "Access and audit".
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
		builder.Property(entry => entry.ActorSubject).HasMaxLength(256).IsRequired();
		builder.Property(entry => entry.TargetType).HasMaxLength(128).IsRequired();
		builder.Property(entry => entry.Detail).HasMaxLength(2000);

		builder.HasIndex(entry => entry.OccurredAt);
		builder.HasIndex(entry => new { entry.TargetType, entry.TargetId });

		// The actor is a token subject, not a key. There is no user table to
		// point a foreign key at, and an audit entry has to outlive whatever
		// access the actor had anyway — revoking someone must never erase what
		// they did. Indexed so "everything this subject touched" stays a cheap
		// question to ask. See ADR-0065.
		builder.HasIndex(entry => entry.ActorSubject);
	}
}

/// <summary>
///     The <c>outbox_messages</c> table. The report and its outbox row commit in one
///     transaction — see ADR-0002.
/// </summary>
public sealed class OutboxMessageConfiguration : IEntityTypeConfiguration<OutboxMessage>
{
	/// <summary>
	///     The rows the worker is allowed to claim: not yet processed, and not set
	///     aside as poison. A partial index keeps the claim query reading only
	///     those, however long the processed history grows.
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
			"type IN ('summarize_report', 'process_attachment', 'translate_answers')"));
	}
}
