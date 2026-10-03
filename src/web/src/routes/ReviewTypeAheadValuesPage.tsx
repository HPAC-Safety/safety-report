import { useCallback, useEffect, useMemo, useState } from "react"
import { useLocale } from "../i18n/useLocale"
import { sortChoices } from "../lib/sortChoices"
import { useUnsavedChangesGuard } from "../hooks/useUnsavedChangesGuard"
import { DEFAULT_TRANSLATION_DIRECTION, translationLocales } from "../components/translationDirection"
import {
	ApiError,
	approveTypeAheadValue,
	correctTypeAheadValue,
	listTypeAheadValuesAwaitingReview,
	mergeTypeAheadValue,
	setTypeAheadValueParents,
	removeTypeAheadValue,
	translate,
	translationAvailable,
	type TypeAheadValueView,
} from "../api/adminQuestions"
import { ReviewTypeAheadValuesPageView } from "./ReviewTypeAheadValuesPage.view"
import { canTranslateDraft, groupByQuestion, wordingIn, type Draft } from "./typeAheadReview"

/** A keyed read, typed for what it is: the key may have no entry. */
function lookup<T>(map: Record<string, T>, id: string): T | undefined {
	return map[id]
}

/*
 * The type-ahead values waiting for a Safety Officer or Administrator
 * (ADR-0129). A reporter's new value is offered to the next reporter at once;
 * this is where someone who reads the reports looks at it afterwards.
 *
 * Each value can be approved as it stands, corrected in place — the same
 * value, so every answer that names it reads the correction — merged into
 * another value of its question, which every answer naming it then reads, or
 * removed: no longer offered, while every answer that named it still does. A removed
 * value a reporter typed again comes back here, still removed.
 *
 * The queue is grouped by question, questions and values each alphabetical in
 * the reader's language. An action refetches the queue without showing the
 * loading state, so the list stays mounted and the reviewer keeps their place.
 */

// A call signature rather than an arrow type: tools/web/check-hardcoded-strings.mjs
// is a line scanner and reads an arrow's `>` as the end of a tag.
type ReviewAction = { (): Promise<void> }

/** The view model: the queue, the corrections in progress, and what each action on a value does. */
export function useReviewTypeAheadValuesPage() {
	const { t, locale } = useLocale()
	const [values, setValues] = useState<TypeAheadValueView[]>([])
	const [drafts, setDrafts] = useState<Record<string, Draft>>({})
	const [mergeInto, setMergeInto] = useState<Record<string, string>>({})
	const [relinkTo, setRelinkTo] = useState<Record<string, string[]>>({})
	const [error, setError] = useState<string | null>(null)
	const [loading, setLoading] = useState(true)
	const [canTranslate, setCanTranslate] = useState(false)

	const dirty = Object.values(drafts).some((draft) => draft.labelEn !== draft.baselineEn || draft.labelFr !== draft.baselineFr)
	useUnsavedChangesGuard(dirty)

	const report = useCallback(
		(cause: unknown) => setError(cause instanceof ApiError ? cause.detail : t("typeAheadValues.error.unexpected")),
		[t],
	)

	// Refetches without touching `loading`, so the list stays mounted and the
	// reviewer's scroll position holds.
	const refresh = useCallback(async () => {
		const queue = await listTypeAheadValuesAwaitingReview()
		setValues(queue.values)
		setError(null)
	}, [])

	const load = useCallback(async () => {
		try {
			setLoading(true)
			const [, translation] = await Promise.all([
				refresh(),
				// Asked once, so Translate is disabled rather than offered and
				// then failing on a server with no credential.
				translationAvailable().catch(() => ({ available: false })),
			])
			setCanTranslate(translation.available)
		} catch (cause) {
			report(cause)
		} finally {
			setLoading(false)
		}
	}, [refresh, report])

	useEffect(() => {
		void load()
	}, [load])

	async function act(action: ReviewAction, id: string) {
		try {
			await action()
			setDrafts((current) => {
				const { [id]: _done, ...rest } = current
				return rest
			})
			await refresh()
		} catch (cause) {
			report(cause)
		}
	}

	function startCorrecting(value: TypeAheadValueView) {
		const labelEn = value.labelEn ?? ""
		const labelFr = value.labelFr ?? ""
		setDrafts((current) => ({
			...current,
			[value.id]: {
				labelEn,
				labelFr,
				baselineEn: labelEn,
				baselineFr: labelFr,
				direction: DEFAULT_TRANSLATION_DIRECTION,
				translating: false,
				translationError: null,
			},
		}))
	}

	/**
	 * Translates one value's draft in its own chosen direction and replaces
	 * its other language with the result, as a draft the reviewer still has
	 * to save. Dropped if the draft was cancelled, its source edited, or its
	 * direction flipped while the request was out — the same guard
	 * `QuestionEditor`'s `translateChoice` uses.
	 */
	async function translateValue(id: string) {
		const draft = drafts[id] as Draft | undefined
		if (!draft) return
		const asked = draft.direction
		const source = asked === "toFrench" ? draft.labelEn : draft.labelFr
		const { from, to } = translationLocales(asked)

		setDrafts((current) =>
			lookup(current, id) ? { ...current, [id]: { ...current[id], translating: true, translationError: null } } : current,
		)

		try {
			const { texts } = await translate([source], from, to)
			const drafted = texts[0] ?? ""
			setDrafts((current) => {
				const now = current[id] as Draft | undefined
				if (!now) return current
				const stillSource = (asked === "toFrench" ? now.labelEn : now.labelFr) === source
				// Stale: the direction flipped or the source was edited while the
				// request was out. Drop the result but stop showing it as working.
				if (now.direction !== asked || !stillSource) return { ...current, [id]: { ...now, translating: false } }
				const changes =
					asked === "toFrench"
						? { labelFr: drafted, baselineEn: source, baselineFr: drafted }
						: { labelEn: drafted, baselineEn: drafted, baselineFr: source }
				return { ...current, [id]: { ...now, ...changes, translating: false } }
			})
		} catch (cause) {
			setDrafts((current) =>
				lookup(current, id)
					? {
							...current,
							[id]: {
								...current[id],
								translating: false,
								translationError: cause instanceof ApiError ? cause.detail : t("typeAheadValues.translate.failed"),
							},
						}
					: current,
			)
		}
	}

	function wording(value: { labelEn: string | null; labelFr: string | null }) {
		return wordingIn(locale, value)
	}

	const groups = useMemo(() => groupByQuestion(values, locale, (value) => wordingIn(locale, value)), [values, locale])

	/** Everything the markup needs for one value, so the view only lays it out. */
	function cardOf(value: TypeAheadValueView) {
		const draft = drafts[value.id] as Draft | undefined
		const aliases = value.aliases ?? []
		const parent = value.parent
		return {
			value,
			draft,
			text: wording(value),
			aliasesText: aliases.map(wording).join(", "),
			hasAliases: aliases.length > 0,
			// Only live parent choices are shown and counted; a link to one since
			// removed is kept by the server, and the page never sends it.
			parentChosen: parent
				? ((relinkTo[value.id] as string[] | undefined) ?? parent.parentChoiceIds.filter((id) => parent.choices.some((choice) => choice.id === id)))
				: [],
			mergeSelected: (mergeInto[value.id] as string | undefined) ?? "",
			mergeOptions: sortChoices(value.mergeTargets, locale, wording).map((target) => ({ id: target.id, text: wording(target) })),
			translateDisabled: !draft || !canTranslate || !canTranslateDraft(draft) || draft.translating,
			onChooseParent: (choiceIds: string[]) => setRelinkTo((current) => ({ ...current, [value.id]: choiceIds })),
			onRelink: (choiceIds: string[]) => void act(() => setTypeAheadValueParents(value.id, choiceIds), value.id),
			// eslint-disable-next-line @typescript-eslint/no-non-null-assertion -- the field is drawn only for a value that has a draft
			onLabelEn: (labelEn: string) => setDrafts((current) => ({ ...current, [value.id]: { ...draft!, labelEn } })),
			// eslint-disable-next-line @typescript-eslint/no-non-null-assertion -- the field is drawn only for a value that has a draft
			onLabelFr: (labelFr: string) => setDrafts((current) => ({ ...current, [value.id]: { ...draft!, labelFr } })),
			onDirection: (direction: Draft["direction"]) =>
				setDrafts((current) => (lookup(current, value.id) ? { ...current, [value.id]: { ...current[value.id], direction } } : current)),
			onTranslate: () => void translateValue(value.id),
			// eslint-disable-next-line @typescript-eslint/no-non-null-assertion -- Save is drawn only for a value that has a draft
			onSaveCorrection: () => void act(() => correctTypeAheadValue(value.id, draft!.labelEn, draft!.labelFr), value.id),
			onCancel: () =>
				setDrafts((current) => {
					const { [value.id]: _cancelled, ...rest } = current
					return rest
				}),
			onApprove: () => void act(() => approveTypeAheadValue(value.id), value.id),
			onCorrect: () => startCorrecting(value),
			onRemove: () => void act(() => removeTypeAheadValue(value.id), value.id),
			onMergeInto: (intoId: string) => setMergeInto((current) => ({ ...current, [value.id]: intoId })),
			onMerge: () => void act(() => mergeTypeAheadValue(value.id, mergeInto[value.id]), value.id),
		}
	}

	return {
		error,
		loading,
		isEmpty: values.length === 0,
		canTranslate,
		wording,
		groups: groups.map((group) => ({ id: group.id, heading: group.heading, cards: group.values.map(cardOf) })),
	}
}

export type ReviewTypeAheadValuesPageModel = ReturnType<typeof useReviewTypeAheadValuesPage>

export function ReviewTypeAheadValuesPage() {
	return <ReviewTypeAheadValuesPageView {...useReviewTypeAheadValuesPage()} />
}
