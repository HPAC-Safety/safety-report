using HpacSafety.Core.Features.QuestionBank;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HpacSafety.Infrastructure.Persistence.Configurations;

/// <summary>Maps complete immutable question revisions.</summary>
public sealed class QuestionConfiguration : IEntityTypeConfiguration<Question>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<Question> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("questions");
        builder.HasKey(question => question.Id);

        builder.Property(question => question.Key).HasMaxLength(128).IsRequired();
        builder.Property(question => question.Type).IsRequired();
        builder.Property(question => question.LabelEn).IsRequired();
        builder.Property(question => question.LabelFr).IsRequired();
        builder.Property(question => question.SectionKey).HasMaxLength(128);

        builder.HasIndex(question => new { question.Key, question.Revision }).IsUnique();
        builder.HasIndex(question => new { question.Key, question.IsActive, question.Revision });
        builder.HasIndex(question => question.SupersedesQuestionId).IsUnique();

        builder.HasOne<Question>()
            .WithOne()
            .HasForeignKey<Question>(question => question.SupersedesQuestionId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasMany(question => question.Options)
            .WithOne()
            .HasForeignKey(option => option.QuestionId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Metadata.FindNavigation(nameof(Question.Options))!
            .SetPropertyAccessMode(PropertyAccessMode.Field);
    }
}

/// <summary>Maps bilingual options owned by one immutable question revision.</summary>
public sealed class QuestionOptionConfiguration : IEntityTypeConfiguration<QuestionOption>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<QuestionOption> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("question_options");
        builder.HasKey(option => option.Id);

        builder.Property(option => option.Code).HasMaxLength(128).IsRequired();
        builder.Property(option => option.LabelEn).IsRequired();
        builder.Property(option => option.LabelFr).IsRequired();

        builder.HasIndex(option => new { option.QuestionId, option.Code }).IsUnique();
    }
}
