using HpacSafety.Infrastructure.Persistence.Seeding;
using Shouldly;

namespace HpacSafety.Infrastructure.Tests.Seeding;

/// <summary>
///     The seed is empty for now — see <see cref="QuestionBankSeed" />'s remarks. A
///     follow-up change replaces this with a correct question set and the tests
///     that hold it to <c>docs/form-spec.md</c> or its successor.
/// </summary>
public sealed class QuestionBankSeedTests
{
	[Fact]
	public void GivenCurrentQuestionBankSeed_WhenRead_ThenSeedsNoQuestions()
	{
		QuestionBankSeed.Questions.ShouldBeEmpty();
	}
}
