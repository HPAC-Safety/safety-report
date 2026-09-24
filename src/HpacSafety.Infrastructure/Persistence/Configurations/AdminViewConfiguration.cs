using HpacSafety.Infrastructure.Persistence.Views;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HpacSafety.Infrastructure.Persistence.Configurations;

/// <summary>
///     The read-only <c>admin_report_queue</c> view. A migration creates it from
///     its own <c>.sql</c> file (ADR-0055); EF only reads it.
/// </summary>
public sealed class AdminReportQueueItemConfiguration : IEntityTypeConfiguration<AdminReportQueueItem>
{
	/// <inheritdoc />
	public void Configure(EntityTypeBuilder<AdminReportQueueItem> builder)
	{
		ArgumentNullException.ThrowIfNull(builder);

		builder.ToView("admin_report_queue");
		builder.HasKey(item => item.Id);
	}
}

/// <summary>The read-only <c>answers_awaiting_translation</c> view.</summary>
public sealed class AnswerAwaitingTranslationConfiguration : IEntityTypeConfiguration<AnswerAwaitingTranslation>
{
	/// <inheritdoc />
	public void Configure(EntityTypeBuilder<AnswerAwaitingTranslation> builder)
	{
		ArgumentNullException.ThrowIfNull(builder);

		builder.ToView("answers_awaiting_translation");
		builder.HasKey(answer => answer.Id);
	}
}

/// <summary>The read-only, single-row <c>admin_pending_counts</c> view.</summary>
public sealed class AdminPendingCountsConfiguration : IEntityTypeConfiguration<AdminPendingCounts>
{
	/// <inheritdoc />
	public void Configure(EntityTypeBuilder<AdminPendingCounts> builder)
	{
		ArgumentNullException.ThrowIfNull(builder);

		builder.ToView("admin_pending_counts");
		builder.HasNoKey();
	}
}
