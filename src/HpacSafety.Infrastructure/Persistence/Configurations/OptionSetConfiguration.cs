using HpacSafety.Core.Features.QuestionBank;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HpacSafety.Infrastructure.Persistence.Configurations;

/// <summary>
/// The <c>option_sets</c> table: reusable choice lists an administrator
/// maintains, shared by every question that offers the same list — ADR-0058.
/// </summary>
public sealed class OptionSetConfiguration : IEntityTypeConfiguration<OptionSet>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<OptionSet> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("option_sets");
        builder.HasKey(set => set.Id);

        builder.Property(set => set.Key).HasMaxLength(128).IsRequired();
        builder.Property(set => set.NameEn).IsRequired();
        builder.Property(set => set.NameFr).IsRequired();

        builder.HasIndex(set => set.Key).IsUnique();

        // Items is a filtered, ordered projection over the backing field, not a
        // mapped navigation — EF writes the field and the property reads it.
        builder.Ignore(set => set.Items);

        builder.HasMany<OptionSetItem>("_items")
            .WithOne()
            .HasForeignKey(item => item.OptionSetId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Metadata.FindNavigation("_items")!
            .SetPropertyAccessMode(PropertyAccessMode.Field);
    }
}

/// <summary>
/// The <c>option_set_items</c> table. Editable, unlike
/// <c>question_revision_options</c>, which is the frozen copy a revision took.
/// </summary>
public sealed class OptionSetItemConfiguration : IEntityTypeConfiguration<OptionSetItem>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<OptionSetItem> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("option_set_items");
        builder.HasKey(item => item.Id);

        builder.Property(item => item.Code).HasMaxLength(128).IsRequired();
        builder.Property(item => item.LabelEn).IsRequired();
        builder.Property(item => item.LabelFr).IsRequired();
        builder.Property(item => item.DisplayOrder).IsRequired();

        // Not nullable: an item was either typed by a reporter or authored by
        // an administrator, and every row that already exists was the latter.
        builder.Property(item => item.AddedByReporter).IsRequired().HasDefaultValue(false);
        builder.Property(item => item.NeedsTranslation).IsRequired().HasDefaultValue(false);

        // The curation query is "show me what reporters have added to this
        // list", so it is worth an index on the pair.
        builder.HasIndex(item => new { item.OptionSetId, item.AddedByReporter });

        // A code is unique in its set, including across a removed item —
        // re-adding a removed code revives that row rather than adding a
        // second one claiming the same code, so history keeps one target.
        builder.HasIndex(item => new { item.OptionSetId, item.Code }).IsUnique();
    }
}
