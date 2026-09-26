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

		// Order, privacy, and active state live on the revision, not
		// here — a referenced revision must preserve the complete question
		// exactly as it was shown. Question.IsPrivate/DisplayOrder/
		// IsActive are computed pass-throughs to CurrentRevision and are
		// therefore not mapped.
		builder.Ignore(question => question.IsPrivate);
		builder.Ignore(question => question.IsRequired);
		builder.Ignore(question => question.DependsOnQuestionId);
		builder.Ignore(question => question.DisplayOrder);
		builder.Ignore(question => question.IsActive);
		builder.Ignore(question => question.Choices);
		builder.Ignore(question => question.TakesReporterAdditions);
		builder.Ignore(question => question.ReporterChoicesAwaitingReview);

		// Unique among live questions only. A fork chain shares one key — the
		// retired questions and the one being asked today — and exactly one of
		// them is live, which is the rule the form resolves by (ADR-0071).
		builder.HasIndex(question => question.Key)
			.IsUnique()
			.HasFilter("deleted IS NULL");

		builder.ToTable(t => t.HasCheckConstraint(
			"ck_questions_role",
			"role IN ('none', 'consent_publish', 'consent_media')"));

		builder.HasMany(question => question.Revisions)
			.WithOne()
			.HasForeignKey(revision => revision.QuestionId)
			.OnDelete(DeleteBehavior.Cascade);

		builder.Metadata.FindNavigation(nameof(Question.Revisions))!
			.SetPropertyAccessMode(PropertyAccessMode.Field);

		// A question owns its choices (ADR-0095). Cascade matches Revisions: a
		// choice is part of its question and is never physically deleted apart
		// from it.
		builder.HasMany(question => question.AllChoices)
			.WithOne()
			.HasForeignKey(choice => choice.QuestionId)
			.OnDelete(DeleteBehavior.Cascade);

		builder.Navigation(question => question.AllChoices)
			.HasField("_choices")
			.UsePropertyAccessMode(PropertyAccessMode.Field);

		// The question whose answer decides which of this one's choices are
		// offered (ADR-0145). On the question, not a revision, so setting it never
		// revises or forks. Restrict: a question is retired by a stamp, never
		// erased. Which types, one level only, and form order are checked by
		// ChoiceDependencies against the current revisions.
		builder.HasOne<Question>()
			.WithMany()
			.HasForeignKey(question => question.ChoicesDependOnQuestionId)
			.OnDelete(DeleteBehavior.Restrict);
		builder.ToTable(t => t.HasCheckConstraint(
			"ck_questions_choices_depend_on_other",
			"choices_depend_on_question_id IS NULL OR choices_depend_on_question_id <> id"));
	}
}

/// <summary>
///     The <c>question_revisions</c> table. A revision is complete and immutable
///     once written: both official languages are present from the start.
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
		builder.Property(revision => revision.IsTranslatable).IsRequired();
		builder.Property(revision => revision.AllowFutureDates).IsRequired();
		builder.Property(revision => revision.IsActive).IsRequired();
		builder.Property(revision => revision.DisplayOrder).IsRequired();
		builder.Ignore(revision => revision.TakesReporterAdditions);

		// A conditional question names the stable question, not a revision of
		// it, so rewording the parent cannot break the child. Restrict, not
		// Cascade: a parent is soft-deleted like everything else here, and a
		// real cascade would take the child's history with it. See ADR-0060.
		builder.HasOne<Question>()
			.WithMany()
			.HasForeignKey(revision => revision.DependsOnQuestionId)
			.OnDelete(DeleteBehavior.Restrict);

		builder.HasIndex(revision => revision.DependsOnQuestionId);

		// A group membership names the stable question too, for the same
		// reason a dependency does, and is deliberately a separate column from
		// DependsOnQuestionId — "display together" is never "conditional on".
		// See ADR-0076.
		builder.HasOne<Question>()
			.WithMany()
			.HasForeignKey(revision => revision.GroupedUnderQuestionId)
			.OnDelete(DeleteBehavior.Restrict);

		builder.HasIndex(revision => revision.GroupedUnderQuestionId);

		// The single-select parent's required choice, by identifier. A condition
		// follows that choice through any replacement, so this row is never
		// rewritten when the parent's option is replaced. See ADR-0074, ADR-0128.
		builder.HasOne<QuestionChoice>()
			.WithMany()
			.HasForeignKey(revision => revision.DependsOnChoiceId)
			.OnDelete(DeleteBehavior.Restrict);

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

		// Only free text is ever machine-translated (ADR-0112).
		builder.ToTable(t => t.HasCheckConstraint(
			"ck_question_revisions_translatable_text",
			"NOT is_translatable OR type IN ('short_text', 'long_text')"));

		// Only a date question can allow a future date (ADR-0138).
		builder.ToTable(t => t.HasCheckConstraint(
			"ck_question_revisions_future_dates_date",
			"NOT allow_future_dates OR type = 'date'"));
	}
}

/// <summary>
///     The <c>question_choices</c> table: a question's own choices, outside its
///     revisions and edited in place. See ADR-0095.
/// </summary>
public sealed class QuestionChoiceConfiguration : IEntityTypeConfiguration<QuestionChoice>
{
	/// <inheritdoc />
	public void Configure(EntityTypeBuilder<QuestionChoice> builder)
	{
		ArgumentNullException.ThrowIfNull(builder);

		builder.ToTable("question_choices");
		builder.HasKey(choice => choice.Id);

		builder.Property(choice => choice.Code).HasMaxLength(128).IsRequired();
		builder.Property(choice => choice.DisplayOrder).IsRequired();

		// Listed before or after the alphabetical rest, or among them (ADR-0136).
		// Every choice that existed before pinning is among them.
		builder.Property(choice => choice.Pin).IsRequired().HasDefaultValue(ChoicePin.None);
		builder.ToTable(t => t.HasCheckConstraint("ck_question_choices_pin", "pin IN ('none', 'first', 'last')"));
		builder.Property(choice => choice.AddedByReporter).IsRequired().HasDefaultValue(false);
		builder.Property(choice => choice.ReporterLocale);

		// The review a reporter-added type-ahead value waits for (ADR-0129). A
		// reviewer is a token subject, never a key: there is no user table.
		builder.Property(choice => choice.NeedsReview).IsRequired().HasDefaultValue(false);
		builder.Property(choice => choice.ReviewedBy).HasMaxLength(256);
		builder.Ignore(choice => choice.Resolved);

		// A merged value names the one it reads as (ADR-0129). Merges are
		// flattened when made, and a merged value is always removed.
		builder.HasOne(choice => choice.MergedInto)
			.WithMany()
			.HasForeignKey(choice => choice.MergedIntoChoiceId)
			.OnDelete(DeleteBehavior.Restrict);
		builder.ToTable(t => t.HasCheckConstraint(
			"ck_question_choices_merged_is_removed",
			"merged_into_choice_id IS NULL OR (deleted IS NOT NULL AND merged_into_choice_id <> id)"));
		builder.Ignore(choice => choice.NeedsTranslation);

		// How each language was produced: written by a person, or supplied by the
		// Worker (ADR-0129). Present exactly when its label is.
		builder.ToTable(t => t.HasCheckConstraint(
			"ck_question_choices_label_source",
			"(label_en_source IS NULL OR label_en_source IN ('human', 'auto')) AND (label_fr_source IS NULL OR label_fr_source IN ('human', 'auto')) AND (label_en IS NULL) = (label_en_source IS NULL) AND (label_fr IS NULL) = (label_fr_source IS NULL)"));

		// A replaced picker option names the option that replaced it, on the same
		// question. Both rows are kept for good (ADR-0128).
		builder.HasOne<QuestionChoice>()
			.WithMany()
			.HasForeignKey(choice => choice.ReplacedByChoiceId)
			.OnDelete(DeleteBehavior.Restrict);

		// The parent question's choice this one is offered under, when its
		// question's choices depend on another's (ADR-0145). Changed, never
		// cleared; which question it belongs to is checked by ChoiceDependencies.
		builder.HasOne<QuestionChoice>()
			.WithMany()
			.HasForeignKey(choice => choice.ParentChoiceId)
			.OnDelete(DeleteBehavior.Restrict);
		builder.ToTable(t => t.HasCheckConstraint(
			"ck_question_choices_parent_other",
			"parent_choice_id IS NULL OR parent_choice_id <> id"));

		// Unique across removed rows too: an Administrator writing a removed

		// choice again revives that row, and a reporter never does, so a code
		// has exactly one row on its question for life.
		builder.HasIndex(choice => new { choice.QuestionId, choice.Code }).IsUnique();

		// Only a reporter-added choice, or a removed one the choice-reference
		// migration made so an old answer resolves (ADR-0128), may lack a
		// language, and never both.
		builder.ToTable(t => t.HasCheckConstraint(
			"ck_question_choices_label",
			"label_en IS NOT NULL AND label_fr IS NOT NULL OR (added_by_reporter OR deleted IS NOT NULL) AND (label_en IS NOT NULL OR label_fr IS NOT NULL)"));
	}
}
