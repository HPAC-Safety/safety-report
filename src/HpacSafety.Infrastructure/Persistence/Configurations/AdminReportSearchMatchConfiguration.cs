using HpacSafety.Infrastructure.Persistence.Views;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HpacSafety.Infrastructure.Persistence.Configurations;

/// <summary>
///     <see cref="AdminReportSearchMatch" /> is backed by no table or view of its
///     own — it is the row shape the <c>search_admin_reports(query)</c> SQL
///     function returns (ADR-0156). EF is told so explicitly so it neither tries
///     to create a table for it nor applies the default soft-delete filter
///     (<c>SoftDeleteFilters</c> only ever sees a <c>Deleted</c> property, which
///     this type has none of); it is queried only through
///     <c>FromSqlInterpolated</c> in <see cref="HpacSafetyDbContext.SearchAdminReports" />.
/// </summary>
public sealed class AdminReportSearchMatchConfiguration : IEntityTypeConfiguration<AdminReportSearchMatch>
{
	/// <inheritdoc />
	public void Configure(EntityTypeBuilder<AdminReportSearchMatch> builder)
	{
		ArgumentNullException.ThrowIfNull(builder);

		builder.HasNoKey();
		builder.ToView((string?)null);
	}
}
