using HpacSafety.Core.Features.QuestionBank;
using HpacSafety.Core.Features.QuestionBank.Typeform;
using Shouldly;

namespace HpacSafety.Core.Tests;

/// <summary>
///     A condition follows its parent through a fork: the retired parent resolves to
///     the live question with its key, and its choice to the copy there (ADR-0132).
/// </summary>
public class ForkedParentConditionTests
{
	private static readonly DateTimeOffset At = new(2026, 9, 25, 12, 0, 0, TimeSpan.Zero);

	[Fact]
	public void GivenAForkedParent_WhenTheConditionIsResolved_ThenItNamesTheReplacementAndItsCopy()
	{
		// Given
		var (parent, dependent) = ParentAndDependent();
		var replacement = Fork(parent);

		// When
		var bank = new[] { parent, replacement, dependent };

		// Then
		QuestionDependencies.ParentToday(bank, parent.Id).ShouldBeSameAs(replacement);
		QuestionDependencies.RequiredChoiceToday(bank, dependent.CurrentRevision)
			.ShouldBeSameAs(replacement.Choices.Single(choice => choice.Code == "paraglider"));
	}

	[Fact]
	public void GivenAParentDeletedRatherThanForked_WhenTheConditionIsResolved_ThenNothing()
	{
		// Given
		var (parent, dependent) = ParentAndDependent();
		parent.Delete(hasBeenAnswered: false, At.AddDays(1));

		// When
		var bank = new[] { parent, dependent };

		// Then — no live question carries its key
		QuestionDependencies.ParentToday(bank, parent.Id).ShouldBeNull();
		QuestionDependencies.RequiredChoiceToday(bank, dependent.CurrentRevision).ShouldBeNull();
	}

	[Fact]
	public void GivenAParentNotLoaded_WhenTheConditionIsResolved_ThenNothing()
	{
		// Given
		var (parent, dependent) = ParentAndDependent();

		// When / Then
		QuestionDependencies.ParentToday([dependent], parent.Id).ShouldBeNull();
		QuestionDependencies.RequiredChoiceToday([dependent], dependent.CurrentRevision).ShouldBeNull();
	}

	[Fact]
	public void GivenAForkedParent_WhenTheRequiredChoicesCopyIsRemoved_ThenTheSaveIsRefused()
	{
		// Given
		var (parent, dependent) = ParentAndDependent();
		var replacement = Fork(parent);

		// When
		var removing = () => QuestionDependencies.EnsureChoicesRemovable(
			[parent, replacement, dependent], replacement, ["hang_glider"]);

		// Then
		removing.ShouldThrow<DomainRuleViolationException>().Message.ShouldContain("Paraglider");
	}

	[Fact]
	public void GivenADependentOnAForkedParent_WhenTheReplacementIsMadeConditionalOnIt_ThenTheCycleIsRefused()
	{
		// Given — a yes/no dependent, which could itself enable another question
		var parent = ParentAndDependent().Parent;
		var dependent = Question.Create(
			"flew_tandem", QuestionType.YesNo, "Tandem?", "Biplace?", At, isActive: true,
			dependsOnQuestionId: parent.Id,
			dependsOnChoiceId: parent.Choices.Single(choice => choice.Code == "paraglider").Id);
		var replacement = Fork(parent);

		// When
		var cycling = () => QuestionDependencies.EnsureDependencyAllowed(
			[parent, replacement, dependent], replacement.Id, dependent.Id);

		// Then — dependent leads to the replacement through the fork
		cycling.ShouldThrow<DomainRuleViolationException>().Message.ShouldContain("cycle");
	}

	[Fact]
	public void GivenAForkedParent_WhenExported_ThenTheConditionNamesTheReplacementAndTheChoiceCode()
	{
		// Given
		var (parent, dependent) = ParentAndDependent();
		var replacement = Fork(parent);

		// When
		var (english, _) = TypeformExportBuilder.Build([replacement, dependent], [parent, replacement, dependent]);

		// Then
		var hpac = english.Fields.Single(field => field.Ref == dependent.Key).Properties.Hpac!;
		hpac.DependsOnKey.ShouldBe(replacement.Key);
		hpac.DependsOnOptionCode.ShouldBe("paraglider");
	}

	private static (Question Parent, Question Dependent) ParentAndDependent()
	{
		var parent = Question.Create(
			"wing", QuestionType.SingleSelect, "Wing", "Aile", At, isActive: true,
			options: [new QuestionOptionInput("hang_glider", "Hang glider", "Deltaplane"), new QuestionOptionInput("paraglider", "Paraglider", "Parapente")]);
		var dependent = Question.Create(
			"rating", QuestionType.ShortText, "Rating", "Qualification", At, isActive: true,
			dependsOnQuestionId: parent.Id,
			dependsOnChoiceId: parent.Choices.Single(choice => choice.Code == "paraglider").Id);
		return (parent, dependent);
	}

	private static Question Fork(Question parent)
	{
		var current = parent.CurrentRevision;
		var replacement = parent.ApplyEdit(
			true, current.Type, "Which wing were you flying?", current.LabelFr, current.IsPrivate, current.IsActive,
			current.DisplayOrder, At.AddDays(1));
		replacement.ShouldNotBeSameAs(parent);
		return replacement;
	}
}
