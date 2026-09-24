using HpacSafety.Core;
using HpacSafety.Infrastructure.Persistence.Views;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HpacSafety.Infrastructure.Persistence.Configurations;

/// <summary>
///     The read-only <c>public_report_media</c> view. A migration creates it from
///     its own <c>.sql</c> file (ADR-0055); EF only reads it.
/// </summary>
public sealed class PublicReportMediaConfiguration : IEntityTypeConfiguration<PublicReportMedia>
{
	/// <inheritdoc />
	public void Configure(EntityTypeBuilder<PublicReportMedia> builder)
	{
		ArgumentNullException.ThrowIfNull(builder);

		builder.ToView("public_report_media");
		builder.HasKey(media => media.Id);

		foreach (var id in new[] { nameof(PublicReportMedia.Id), nameof(PublicReportMedia.ReportId) })
		{
			builder.Property<string>(id)
				.HasMaxLength(TinyId.Length)
				.IsFixedLength()
				.HasColumnType($"char({TinyId.Length})");
		}
	}
}
