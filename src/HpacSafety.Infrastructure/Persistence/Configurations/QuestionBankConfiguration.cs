using HpacSafety.Core.Features.QuestionBank;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HpacSafety.Infrastructure.Persistence.Configurations;

/// <summary>The <c>questions</c> table. The form is rows, not columns — ADR-0016.</summary>
public sealed class QuestionConfiguration : IEntityTypeConfiguration<Question>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<Question> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("questions");
        builder.HasKey(question => question.Id);

        builder.Property(question => question.Key).HasMaxLength(128).IsRequired();
        builder.Property(question => question.SectionKey).HasMaxLength(128);
        builder.Property(question => question.Role).IsRequired();
        builder.Property(question => question.IsPrivate).IsRequired();

        builder.HasIndex(question => question.Key).IsUnique();
        builder.HasIndex(question => new { question.IsActive, question.DisplayOrder });

        builder.HasMany(question => question.Revisions)
            .WithOne()
            .HasForeignKey(revision => revision.QuestionId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Metadata.FindNavigation(nameof(Question.Revisions))!
            .SetPropertyAccessMode(PropertyAccessMode.Field);
    }
}

/// <summary>
/// The <c>question_revisions</c> table. A revision is complete and immutable
/// once written: both official languages are present from the start.
/// </summary>
public sealed class QuestionRevisionConfiguration : IEntityTypeConfiguration<QuestionRevision>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<QuestionRevision> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("question_revisions");
        builder.HasKey(revision => revision.Id);

        builder.Property(revision => revision.Type).IsRequired();
        builder.Property(revision => revision.LabelEn).IsRequired();
        builder.Property(revision => revision.LabelFr).IsRequired();

        // Unique stable key + revision number.
        builder.HasIndex(revision => new { revision.QuestionId, revision.RevisionNumber }).IsUnique();

        builder.HasMany(revision => revision.Options)
            .WithOne()
            .HasForeignKey(option => option.QuestionRevisionId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Metadata.FindNavigation(nameof(QuestionRevision.Options))!
            .SetPropertyAccessMode(PropertyAccessMode.Field);
    }
}

/// <summary>
/// The <c>question_revision_options</c> table, complete in both official
/// languages.
/// </summary>
public sealed class QuestionRevisionOptionConfiguration : IEntityTypeConfiguration<QuestionRevisionOption>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<QuestionRevisionOption> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("question_revision_options");
        builder.HasKey(option => option.Id);

        builder.Property(option => option.Code).HasMaxLength(128).IsRequired();
        builder.Property(option => option.LabelEn).IsRequired();
        builder.Property(option => option.LabelFr).IsRequired();

        // A code never changes, and never repeats within a revision — that is
        // what lets a rename be a translation change rather than a data
        // migration.
        builder.HasIndex(option => new { option.QuestionRevisionId, option.Code }).IsUnique();
    }
}
