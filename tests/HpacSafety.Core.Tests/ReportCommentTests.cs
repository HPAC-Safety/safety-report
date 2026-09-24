using HpacSafety.Core.Features.Comments;
using HpacSafety.Core.Features.Reporting;
using Shouldly;

namespace HpacSafety.Core.Tests;

/// <summary>
///     A member's comment on a published report (ADR-0114): only its author
///     changes it, edits keep every revision, and nothing is ever erased.
/// </summary>
public sealed class ReportCommentTests
{
	private const string Author = "member:author";
	private const string Other = "member:other";
	private static readonly DateTimeOffset At = new(2026, 9, 24, 9, 0, 0, TimeSpan.Zero);

	[Fact]
	public void GivenText_WhenPosted_ThenFirstRevisionIsTrimmedAndCurrent()
	{
		// When
		var comment = Posted("  Synthetic: pick the field early.  ");

		// Then
		comment.Current.Number.ShouldBe(1);
		comment.Current.Text.ShouldBe("Synthetic: pick the field early.");
		comment.Current.Locale.ShouldBe(Locale.EnCa);
		comment.AuthorSubject.ShouldBe(Author);
	}

	[Theory]
	[InlineData(null)]
	[InlineData("")]
	[InlineData("   ")]
	public void GivenNoText_WhenPosted_ThenRefused(string? text)
	{
		// Then
		Should.Throw<DomainRuleViolationException>(() => Posted(text));
	}

	[Fact]
	public void GivenTextOverTheLimit_WhenPosted_ThenRefused()
	{
		// Then
		Should.Throw<DomainRuleViolationException>(() => Posted(new string('a', ReportComment.MaxLength + 1)));
	}

	[Fact]
	public void GivenAuthor_WhenEdited_ThenNewRevisionIsCurrentAndOldIsKept()
	{
		// Given
		var comment = Posted("Synthetic first.");

		// When
		var revision = comment.Edit(Author, "Synthétique second.", Locale.FrCa, At.AddMinutes(5));

		// Then
		comment.Current.ShouldBe(revision);
		revision.Number.ShouldBe(2);
		revision.Locale.ShouldBe(Locale.FrCa);
		comment.Revisions.Select(candidate => candidate.Text).ShouldBe(["Synthetic first.", "Synthétique second."]);
	}

	[Fact]
	public void GivenAnotherMember_WhenEditingOrDeleting_ThenRefused()
	{
		// Given
		var comment = Posted("Synthetic.");

		// Then
		Should.Throw<CommentNotYoursException>(() => comment.Edit(Other, "Takeover.", Locale.EnCa, At));
		Should.Throw<CommentNotYoursException>(() => comment.DeleteBy(Other, At));
		comment.Deleted.ShouldBeNull();
	}

	[Fact]
	public void GivenAuthor_WhenDeletedTwice_ThenCommentAndRevisionsKeepTheFirstStamp()
	{
		// Given
		var comment = Posted("Synthetic.");
		comment.Edit(Author, "Synthetic, edited.", Locale.EnCa, At);

		// When
		comment.DeleteBy(Author, At);
		comment.DeleteBy(Author, At.AddHours(1));

		// Then
		comment.Deleted.ShouldBe(At);
		comment.Revisions.ShouldAllBe(revision => revision.Deleted == At);
	}

	[Fact]
	public void GivenDeletedOrHiddenComment_WhenEdited_ThenRefused()
	{
		// Given
		var deleted = Posted("Synthetic.");
		deleted.DeleteBy(Author, At);
		var hidden = Posted("Synthetic.");
		hidden.HideBy("officer", At);

		// Then
		Should.Throw<DomainRuleViolationException>(() => deleted.Edit(Author, "Again.", Locale.EnCa, At));
		Should.Throw<DomainRuleViolationException>(() => hidden.Edit(Author, "Again.", Locale.EnCa, At));
	}

	[Fact]
	public void GivenReviewer_WhenHiddenTwice_ThenFirstHideIsKept()
	{
		// Given
		var comment = Posted("Synthetic.");

		// When
		comment.HideBy("officer:first", At);
		comment.HideBy("officer:second", At.AddHours(1));

		// Then
		comment.HiddenAt.ShouldBe(At);
		comment.HiddenBySubject.ShouldBe("officer:first");
	}

	[Fact]
	public void GivenRevision_WhenTranslatedTwice_ThenFirstTranslationIsKept()
	{
		// Given
		var revision = Posted("Synthetic.").Current;

		// When
		revision.SupplyAutoTranslation("Synthétique.");
		revision.SupplyAutoTranslation("Autre.");

		// Then
		revision.TranslatedText.ShouldBe("Synthétique.");
		revision.TranslationSource.ShouldBe(TranslationSource.Auto);
	}

	private static ReportComment Posted(string? text)
	{
		return ReportComment.Post(TinyId.New(), Author, text, Locale.EnCa, At);
	}
}
