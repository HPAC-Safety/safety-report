namespace HpacSafety.Core.Features.QuestionBank;

/// <summary>
///     A question on the occurrence form. The question set is data — an
///     administrator adds, rewords, retypes, reorders, and removes questions without
///     a deploy — so this is the aggregate root of the question bank.
/// </summary>
/// <remarks>
///     <para>
///         Two questions are <b>system questions</b>: publication consent, the gate
///         every publication path checks, and media consent, the gate on a published
///         report's photos and video (ADR-0117). Neither can be deleted, deactivated,
///         retyped, rekeyed, made conditional, or given another role, because there is
///         no defined behaviour without them. Their wording is still editable, and
///         they reorder like any other.
///     </para>
///     <para>
///         Nothing but the two consents projects onto a typed property of
///         <see cref="Reporting.Report" /> — the admin review DTO reads exact asked
///         questions and answers directly. See <c>docs/data-and-persistence.md</c>.
///     </para>
///     <para>
///         Order, privacy, active state, system state, and required state all
///         live on <see cref="QuestionRevision" />, not
///         here — a referenced revision has to preserve the complete question exactly
///         as it was shown, and none of those facts can be reconstructed from the
///         current state of a mutable question row. Every read here that looks
///         question-scoped (<see cref="IsPrivate" />, <see cref="DisplayOrder" />,
///         <see cref="IsActive" />) reads through to
///         <see cref="CurrentRevision" />, and every change to one of them is made by
///         creating a new revision. See
///         <c>features/question-bank-and-form/question-bank-and-form.feature</c>.
///     </para>
///     <para>
///         Choices are the exception, deliberately: they live here, on
///         <see cref="QuestionChoice" /> rows the question owns, and are edited in
///         place without a revision or a fork (ADR-0095). An answer names its
///         choice and reads its wording there, so a choice an answer names is
///         removed by a stamp, never erased (ADR-0128).
///     </para>
/// </remarks>
public class Question
{
	private readonly List<QuestionChoice> _choices = [];
	private readonly List<QuestionRevision> _revisions = [];

	// EF Core materializes an entity by calling this constructor and then
	// setting every mapped property and backing field directly. It exists for
	// the ORM and for nothing else — domain code still has to go through the
	// constructor or factory that follows, so no caller can reach a half-built
	// aggregate. See ADR-0019.
#pragma warning disable CS8618 // Every mapped property is set by EF Core immediately after this runs.
	private Question()
	{
	}
#pragma warning restore CS8618

	private Question(string key,
					 bool isSystem,
					 QuestionRole role,
					 DateTimeOffset at)
	{
		Id = TinyId.New();
		Key = QuestionKey.Normalize(key);
		IsSystem = isSystem;
		Role = role;
		CreatedAt = at;
	}

	/// <summary>Surrogate key.</summary>
	public TinyId Id { get; private init; }

	/// <summary>Stable invariant identity, used by exports and integrations.</summary>
	public string Key { get; private init; }

	/// <summary>True only for publication consent and media consent.</summary>
	public bool IsSystem { get; private init; }

	/// <summary>What downstream logic reads this answer for, if anything.</summary>
	public QuestionRole Role { get; private set; }

	/// <summary>
	///     Whether answers are private redaction context rather than facts eligible
	///     for the summary, on the current revision. See ADR-0038 and the class
	///     remarks: this is a revision field, changed by creating a new revision.
	/// </summary>
	public bool IsPrivate => CurrentRevision.IsPrivate;

	/// <summary>
	///     Whether a reporter must answer this question today. Authored —
	///     see <see cref="QuestionRevision.IsRequired" /> and ADR-0061.
	/// </summary>
	public bool IsRequired => CurrentRevision.IsRequired;

	/// <summary>The question this one is conditional on today, if any.</summary>
	public TinyId? DependsOnQuestionId => CurrentRevision.DependsOnQuestionId;

	/// <summary>
	///     The parent's choice a single-select parent must be answered with today,
	///     if any, as it was named — follow it with <see cref="CurrentChoice" /> on the
	///     parent. See ADR-0074, ADR-0128.
	/// </summary>
	public TinyId? DependsOnChoiceId => CurrentRevision.DependsOnChoiceId;

	/// <summary>The group question this one renders together with today, if any. See ADR-0076.</summary>
	public TinyId? GroupedUnderQuestionId => CurrentRevision.GroupedUnderQuestionId;

	/// <summary>Whether a reporter's value this question does not offer is added as a new choice. See ADR-0063, ADR-0095.</summary>
	public bool TakesReporterAdditions => CurrentRevision.TakesReporterAdditions;

	/// <summary>
	///     The choices the form offers today: pinned first, then unpinned, then pinned
	///     last, each group by identifier — a stable order, not an alphabetical one.
	///     The reader's browser sorts each group in their language (ADR-0136). Removed
	///     ones are left out.
	/// </summary>
	public IReadOnlyList<QuestionChoice> Choices =>
		[.. InListOrder(_choices.Where(choice => choice.Deleted is null))];

	/// <summary>Every choice this question has ever had, removed ones included. A fork copies all of them.</summary>
	public IReadOnlyCollection<QuestionChoice> AllChoices => _choices;

	/// <summary>
	///     How many type-ahead values a Safety Officer or Administrator has yet to
	///     review — a removed one a reporter typed again included (ADR-0129).
	/// </summary>
	public int ReporterChoicesAwaitingReview => _choices.Count(choice => choice.NeedsReview);

	/// <summary>
	///     Where this question sits on the form today. Not versioned
	///     independently — see the class remarks.
	/// </summary>
	public int DisplayOrder => CurrentRevision.DisplayOrder;

	/// <summary>
	///     Whether the public form asks this question today. Always false
	///     once the question itself is deleted, regardless of what the current
	///     revision says.
	/// </summary>
	public bool IsActive => Deleted is null && CurrentRevision.IsActive;

	/// <summary>When this question was created.</summary>
	public DateTimeOffset CreatedAt { get; private init; }

	/// <summary>When this question was retired, if it was.</summary>
	public DateTimeOffset? Deleted { get; private set; }

	/// <summary>Every revision, oldest first. Answers reference one of these.</summary>
	public IReadOnlyList<QuestionRevision> Revisions => _revisions;

	/// <summary>
	///     The revision the form asks today. Selected by the highest revision
	///     number rather than list position: EF Core does not guarantee the order
	///     of a loaded navigation collection, so the last element of
	///     <see cref="_revisions" /> can be an arbitrary historical row after a
	///     load.
	/// </summary>
	public QuestionRevision CurrentRevision =>
		_revisions.Count > 0
			? _revisions.MaxBy(revision => revision.RevisionNumber)!
			: throw new DomainRuleViolationException("A question always has at least one revision.");

	/// <summary>What this question currently asks for.</summary>
	public QuestionType Type => CurrentRevision.Type;

	/// <summary>Creates an ordinary question, complete in both official languages.</summary>
	public static Question Create(
		string key,
		QuestionType type,
		string labelEn,
		string labelFr,
		DateTimeOffset at,
		string? helpTextEn = null,
		string? helpTextFr = null,
		string? placeholderEn = null,
		string? placeholderFr = null,
		QuestionRole role = QuestionRole.None,
		bool isRequired = false,
		bool isPrivate = true,
		bool isActive = false,
		int displayOrder = 0,
		TinyId? dependsOnQuestionId = null,
		TinyId? dependsOnChoiceId = null,
		TinyId? groupedUnderQuestionId = null,
		IReadOnlyList<QuestionOptionInput>? options = null,
		bool? isTranslatable = null,
		bool? allowFutureDates = null)
	{
		return Create(
			key, type, labelEn, labelFr, at, false, helpTextEn, helpTextFr, placeholderEn, placeholderFr,
			role, isRequired, isPrivate, isActive, displayOrder, dependsOnQuestionId, dependsOnChoiceId,
			groupedUnderQuestionId, options, isTranslatable ?? QuestionRevision.TranslatableByDefault(type),
			allowFutureDates ?? false);
	}

	/// <summary>
	///     Creates the publication-consent question. The only question the system
	///     refuses to lose, and the only caller of this method.
	/// </summary>
	public static Question CreateConsentPublish(
		string labelEn,
		string labelFr,
		DateTimeOffset at,
		string? helpTextEn = null,
		string? helpTextFr = null,
		int displayOrder = 0)
	{
		return CreateSystem(QuestionKey.ConsentPublish, QuestionRole.ConsentPublish, labelEn, labelFr, at, helpTextEn, helpTextFr, displayOrder);
	}

	/// <summary>
	///     Creates the media-consent question (ADR-0117). The form asks it only when
	///     publication consent is yes and an image or video is attached; that is a
	///     built-in rule rather than an authored dependency, so it is never
	///     conditional here.
	/// </summary>
	public static Question CreateConsentMedia(
		string labelEn,
		string labelFr,
		DateTimeOffset at,
		string? helpTextEn = null,
		string? helpTextFr = null,
		int displayOrder = 0)
	{
		return CreateSystem(QuestionKey.ConsentMedia, QuestionRole.ConsentMedia, labelEn, labelFr, at, helpTextEn, helpTextFr, displayOrder);
	}

	private static Question CreateSystem(
		string key,
		QuestionRole role,
		string labelEn,
		string labelFr,
		DateTimeOffset at,
		string? helpTextEn,
		string? helpTextFr,
		int displayOrder)
	{
		return Create(
			key,
			QuestionType.YesNo,
			labelEn,
			labelFr,
			at,
			true,
			helpTextEn,
			helpTextFr,
			null,
			null,
			role,
			true,
			true,
			true,
			displayOrder,
			null,
			null,
			null,
			null,
			false,
			false);
	}

	private static Question Create(
		string key,
		QuestionType type,
		string labelEn,
		string labelFr,
		DateTimeOffset at,
		bool isSystem,
		string? helpTextEn,
		string? helpTextFr,
		string? placeholderEn,
		string? placeholderFr,
		QuestionRole role,
		bool isRequired,
		bool isPrivate,
		bool isActive,
		int displayOrder,
		TinyId? dependsOnQuestionId,
		TinyId? dependsOnChoiceId,
		TinyId? groupedUnderQuestionId,
		IReadOnlyList<QuestionOptionInput>? options,
		bool isTranslatable,
		bool allowFutureDates)
	{
		var question = new Question(key, isSystem, role, at);
		question._revisions.Add(
			QuestionRevision.Create(
				question.Id, 1, type, labelEn, labelFr, helpTextEn, helpTextFr, placeholderEn, placeholderFr,
				isSystem, isRequired, isPrivate, isActive, displayOrder, dependsOnQuestionId, dependsOnChoiceId,
				groupedUnderQuestionId, isTranslatable, allowFutureDates, at));
		question.ReplaceChoices(options ?? [], at);
		question.EnsureChoicesFitType();
		return question;
	}

	/// <summary>
	///     Rewords, retypes, reorders, moves, reclassifies, activates, or
	///     deactivates this question, producing one new complete bilingual
	///     revision. Choices are not part of a revision — see
	///     <see cref="ReplaceChoices" />. Answers already given keep pointing at the revision
	///     they were given under, so an old report still shows exactly what it was
	///     actually asked, including the order, privacy, and active state
	///     in force at the time.
	/// </summary>
	public QuestionRevision Revise(
		QuestionType type,
		string labelEn,
		string labelFr,
		bool isPrivate,
		bool isActive,
		int displayOrder,
		DateTimeOffset at,
		string? helpTextEn = null,
		string? helpTextFr = null,
		string? placeholderEn = null,
		string? placeholderFr = null,
		bool isRequired = false,
		TinyId? dependsOnQuestionId = null,
		TinyId? dependsOnChoiceId = null,
		TinyId? groupedUnderQuestionId = null,
		bool? isTranslatable = null,
		bool? allowFutureDates = null)
	{
		if (IsSystem && type != Type)
		{
			throw new DomainRuleViolationException(
				$"'{Key}' is a system question. Its wording can change; its type cannot.");
		}

		// Deactivate() refuses this, but an administrator's ordinary edit reaches
		// the same state by clearing the active flag, and that path had no guard.
		// A rule enforced on one route into a state is not enforced.
		if (IsSystem && !isActive)
		{
			throw new DomainRuleViolationException(
				$"'{Key}' is a system question and gates publication. The form must keep asking it.");
		}

		// A consent answer is not an occurrence fact. The Worker already leaves it
		// out of summary input by its key; keeping it private is the second guard,
		// so no path that classifies by privacy alone can ever treat it as one.
		if (IsSystem && !isPrivate)
		{
			throw new DomainRuleViolationException(
				$"'{Key}' is a system question. Its answer is always private.");
		}

		return ReviseInternal(
			new RevisionDraft(
				type, labelEn, labelFr, helpTextEn, helpTextFr, placeholderEn, placeholderFr,
				isRequired, isPrivate, isActive, displayOrder, dependsOnQuestionId, dependsOnChoiceId,
				groupedUnderQuestionId, TranslatableFor(type, isTranslatable),
				AllowFutureDatesFor(type, allowFutureDates)),
			at);
	}

	/// <summary>
	///     Applies an administrator's edit and returns the question that is live
	///     afterwards — this one, revised, or a new one that replaces it.
	/// </summary>
	/// <remarks>
	///     <para>
	///         This is where ADR-0071 lives. While nothing has answered the question, an
	///         edit is a revision and the question keeps its identity. Once an answer
	///         exists, a reworded question is a different question: this one is retired
	///         and a new one takes its place, carrying the same stable key, so every
	///         answer already given keeps pointing at the wording it was given under.
	///     </para>
	///     <para>
	///         Choices are not part of a revision (ADR-0095). When
	///         <paramref name="options" /> is given it is the complete new list, applied
	///         in place to whichever question is live afterwards — so an edit that only
	///         changes choices creates no revision and never forks, and a fork carries
	///         every choice across before the new list is applied. An edit that changes
	///         no revision field creates no revision at all.
	///     </para>
	///     <para>
	///         A system question never forks. It cannot be deleted, so it revises in
	///         place however many answers it has.
	///     </para>
	///     <para>
	///         Whether the question has been answered is a fact about reports, which
	///         this aggregate cannot see, so the caller reads it and passes it in. It
	///         must count answers on deleted reports too — a deleted report is still a
	///         record of what somebody was asked.
	///     </para>
	/// </remarks>
	public Question ApplyEdit(
		bool hasBeenAnswered,
		QuestionType type,
		string labelEn,
		string labelFr,
		bool isPrivate,
		bool isActive,
		int displayOrder,
		DateTimeOffset at,
		string? helpTextEn = null,
		string? helpTextFr = null,
		string? placeholderEn = null,
		string? placeholderFr = null,
		bool isRequired = false,
		TinyId? dependsOnQuestionId = null,
		TinyId? dependsOnChoiceId = null,
		TinyId? groupedUnderQuestionId = null,
		IReadOnlyList<QuestionOptionInput>? options = null,
		bool? isTranslatable = null,
		bool? allowFutureDates = null)
	{
		EnsureNotDeleted();

		var draft = new RevisionDraft(
			type, labelEn, labelFr, helpTextEn, helpTextFr, placeholderEn, placeholderFr,
			isRequired, isPrivate, isActive, displayOrder, dependsOnQuestionId,
			dependsOnChoiceId, groupedUnderQuestionId,
			TranslatableFor(type, isTranslatable),
			AllowFutureDatesFor(type, allowFutureDates));

		var live = this;

		if (draft != CurrentDraft())
		{
			if (ForksWhenEdited(hasBeenAnswered))
			{
				live = Fork(draft, at);
			}
			else
			{
				Revise(
					type, labelEn, labelFr, isPrivate, isActive, displayOrder, at,
					helpTextEn, helpTextFr, placeholderEn, placeholderFr, isRequired, dependsOnQuestionId,
					dependsOnChoiceId, groupedUnderQuestionId, draft.IsTranslatable, draft.AllowFutureDates);
			}
		}

		if (options is not null)
		{
			live.ReplaceChoices(options, at);
		}

		live.EnsureChoicesFitType();
		return live;
	}

	/// <summary>
	///     Whether an edit would replace this question rather than revise it. False
	///     for a question nobody has answered, and false for a system question
	///     however many answers it has.
	/// </summary>
	public bool ForksWhenEdited(bool hasBeenAnswered)
	{
		return hasBeenAnswered && !IsSystem;
	}

	/// <summary>
	///     Moves the question on the form, as a new revision. Every other
	///     field is carried forward unchanged from <see cref="CurrentRevision" />.
	/// </summary>
	public QuestionRevision Reorder(int displayOrder,
									DateTimeOffset at)
	{
		return ReviseInternal(CurrentDraft() with { DisplayOrder = displayOrder }, at);
	}

	/// <summary>
	///     Makes the question conditional on another question, or unconditional
	///     again, as a new revision. <paramref name="dependsOnChoiceId" /> names
	///     the parent's required choice when the parent is single-select, and must be null
	///     when it is yes/no or when there is no parent. Whether the named
	///     question is a type that can be a parent at all — and, for
	///     single-select, whether it currently offers the named option — is
	///     checked by <see cref="QuestionDependencies" />, which can see the rest
	///     of the bank. See ADR-0060, ADR-0074.
	/// </summary>
	public QuestionRevision DependOn(TinyId? dependsOnQuestionId,
									 TinyId? dependsOnChoiceId,
									 DateTimeOffset at)
	{
		return ReviseInternal(
			CurrentDraft() with { DependsOnQuestionId = dependsOnQuestionId, DependsOnChoiceId = dependsOnChoiceId },
			at);
	}

	/// <summary>
	///     Groups the question under a <see cref="QuestionType.Group" /> heading,
	///     or ungroups it, as a new revision. Whether the named question is
	///     currently a group — and does not lead back to this one — is checked
	///     by <see cref="QuestionGrouping" />, which can see the rest of the
	///     bank. Distinct from <see cref="DependOn" />: this never hides the
	///     question, it only says which heading it renders under. See ADR-0076.
	/// </summary>
	public QuestionRevision GroupUnder(TinyId? groupedUnderQuestionId,
									   DateTimeOffset at)
	{
		return ReviseInternal(CurrentDraft() with { GroupedUnderQuestionId = groupedUnderQuestionId }, at);
	}

	/// <summary>A live choice by its invariant code, or null when this question does not offer it.</summary>
	public QuestionChoice? Choice(string code)
	{
		var normalized = QuestionKey.Normalize(code);
		return _choices.Find(choice => choice.Code == normalized && choice.Deleted is null);
	}

	/// <summary>
	///     The live choice with this identifier, or null when this question does not
	///     offer it — a removed choice, or another question's. What a submitted
	///     choice answer is validated against (ADR-0128).
	/// </summary>
	public QuestionChoice? OfferedChoice(TinyId choiceId)
	{
		return _choices.Find(choice => choice.Id == choiceId && choice.Deleted is null);
	}

	/// <summary>
	///     The choice that stands for <paramref name="choiceId" /> today: that choice,
	///     or — when an Administrator replaced it — the one that replaced it,
	///     following replacements to the end. Null when this question never had that
	///     choice. The result is removed only when the last choice in the chain was
	///     removed rather than replaced (ADR-0128).
	/// </summary>
	public QuestionChoice? CurrentChoice(TinyId choiceId)
	{
		var choice = _choices.Find(candidate => candidate.Id == choiceId);
		var visited = new HashSet<TinyId>();

		while (choice?.ReplacedByChoiceId is { } next
			   && visited.Add(choice.Id)
			   && _choices.Find(candidate => candidate.Id == next) is { } replacement)
		{
			choice = replacement;
		}

		return choice;
	}

	/// <summary>
	///     The live choice labelled exactly this way in the reporter's language —
	///     which, for a one-language choice, may be the other language
	///     (<see cref="QuestionChoice.Label" />) — or null when none is.
	/// </summary>
	public QuestionChoice? OfferedChoiceLabelled(string label,
												 Locale locale)
	{
		return _choices.Find(choice => choice.Deleted is null
									   && string.Equals(choice.Label(locale), label, StringComparison.Ordinal));
	}

	/// <summary>
	///     Replaces this question's choices with the complete list an Administrator
	///     saved, in place — wording, pins, and all — with no revision and no fork,
	///     however many answers the question has (ADR-0095, ADR-0136).
	/// </summary>
	/// <remarks>
	///     <para>
	///         A choice missing from the list is removed: stamped, never erased. A
	///         choice already here is fixed in place — reworded and moved, same row,
	///         same code — unless the Administrator asked to replace it
	///         (<see cref="QuestionOptionInput.Replace" />): then it is retired, linked
	///         to a new choice with the new wording and a code derived from it, and
	///         every answer given under it keeps it (ADR-0128). A new code is added —
	///         or, if the question once had it and it was removed, that row is revived,
	///         because an Administrator writing it again means it.
	///     </para>
	///     <para>
	///         Whether a removed choice is one a live question depends on is a fact
	///         about other questions; <see cref="QuestionDependencies.EnsureChoicesRemovable" />
	///         checks it before this runs.
	///     </para>
	/// </remarks>
	public void ReplaceChoices(IReadOnlyList<QuestionOptionInput> options,
							   DateTimeOffset at)
	{
		ArgumentNullException.ThrowIfNull(options);
		EnsureNotDeleted();

		var codes = options.Select(option => QuestionKey.Normalize(option.Code)).ToList();

		if (codes.Distinct(StringComparer.Ordinal).Count() != codes.Count)
		{
			var repeated = codes.GroupBy(code => code, StringComparer.Ordinal).First(group => group.Count() > 1).Key;
			throw new DomainRuleViolationException($"This question already has a choice coded '{repeated}'.");
		}

		if (options.Count > 0)
		{
			EnsureTakesChoices(Type);
		}

		var replacements = ReplacementCodes(options, codes);

		foreach (var removed in Choices.Where(choice => !codes.Contains(choice.Code, StringComparer.Ordinal)))
		{
			removed.Delete(at);
		}

		for (var i = 0; i < options.Count; i++)
		{
			var option = options[i];

			if (replacements[i] is { } replacementCode)
			{
				var retired = _choices.Find(choice => choice.Code == codes[i])!;
				var replacement = QuestionChoice.Written(Id, replacementCode, i, option.LabelEn!, option.LabelFr!, option.Pin);
				_choices.Add(replacement);
				retired.ReplaceWith(replacement, at);
			}
			else if (_choices.Find(choice => choice.Code == codes[i]) is not { } existing)
			{
				_choices.Add(QuestionChoice.Written(Id, codes[i], i, option.LabelEn!, option.LabelFr!, option.Pin));
			}
			else if (existing.Deleted is not null)
			{
				existing.Restore(i, option.LabelEn, option.LabelFr, option.Pin);
			}
			else
			{
				existing.Relabel(option.LabelEn, option.LabelFr);
				existing.MoveTo(i);
				existing.PinTo(option.Pin);
			}
		}

		EnsureChoicesFitType();
	}

	/// <summary>
	///     For each option, the code of the new choice that replaces it, or null when
	///     it is not a replacement. Only a live picker option is replaced; its new
	///     code comes from its new English wording and must be one this question has
	///     never used, removed choices included, so a replacement is always a new row.
	/// </summary>
	private string?[] ReplacementCodes(IReadOnlyList<QuestionOptionInput> options,
									   List<string> codes)
	{
		var result = new string?[options.Count];

		for (var i = 0; i < options.Count; i++)
		{
			if (!options[i].Replace
				|| Choices.FirstOrDefault(choice => choice.Code == codes[i]) is null)
			{
				continue;
			}

			if (Type is not (QuestionType.SingleSelect or QuestionType.MultiSelect))
			{
				throw new DomainRuleViolationException(
					$"'{Key}' is a {EnumCode.Of(Type)} question. Only a picker option is replaced; a type-ahead value is corrected in place (ADR-0129).");
			}

			var code = QuestionKey.Normalize(options[i].LabelEn ?? string.Empty);

			if (_choices.Exists(choice => choice.Code == code)
				|| codes.Contains(code, StringComparer.Ordinal)
				|| result.Contains(code, StringComparer.Ordinal))
			{
				throw new DomainRuleViolationException(
					$"'{options[i].LabelEn}' reads like a choice this question already has, or once had. Replace it with different wording, or fix the option in place instead.");
			}

			result[i] = code;
		}

		return result;
	}

	/// <summary>
	///     The value a reporter's typed words name on this type-ahead, adding one when
	///     none does — the pilot who flew at a site nobody had written down. Runs at
	///     submission, in the report's transaction. See ADR-0129.
	/// </summary>
	/// <remarks>
	///     <para>Matched ignoring case, in either language, in this order:</para>
	///     <list type="number">
	///         <item>
	///             A live value reads that way — the reporter typed a site that exists, or
	///             a second pilot typed the same new one. It is returned unchanged, so a
	///             busy weekend at a new site produces one value, and a reviewer's wording
	///             is never replaced by a reporter's.
	///         </item>
	///         <item>
	///             A removed value reads that way, or has the code the words reduce to. It
	///             is returned <b>without</b> being revived — a reviewer removed it on
	///             purpose — and flagged for review again, so a reviewer sees it is still
	///             in use.
	///         </item>
	///         <item>
	///             Nothing does. A value is added holding only the language the reporter
	///             typed, marked <see cref="QuestionChoice.AddedByReporter" />, flagged for
	///             review, and offered from now on. Nothing on the submission path
	///             translates it.
	///         </item>
	///     </list>
	/// </remarks>
	public QuestionChoice AddChoiceFromReporter(string value,
												Locale locale,
												DateTimeOffset? at = null)
	{
		ArgumentNullException.ThrowIfNull(value);
		EnsureNotDeleted();

		if (!TakesReporterAdditions)
		{
			throw new DomainRuleViolationException(
				$"'{Key}' is a {EnumCode.Of(Type)} question. Only a type-ahead takes a choice a reporter adds.");
		}

		var typed = value.Trim();

		if (_choices.Find(choice => choice.Deleted is null && ReadsAs(choice, typed)) is { } worded)
		{
			return worded;
		}

		var code = QuestionKey.Normalize(typed);

		if ((_choices.Find(choice => choice.MergedIntoChoiceId is not null && ReadsAs(choice, typed))
			 ?? _choices.Find(choice => choice.Deleted is not null && ReadsAs(choice, typed))
			 ?? _choices.Find(choice => choice.Code == code)) is { } existing)
		{
			if (existing.MergedIntoChoiceId is not null)
			{
				return MergeTargetOf(existing);
			}

			if (existing.Deleted is not null)
			{
				existing.FlagForReview();
			}

			return existing;
		}

		var added = QuestionChoice.FromReporter(Id, code, NextChoiceOrder(), typed, locale, at);
		_choices.Add(added);
		return added;
	}

	/// <summary>
	///     A reviewer approves a type-ahead value as it stands, clearing its review
	///     flag (ADR-0129).
	/// </summary>
	public void ApproveValue(TinyId choiceId,
							 string reviewer,
							 DateTimeOffset at)
	{
		ReviewedValue(choiceId).MarkReviewed(reviewer, at);
	}

	/// <summary>
	///     A reviewer corrects a type-ahead value's wording in place: same value,
	///     so every answer that names it reads the correction (ADR-0129). A reporter-
	///     added value may keep one language missing; one an Administrator wrote keeps
	///     both. Wording another live value already has is refused — that is a
	///     duplicate to merge, not a correction.
	/// </summary>
	public void CorrectValue(TinyId choiceId,
							 string? labelEn,
							 string? labelFr,
							 string reviewer,
							 DateTimeOffset at)
	{
		var value = ReviewedValue(choiceId);

		foreach (var label in new[] { labelEn, labelFr }.Where(label => !string.IsNullOrWhiteSpace(label)))
		{
			if (_choices.Exists(other => other.Id != value.Id && other.Deleted is null && ReadsAs(other, label!.Trim())))
			{
				throw new DomainRuleViolationException(
					$"'{Key}' already offers a value reading '{label!.Trim()}'. Merge the two instead.");
			}
		}

		value.Relabel(labelEn?.Trim(), labelFr?.Trim());
		value.MarkReviewed(reviewer, at);
	}

	/// <summary>
	///     A reviewer removes a type-ahead value: it stops being offered, and every
	///     answer that names it still does and reads it (ADR-0128, ADR-0129).
	/// </summary>
	public void RemoveValue(TinyId choiceId,
							string reviewer,
							DateTimeOffset at)
	{
		var value = ReviewedValue(choiceId);

		if (value.Deleted is null)
		{
			value.Delete(at);
		}

		value.MarkReviewed(reviewer, at);
	}

	/// <summary>
	///     A reviewer merges one type-ahead value into another of the same question:
	///     the source is removed and every answer naming it reads the target, without
	///     any answer being rewritten. A value already merged into the source follows
	///     it to the target, so a merge never forms a chain or a cycle. Both are
	///     reviewed (ADR-0129).
	/// </summary>
	public void MergeValue(TinyId sourceId,
						   TinyId targetId,
						   string reviewer,
						   DateTimeOffset at)
	{
		var source = ReviewedValue(sourceId);
		var target = ReviewedValue(targetId);

		if (source.Id == target.Id)
		{
			throw new DomainRuleViolationException("A value cannot be merged into itself.");
		}

		if (source.MergedIntoChoiceId is not null)
		{
			throw new DomainRuleViolationException("That value was already merged into another.");
		}

		if (target.Deleted is not null)
		{
			throw new DomainRuleViolationException("A value can only be merged into one the form still offers.");
		}

		source.MergeInto(target, at);

		foreach (var earlier in _choices.Where(choice => choice.MergedIntoChoiceId == source.Id))
		{
			earlier.MergeInto(target, earlier.Deleted!.Value);
		}

		source.MarkReviewed(reviewer, at);
		target.MarkReviewed(reviewer, at);
	}

	private QuestionChoice MergeTargetOf(QuestionChoice merged)
	{
		return _choices.Single(choice => choice.Id == merged.MergedIntoChoiceId);
	}

	/// <summary>This type-ahead's value by identifier, removed ones included; any other question type refuses review.</summary>
	private QuestionChoice ReviewedValue(TinyId choiceId)
	{
		EnsureNotDeleted();

		if (!TakesReporterAdditions)
		{
			throw new DomainRuleViolationException(
				$"'{Key}' is a {EnumCode.Of(Type)} question. Only a type-ahead value is reviewed; an Administrator edits a picker's options.");
		}

		return _choices.Find(choice => choice.Id == choiceId)
			   ?? throw new DomainRuleViolationException($"'{Key}' has no such value.");
	}

	private static bool ReadsAs(QuestionChoice choice,
								string words)
	{
		return string.Equals(choice.LabelEn, words, StringComparison.OrdinalIgnoreCase)
			   || string.Equals(choice.LabelFr, words, StringComparison.OrdinalIgnoreCase);
	}

	/// <summary>
	///     Refuses a question whose choices do not fit its type: one that takes no
	///     choices while it still offers some — the state a retype would otherwise
	///     leave behind — and a single- or multi-select that offers none, which no
	///     reporter could answer. A type-ahead may offer none: reporters add to it
	///     (ADR-0063).
	/// </summary>
	private void EnsureChoicesFitType()
	{
		if (Choices.Count > 0)
		{
			EnsureTakesChoices(Type);
		}
		else if (Type is QuestionType.SingleSelect or QuestionType.MultiSelect)
		{
			throw new DomainRuleViolationException(
				$"A {EnumCode.Of(Type)} question needs at least one choice. A reporter could not answer it otherwise.");
		}
	}

	private static void EnsureTakesChoices(QuestionType type)
	{
		if (type == QuestionType.YesNo)
		{
			throw new DomainRuleViolationException(
				"A yes/no question has exactly two answers, yes and no. It cannot be given more, and it has no default.");
		}

		if (type is not (QuestionType.SingleSelect or QuestionType.MultiSelect or QuestionType.Autocomplete))
		{
			throw new DomainRuleViolationException($"A {type} question does not have options.");
		}
	}

	/// <summary>
	///     Choices in the order every reader receives them: pinned first, unpinned,
	///     pinned last, and by identifier within a group (ADR-0136).
	/// </summary>
	public static IEnumerable<QuestionChoice> InListOrder(IEnumerable<QuestionChoice> choices)
	{
		ArgumentNullException.ThrowIfNull(choices);

		return choices
			.OrderBy(choice => choice.Pin.Group())
			.ThenBy(choice => choice.Id.Value, StringComparer.Ordinal);
	}

	private int NextChoiceOrder()
	{
		return _choices.Count == 0 ? 0 : _choices.Max(choice => choice.DisplayOrder) + 1;
	}

	/// <summary>
	///     Reassigns what logic reads this answer for. A role lives on at
	///     most one active question at a time; that is enforced by the question bank,
	///     not here.
	/// </summary>
	public void AssignRole(QuestionRole role)
	{
		EnsureNotDeleted();

		if (IsSystem && role != Role)
		{
			throw new DomainRuleViolationException($"'{Key}' is a system question and cannot give up its role.");
		}

		Role = role;
	}

	/// <summary>
	///     Starts asking this question, as a new revision. Every revision is born
	///     complete in both official languages, so there is nothing left to check
	///     here beyond whether the question itself is still live.
	/// </summary>
	public QuestionRevision Activate(DateTimeOffset at)
	{
		return ReviseInternal(CurrentDraft() with { IsActive = true }, at);
	}

	/// <summary>
	///     Stops asking this question, as a new revision. Every answer
	///     already given to it is kept.
	/// </summary>
	public QuestionRevision Deactivate(DateTimeOffset at)
	{
		if (IsSystem)
		{
			throw new DomainRuleViolationException(
				$"'{Key}' is a system question and gates publication. The form must keep asking it.");
		}

		return ReviseInternal(CurrentDraft() with { IsActive = false }, at);
	}

	/// <summary>
	///     Retires the question. A soft delete, always: answers to it are part of a
	///     real report and are never removed with it.
	/// </summary>
	/// <remarks>
	///     There is no undelete, deliberately. A retired question may already have
	///     answers frozen against its retirement, and a row that can come back is
	///     not frozen (ADR-0071). An administrator who wants it again authors it
	///     again.
	/// </remarks>
	/// <remarks>
	///     <para>
	///         A question any answer references is history and is never removed:
	///         the answer records what somebody was asked, and a question that can
	///         vanish leaves it recording nothing. Deactivating it through a new
	///         revision is how it stops appearing on the form.
	///     </para>
	///     <para>
	///         Whether an answer references it is a fact about reports, which this
	///         aggregate cannot see, so the caller reads it and passes it in — the
	///         same arrangement <see cref="ApplyEdit" /> uses. It must count
	///         answers on deleted reports too: a deleted report is still a record
	///         of what somebody was asked.
	///     </para>
	/// </remarks>
	public void Delete(bool hasBeenAnswered,
					   DateTimeOffset at)
	{
		if (IsSystem)
		{
			throw new DomainRuleViolationException(
				$"'{Key}' is a system question and cannot be deleted. Publication depends on it.");
		}

		if (hasBeenAnswered)
		{
			throw new DomainRuleViolationException(
				$"'{Key}' has been answered and is part of those reports. Deactivate it instead: "
				+ "it stops appearing on the form and every answer keeps the wording it was given under.");
		}

		Retire(at);
	}

	/// <summary>
	///     Deletes one revision out of this question's history — distinct from
	///     <see cref="Delete" />, which retires the whole question. A revision
	///     leaving the form because it was superseded already happened when the
	///     next one was created; this is an administrator cleaning up a revision
	///     nobody answered, not a change to what the form asks.
	/// </summary>
	/// <remarks>
	///     Whether an answer references it — including one on a deleted report —
	///     is a fact about reports, which this aggregate cannot see, so the caller
	///     reads it and passes it in, the same arrangement <see cref="Delete" />
	///     and <see cref="ApplyEdit" /> use.
	/// </remarks>
	public void DeleteRevision(TinyId revisionId,
							   bool hasBeenAnswered,
							   DateTimeOffset at)
	{
		var revision = _revisions.Find(candidate => candidate.Id == revisionId)
					   ?? throw new DomainRuleViolationException("That revision does not belong to this question.");

		if (revision.Id == CurrentRevision.Id)
		{
			throw new DomainRuleViolationException(
				"This is the current revision. It is what the form asks today, not history to clean up.");
		}

		if (hasBeenAnswered)
		{
			throw new DomainRuleViolationException(
				"An answer references this revision. It stays exactly as that reporter was asked.");
		}

		revision.Delete(at);
	}

	/// <summary>
	///     Soft-deletes without the reference check. Retiring a question that has
	///     been answered is exactly what a fork does (ADR-0071), so the check
	///     belongs on the administrator's delete, not on every path to Deleted.
	/// </summary>
	private void Retire(DateTimeOffset at)
	{
		if (Deleted is not null)
		{
			return;
		}

		Deleted = at;
	}

	/// <summary>
	///     Retires this question and returns its replacement, carrying the same
	///     stable key and starting a fresh revision chain. The key is shared with
	///     every retired question in the chain and is unique only among live ones,
	///     which is what the partial unique index enforces (ADR-0071).
	/// </summary>
	private Question Fork(RevisionDraft draft,
						  DateTimeOffset at)
	{
		EnsureNotDeleted();

		var replacement = new Question(Key, false, Role, at);
		replacement._revisions.Add(
			QuestionRevision.Create(
				replacement.Id, 1, draft.Type, draft.LabelEn, draft.LabelFr,
				draft.HelpTextEn, draft.HelpTextFr, draft.PlaceholderEn, draft.PlaceholderFr,
				false, draft.IsRequired, draft.IsPrivate, draft.IsActive, draft.DisplayOrder,
				draft.DependsOnQuestionId, draft.DependsOnChoiceId, draft.GroupedUnderQuestionId,
				draft.IsTranslatable, draft.AllowFutureDates, at));

		// Every choice crosses, removed ones and reporter-added marks included,
		// so the replacement offers exactly what this one did (ADR-0095). The
		// copies are new rows (ADR-0128), so a replaced-by link is re-pointed at
		// the copy of the choice that replaced the original.
		var copies = _choices.ToDictionary(choice => choice.Id, choice => choice.CopyTo(replacement.Id));
		foreach (var (originalId, copy) in copies)
		{
			var original = _choices.Single(choice => choice.Id == originalId);
			copy.RelinkReplacement(original.ReplacedByChoiceId is { } next && copies.TryGetValue(next, out var nextCopy) ? nextCopy.Id : null);
		}

		// A merged value's copy is merged into the copy of its target: the copies
		// have new identifiers (ADR-0129).
		foreach (var merged in _choices.Where(choice => choice.MergedIntoChoiceId is not null))
		{
			copies[merged.Id].MergeInto(copies[merged.MergedIntoChoiceId!.Value], merged.Deleted!.Value);
		}

		replacement._choices.AddRange(copies.Values);

		Retire(at);
		return replacement;
	}

	private QuestionRevision ReviseInternal(RevisionDraft draft,
											DateTimeOffset at)
	{
		EnsureNotDeleted();

		var revision = QuestionRevision.Create(
			Id, CurrentRevision.RevisionNumber + 1, draft.Type, draft.LabelEn, draft.LabelFr,
			draft.HelpTextEn, draft.HelpTextFr, draft.PlaceholderEn, draft.PlaceholderFr,
			IsSystem, draft.IsRequired, draft.IsPrivate, draft.IsActive, draft.DisplayOrder,
			draft.DependsOnQuestionId, draft.DependsOnChoiceId, draft.GroupedUnderQuestionId,
			draft.IsTranslatable, draft.AllowFutureDates, at);
		_revisions.Add(revision);
		return revision;
	}

	/// <summary>
	///     The current revision, field for field, as input for the next one. A
	///     change that touches one field says so with a <c>with</c> expression, so
	///     adding a revision field cannot quietly drop it from the five methods
	///     that carry everything else forward.
	/// </summary>
	private RevisionDraft CurrentDraft()
	{
		var current = CurrentRevision;

		return new RevisionDraft(
			current.Type, current.LabelEn, current.LabelFr, current.HelpTextEn, current.HelpTextFr,
			current.PlaceholderEn, current.PlaceholderFr, current.IsRequired, current.IsPrivate, current.IsActive,
			current.DisplayOrder, current.DependsOnQuestionId, current.DependsOnChoiceId,
			current.GroupedUnderQuestionId, current.IsTranslatable, current.AllowFutureDates);
	}

	/// <summary>
	///     Whether the next revision needs translation. An explicit answer wins. With
	///     none, a question that keeps its type keeps its setting, and one that
	///     changes type — or is new — takes the new type's default (ADR-0112).
	/// </summary>
	private bool TranslatableFor(QuestionType type,
								 bool? requested)
	{
		if (requested is { } explicitly)
		{
			return explicitly;
		}

		return type == Type
			? CurrentRevision.IsTranslatable
			: QuestionRevision.TranslatableByDefault(type);
	}

	/// <summary>
	///     Whether the next revision allows a future date. An explicit answer wins.
	///     With none, a date question keeps its setting, and anything else — a new
	///     type included — does not allow one (ADR-0138).
	/// </summary>
	private bool AllowFutureDatesFor(QuestionType type,
									 bool? requested)
	{
		if (requested is { } explicitly)
		{
			return explicitly;
		}

		return type == Type && CurrentRevision.AllowFutureDates;
	}

	private void EnsureNotDeleted()
	{
		if (Deleted is not null)
		{
			throw new DomainRuleViolationException($"'{Key}' was deleted and cannot be changed.");
		}
	}

	/// <summary>
	///     Every field of a revision-to-be. Exists so that a change to one field is
	///     written as one <c>with</c> expression rather than as a positional
	///     argument list that a reader has to count.
	/// </summary>
	private sealed record RevisionDraft(
		QuestionType Type,
		string LabelEn,
		string LabelFr,
		string? HelpTextEn,
		string? HelpTextFr,
		string? PlaceholderEn,
		string? PlaceholderFr,
		bool IsRequired,
		bool IsPrivate,
		bool IsActive,
		int DisplayOrder,
		TinyId? DependsOnQuestionId,
		TinyId? DependsOnChoiceId,
		TinyId? GroupedUnderQuestionId,
		bool IsTranslatable,
		bool AllowFutureDates);
}
