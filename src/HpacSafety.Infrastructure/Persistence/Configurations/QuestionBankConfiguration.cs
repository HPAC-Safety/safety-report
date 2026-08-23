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

        builder.HasMany(question => question.Versions)
            .WithOne()
            .HasForeignKey(version => version.QuestionId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Metadata.FindNavigation(nameof(Question.Versions))!
            .SetPropertyAccessMode(PropertyAccessMode.Field);
    }
}

/// <summary>The <c>question_versions</c> table. A version is immutable once written.</summary>
public sealed class QuestionVersionConfiguration : IEntityTypeConfiguration<QuestionVersion>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<QuestionVersion> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("question_versions");
        builder.HasKey(version => version.Id);

        builder.Property(version => version.Type).IsRequired();

        builder.HasIndex(version => new { version.QuestionId, version.VersionNumber }).IsUnique();

        builder.HasMany(version => version.Options)
            .WithOne()
            .HasForeignKey(option => option.QuestionVersionId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(version => version.Translations)
            .WithOne()
            .HasForeignKey(translation => translation.QuestionVersionId)
            .OnDelete(DeleteBehavior.Cascade);

        foreach (var navigation in builder.Metadata.GetNavigations())
        {
            navigation.SetPropertyAccessMode(PropertyAccessMode.Field);
        }
    }
}

/// <summary>The <c>question_options</c> table.</summary>
public sealed class QuestionOptionConfiguration : IEntityTypeConfiguration<QuestionOption>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<QuestionOption> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("question_options");
        builder.HasKey(option => option.Id);

        builder.Property(option => option.Code).HasMaxLength(128).IsRequired();

        // A code never changes, and never repeats within a version — that is
        // what lets a rename be a translation change rather than a data
        // migration.
        builder.HasIndex(option => new { option.QuestionVersionId, option.Code }).IsUnique();

        builder.HasMany(option => option.Translations)
            .WithOne()
            .HasForeignKey(translation => translation.QuestionOptionId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Metadata.FindNavigation(nameof(QuestionOption.Translations))!
            .SetPropertyAccessMode(PropertyAccessMode.Field);
    }
}

/// <summary>
/// The <c>question_translations</c> table — the same <c>is_source</c> and
/// <c>is_machine_translated</c> shape <c>summaries</c> uses.
/// </summary>
public sealed class QuestionTranslationConfiguration : IEntityTypeConfiguration<QuestionTranslation>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<QuestionTranslation> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("question_translations");
        builder.HasKey(translation => translation.Id);

        builder.Property(translation => translation.Locale).IsRequired();
        builder.Property(translation => translation.Label).IsRequired();

        builder.HasIndex(translation => new { translation.QuestionVersionId, translation.Locale }).IsUnique();
    }
}

/// <summary>The <c>question_option_translations</c> table.</summary>
public sealed class QuestionOptionTranslationConfiguration : IEntityTypeConfiguration<QuestionOptionTranslation>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<QuestionOptionTranslation> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("question_option_translations");
        builder.HasKey(translation => translation.Id);

        builder.Property(translation => translation.Locale).IsRequired();
        builder.Property(translation => translation.Label).IsRequired();

        builder.HasIndex(translation => new { translation.QuestionOptionId, translation.Locale }).IsUnique();
    }
}
