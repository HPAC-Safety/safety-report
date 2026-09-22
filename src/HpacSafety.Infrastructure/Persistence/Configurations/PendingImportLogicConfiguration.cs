using HpacSafety.Core.Features.QuestionBank.Typeform;
using HpacSafety.Infrastructure.Persistence.Conventions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HpacSafety.Infrastructure.Persistence.Configurations;

/// <summary>
///     The <c>pending_import_logic</c> table. Deliberately has no
///     <c>Deleted</c> column — see the class remarks on
///     <see cref="PendingImportLogic" />. <see cref="SoftDeleteFilters" />
///     applies the live-row filter to every entity with a <c>Deleted</c>
///     property, so this one is skipped automatically, the same way
///     <c>audit_log</c> is.
/// </summary>
public sealed class PendingImportLogicConfiguration : IEntityTypeConfiguration<PendingImportLogic>
{
	/// <inheritdoc />
	public void Configure(EntityTypeBuilder<PendingImportLogic> builder)
	{
		ArgumentNullException.ThrowIfNull(builder);

		builder.ToTable("pending_import_logic");
		builder.HasKey(note => note.Id);

		builder.Property(note => note.FieldRef).HasMaxLength(256).IsRequired();
		builder.Property(note => note.FieldTitle).IsRequired();
		builder.Property(note => note.RawLogicJson).IsRequired();

		builder.HasIndex(note => note.ImportBatchId);
	}
}
