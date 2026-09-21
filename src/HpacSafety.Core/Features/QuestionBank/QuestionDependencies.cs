namespace HpacSafety.Core.Features.QuestionBank;

/// <summary>
/// The rules that no single question can check on its own, because they are
/// about its relationship to the others.
/// </summary>
/// <remarks>
/// <para>
/// <see cref="QuestionRevision"/> checks everything visible from inside one
/// revision — that a dependency is not self-referential, that the system
/// question is not made conditional. It cannot check the <i>parent's</i>
/// current type, or, for a single-select parent, whether it currently offers
/// the required option — those facts live on the parent's current revision,
/// which is a different row. Nor can it see a cycle. All three are checked
/// here, against the live bank, and the database is not asked to enforce
/// them: doing so would take a trigger reading a second table, and a trigger
/// is a rule hidden from everyone reading the C#. See ADR-0060, ADR-0074.
/// </para>
/// <para>
/// This is a static rule-checker over a collection, not a repository. It does
/// no I/O — the caller loads the live questions and hands them in.
/// </para>
/// </remarks>
public static class QuestionDependencies
{
    /// <summary>
    /// Checks that a question may depend on the one it names: the parent
    /// exists, is live, is a type that can enable another question, currently
    /// offers <paramref name="requiredOptionCode"/> when it needs one, and
    /// does not lead back to the child.
    /// </summary>
    /// <param name="questions">Every live question, including the child if it already exists.</param>
    /// <param name="childId">The question being made conditional, or null when it is being created.</param>
    /// <param name="parentId">The question it is to depend on.</param>
    /// <param name="requiredOptionCode">
    /// The option code the parent must be answered with, when the parent is
    /// single-select. Must be null for a yes/no parent, whose condition is
    /// the invariant "yes". See ADR-0074.
    /// </param>
    /// <exception cref="DomainRuleViolationException">When the dependency is not allowed.</exception>
    public static void EnsureDependencyAllowed(
        IReadOnlyCollection<Question> questions, TinyId? childId, TinyId parentId, string? requiredOptionCode = null)
    {
        ArgumentNullException.ThrowIfNull(questions);

        if (childId == parentId)
        {
            throw new DomainRuleViolationException("A question cannot be conditional on itself.");
        }

        var parent = questions.FirstOrDefault(question => question.Id == parentId && question.Deleted is null)
            ?? throw new DomainRuleViolationException(
                "That question no longer exists, so nothing can be made conditional on it.");

        switch (parent.Type)
        {
            case QuestionType.YesNo when requiredOptionCode is not null:
                throw new DomainRuleViolationException(
                    $"'{parent.Key}' is a yes/no question. Its condition is always 'answered yes' and cannot also name a required option.");

            case QuestionType.YesNo:
                break;

            case QuestionType.SingleSelect when requiredOptionCode is null:
                throw new DomainRuleViolationException(
                    $"'{parent.Key}' is a single-select question and needs a required option to enable another one.");

            case QuestionType.SingleSelect
                when parent.CurrentRevision.Option(QuestionKey.Normalize(requiredOptionCode)) is null:
                throw new DomainRuleViolationException(
                    $"'{parent.Key}' does not currently offer the option '{requiredOptionCode}'.");

            case QuestionType.SingleSelect:
                break;

            default:
                throw new DomainRuleViolationException(
                    $"'{parent.Key}' is a {EnumCode.Of(parent.Type)} question. Only a yes/no or single-select question can enable another one.");
        }

        if (childId is { } child && LeadsTo(questions, parentId, child))
        {
            throw new DomainRuleViolationException(
                $"'{parent.Key}' already depends on this question, directly or through another one. A cycle would leave both permanently disabled.");
        }
    }

    /// <summary>
    /// Whether following <see cref="Question.DependsOnQuestionId"/> from
    /// <paramref name="from"/> reaches <paramref name="target"/>. The visited
    /// set is what stops a cycle already in the data from looping here.
    /// </summary>
    private static bool LeadsTo(IReadOnlyCollection<Question> questions, TinyId from, TinyId target)
    {
        var visited = new HashSet<TinyId>();
        var current = from;

        while (visited.Add(current))
        {
            var question = questions.FirstOrDefault(candidate => candidate.Id == current);

            if (question?.DependsOnQuestionId is not { } next)
            {
                return false;
            }

            if (next == target)
            {
                return true;
            }

            current = next;
        }

        return false;
    }
}
