using HpacSafety.Core;
using HpacSafety.Infrastructure.Persistence.Views;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HpacSafety.Infrastructure.Persistence.Configurations;

/// <summary>
///     The read-only <c>public_report_comments</c> view. A migration creates it
///     from its own <c>.sql</c> file (ADR-0055); EF only reads it.
/// </summary>
public sealed class PublicReportCommentConfiguration : IEntityTypeConfiguration<PublicReportComment>
{
	/// <inheritdoc />
	public void Configure(EntityTypeBuilder<PublicReportComment> builder)
	{
		ArgumentNullException.ThrowIfNull(builder);

		builder.ToView("public_report_comments");
		builder.HasKey(comment => comment.Id);

		foreach (var id in new[] { nameof(PublicReportComment.Id), nameof(PublicReportComment.ReportId) })
		{
			builder.Property<string>(id)
				.HasMaxLength(TinyId.Length)
				.IsFixedLength()
				.HasColumnType($"char({TinyId.Length})");
		}
	}
}
