namespace HpacSafety.Acceptance.Tests;

/// <summary>
///     Features tagged <c>@xunit:collection(QuestionBankRunsAlone)</c> run alone,
///     after everything that runs in parallel. Deleting or retyping a group
///     (REQ-QB-052) and rearranging the form revise every question that has to
///     move, and the booted host's question bank is shared by every scenario: a
///     parallel scenario that had read a question's revision would then be refused
///     for answering one that is no longer current (ADR-0185).
/// </summary>
[CollectionDefinition(Name, DisableParallelization = true)]
public sealed class QuestionBankRunsAlone
{
	/// <summary>The collection name the feature tag refers to.</summary>
	public const string Name = "QuestionBankRunsAlone";
}
