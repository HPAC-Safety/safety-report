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
        builder.Property(question => question.Role).IsRequired();
        builder.Property(question => question.IsSystem).IsRequired();

        // Order, section, privacy, and active state live on the revision, not
        // here — a referenced revision must preserve the complete question
        // exactly as it was shown. Question.IsPrivate/DisplayOrder/SectionKey/
        // IsActive are computed pass-throughs to CurrentRevision and are
        // therefore not mapped.
        builder.Ignore(question => question.IsPrivate);
        builder.Ignore(question => question.IsRequired);
        builder.Ignore(question => question.DependsOnQuestionId);
        builder.Ignore(question => question.DisplayOrder);
        builder.Ignore(question => question.SectionKey);
        builder.Ignore(question => question.IsActive);

        // Unique among live questions only. A fork chain shares one key — the
        // retired questions and the one being asked today — and exactly one of
        // them is live, which is the rule the form resolves by (ADR-0071).
        builder.HasIndex(question => question.Key)
            .IsUnique()
            .HasFilter("deleted IS NULL");

        builder.ToTable(t => t.HasCheckConstraint(
            "ck_questions_role",
            "role IN ('none', 'consent_publish')"));

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
        builder.Property(revision => revision.IsSystem).IsRequired();
        builder.Property(revision => revision.IsRequired).IsRequired();
        builder.Property(revision => revision.IsPrivate).IsRequired();
        builder.Property(revision => revision.IsActive).IsRequired();
        builder.Property(revision => revision.DisplayOrder).IsRequired();
        builder.Property(revision => revision.SectionKey).HasMaxLength(128);

        // A conditional question names the stable question, not a revision of
        // it, so rewording the parent cannot break the child. Restrict, not
        // Cascade: a parent is soft-deleted like everything else here, and a
        // real cascade would take the child's history with it. See ADR-0060.
        builder.HasOne<Question>()
            .WithMany()
            .HasForeignKey(revision => revision.DependsOnQuestionId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(revision => revision.DependsOnQuestionId);

        // Provenance of the option snapshot, never consulted to render one.
        // SetNull so retiring a set leaves every revision built from it intact
        // and merely unattributed. See ADR-0058.
        builder.HasOne<OptionSet>()
            .WithMany()
            .HasForeignKey(revision => revision.OptionSetId)
            .OnDelete(DeleteBehavior.SetNull);

        // Unique stable key + revision number.
        builder.HasIndex(revision => new { revision.QuestionId, revision.RevisionNumber }).IsUnique();

        // Ties by stable key break ties in sort order — see
        // features/question-bank-and-form/question-bank-and-form.feature.
        builder.HasIndex(revision => new { revision.IsActive, revision.DisplayOrder });

        builder.ToTable(t => t.HasCheckConstraint(
            "ck_question_revisions_type",
            "type IN ('short_text', 'long_text', 'email', 'phone', 'date', 'number', 'single_select', " +
            "'multi_select', 'yes_no', 'checkbox', 'file_upload', 'statement', 'group', 'time', " +
            "'autocomplete')"));

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

        // Which shared item this was copied from, when it was copied from one.
        // Restrict rather than Cascade: the snapshot outlives the item on
        // purpose, and losing it would rewrite what a reporter was shown. See
        // ADR-0058.
        builder.HasOne<OptionSetItem>()
            .WithMany()
            .HasForeignKey(option => option.SourceItemId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
