using HpacSafety.Core;
using HpacSafety.Core.Features.Reporting;
using HpacSafety.Infrastructure.Persistence.Views;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HpacSafety.Infrastructure.Persistence.Configurations;

/// <summary>
///     The read-only <c>own_reports</c> view. A migration creates it from its own
///     <c>.sql</c> file (ADR-0055); EF only reads it.
/// </summary>
public sealed class OwnReportConfiguration : IEntityTypeConfiguration<OwnReport>
{
	/// <inheritdoc />
	public void Configure(EntityTypeBuilder<OwnReport> builder)
	{
		ArgumentNullException.ThrowIfNull(builder);

		builder.ToView("own_reports");
		builder.HasKey(report => report.Id);
		builder.Property(report => report.Id)
			.HasMaxLength(TinyId.Length)
			.IsFixedLength()
			.HasColumnType($"char({TinyId.Length})");
		builder.Property(report => report.ReceiptHash).HasMaxLength(BrowserReceipt.Length);
	}
}

/// <summary>
///     The read-only <c>own_report_media</c> view, which reads the same consent
///     rule as <c>public_report_media</c>.
/// </summary>
public sealed class OwnReportMediaConfiguration : IEntityTypeConfiguration<OwnReportMedia>
{
	/// <inheritdoc />
	public void Configure(EntityTypeBuilder<OwnReportMedia> builder)
	{
		ArgumentNullException.ThrowIfNull(builder);

		builder.ToView("own_report_media");
		builder.HasKey(media => media.Id);

		foreach (var id in new[] { nameof(OwnReportMedia.Id), nameof(OwnReportMedia.ReportId) })
		{
			builder.Property<string>(id)
				.HasMaxLength(TinyId.Length)
				.IsFixedLength()
				.HasColumnType($"char({TinyId.Length})");
		}
	}
}
