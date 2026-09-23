using HpacSafety.Core.Features.QuestionBank;
using HpacSafety.Infrastructure.Persistence.Seeding;
using Shouldly;

namespace HpacSafety.Infrastructure.Tests.Seeding;

/// <summary>
///     The organization's real form, seeded via the Typeform importer and
///     reviewed by hand (ADR-0077, ADR-0078) — see <see cref="QuestionBankSeed" />'s
///     remarks.
/// </summary>
public sealed class QuestionBankSeedTests
{
	[Fact]
	public void GivenCurrentQuestionBankSeed_WhenRead_ThenSeedsTheRealForm()
	{
		QuestionBankSeed.Questions.ShouldNotBeEmpty();
	}

	[Fact]
	public void GivenCurrentQuestionBankSeed_WhenRead_ThenExactlyOneConsentPublishQuestion()
	{
		QuestionBankSeed.Questions.Count(question => question.Role == QuestionRole.ConsentPublish).ShouldBe(1);
	}

	[Fact]
	public void GivenCurrentQuestionBankSeed_WhenRead_ThenEveryKeyIsUnique()
	{
		QuestionBankSeed.Questions.Select(question => question.Key).Distinct().Count()
			.ShouldBe(QuestionBankSeed.Questions.Count);
	}

	[Fact]
	public void GivenCurrentQuestionBankSeed_WhenRead_ThenEveryGroupedUnderKeyNamesAGroupQuestion()
	{
		var groupKeys = QuestionBankSeed.Questions
			.Where(question => question.Type == QuestionType.Group)
			.Select(question => question.Key)
			.ToHashSet();

		foreach (var question in QuestionBankSeed.Questions.Where(question => question.GroupedUnderKey is not null))
		{
			groupKeys.ShouldContain(question.GroupedUnderKey);
		}
	}

	[Fact]
	public void GivenCurrentQuestionBankSeed_WhenRead_ThenNoQuestionThatCollectsNoAnswerIsRequiredOrPrivate()
	{
		foreach (var question in QuestionBankSeed.Questions.Where(question =>
					 question.Type is QuestionType.Statement or QuestionType.Group))
		{
			question.IsRequired.ShouldBeFalse();
			question.IsPrivate.ShouldBeFalse();
		}
	}
}
