using HpacSafety.Core;
using HpacSafety.Infrastructure.Persistence.Views;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HpacSafety.Infrastructure.Persistence.Configurations;

/// <summary>
///     The read-only <c>public_reports</c> view. A migration creates it from its
///     own <c>.sql</c> file (ADR-0055); EF only reads it.
/// </summary>
public sealed class PublicReportConfiguration : IEntityTypeConfiguration<PublicReport>
{
	/// <inheritdoc />
	public void Configure(EntityTypeBuilder<PublicReport> builder)
	{
		ArgumentNullException.ThrowIfNull(builder);

		builder.ToView("public_reports");
		builder.HasKey(report => report.Id);
		builder.Property(report => report.Id)
			.HasMaxLength(TinyId.Length)
			.IsFixedLength()
			.HasColumnType($"char({TinyId.Length})");
	}
}
