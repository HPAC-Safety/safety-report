import { useCallback, useEffect, useMemo, useState } from "react"
import { useLocale } from "../i18n/useLocale"
import { sortChoices } from "../lib/sortChoices"
import { MultiSelectPicker } from "../report-form/MultiSelectPicker"
import {
	DEFAULT_TRANSLATION_DIRECTION,
	TranslationDirectionSwitch,
	translationLocales,
	type TranslationDirection,
} from "../components/TranslationDirectionSwitch"
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

/**
 * A correction in progress. `baselineEn`/`baselineFr` are the wording as the
 * correction view opened, or as Translate last drafted from — the same
 * "written and differs from the source" rule a question's choice uses
 * (ADR-0141, ADR-0144). Editing the source again after an earlier translate,
 * or after the target already had text, re-enables Translate.
 */
type Draft = {
	labelEn: string
	labelFr: string
	baselineEn: string
	baselineFr: string
	direction: TranslationDirection
	translating: boolean
	translationError: string | null
}

// A call signature rather than an arrow type: tools/check-hardcoded-strings.mjs
// is a line scanner and reads an arrow's `>` as the end of a tag.
type ReviewAction = { (): Promise<void> }

/**
 * Whether Translate is offered for a draft: its source language has text, and
 * either its target is empty or its source differs from the baseline — the
 * wording when correction opened, or when it was last translated
 * (ADR-0141/ADR-0144, same rule as `QuestionEditor`'s `canTranslateChoice`).
 */
function canTranslateDraft(draft: Draft): boolean {
	const source = draft.direction === "toFrench" ? draft.labelEn : draft.labelFr
	const target = draft.direction === "toFrench" ? draft.labelFr : draft.labelEn
	const baseline = draft.direction === "toFrench" ? draft.baselineEn : draft.baselineFr
	return source.trim().length > 0 && (target.trim().length === 0 || source !== baseline)
}

type QuestionGroup = { id: string; heading: string; values: TypeAheadValueView[] }

/** The queue grouped by question: headings and the values under each in `locale`'s alphabetical order (ADR-0136). */
function groupByQuestion(
	values: readonly TypeAheadValueView[],
	locale: string,
	wording: (value: { labelEn: string | null; labelFr: string | null }) => string,
): QuestionGroup[] {
	const byQuestion = new Map<string, TypeAheadValueView[]>()
	for (const value of values) byQuestion.set(value.questionId, [...(byQuestion.get(value.questionId) ?? []), value])
	const groups = [...byQuestion].map(([id, questionValues]) => ({
		id,
		heading: locale === "fr-CA" ? questionValues[0].questionLabelFr : questionValues[0].questionLabelEn,
		values: sortChoices(questionValues, locale, wording),
	}))
	return sortChoices(groups, locale, (group) => group.heading)
}

export function ReviewTypeAheadValuesPage() {
	const { t, locale } = useLocale()
	const [values, setValues] = useState<TypeAheadValueView[]>([])
	const [drafts, setDrafts] = useState<Record<string, Draft>>({})
	const [mergeInto, setMergeInto] = useState<Record<string, string>>({})
	const [relinkTo, setRelinkTo] = useState<Record<string, string[]>>({})
	const [error, setError] = useState<string | null>(null)
	const [loading, setLoading] = useState(true)
	const [canTranslate, setCanTranslate] = useState(false)

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
		const draft = drafts[id]
		if (!draft) return
		const asked = draft.direction
		const source = asked === "toFrench" ? draft.labelEn : draft.labelFr
		const { from, to } = translationLocales(asked)

		setDrafts((current) =>
			current[id] ? { ...current, [id]: { ...current[id], translating: true, translationError: null } } : current,
		)

		try {
			const { texts } = await translate([source], from, to)
			const drafted = texts[0] ?? ""
			setDrafts((current) => {
				const now = current[id]
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
				current[id]
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
		return (locale === "fr-CA" ? (value.labelFr ?? value.labelEn) : (value.labelEn ?? value.labelFr)) ?? ""
	}

	// `wording` changes only with `locale`.
	const groups = useMemo(() => groupByQuestion(values, locale, wording), [values, locale])

	return (
		<main className="mx-auto max-w-4xl px-6 py-12">
			<h1 className="font-display text-3xl font-bold">{t("typeAheadValues.title")}</h1>
			<p className="mt-2 font-sans text-ink-muted">{t("typeAheadValues.intro")}</p>

			{error && (
				<p role="alert" className="mt-6 rounded border border-brand-700 bg-surface-2 p-4 font-sans text-ink">
					{error}
				</p>
			)}

			{loading ? (
				<p className="mt-8 font-sans text-ink-muted">{t("typeAheadValues.loading")}</p>
			) : values.length === 0 ? (
				<p className="mt-8 font-sans text-ink-muted">{t("typeAheadValues.empty")}</p>
			) : (
				<div className="mt-8 flex flex-col gap-8">
					{groups.map((group) => (
						<section key={group.id} aria-labelledby={`type-ahead-group-${group.id}`}>
							<h2 id={`type-ahead-group-${group.id}`} className="font-display text-xl font-semibold text-ink">
								{group.heading}
							</h2>
							<ul className="mt-4 flex flex-col gap-4">
								{group.values.map((value) => {
									const draft = drafts[value.id]
									return (
										<li key={value.id} data-testid="type-ahead-value" className="rounded border border-rule bg-surface p-4">
											<p data-testid="type-ahead-value-wording" className="font-sans text-lg font-medium text-ink">
												{wording(value)}
											</p>
											<p className="font-sans text-sm text-ink-muted">
												{value.typedIn
													? t("typeAheadValues.typedIn", { locale: value.typedIn })
													: t("typeAheadValues.written")}
												{" · "}
												{t("typeAheadValues.answerCount", { count: String(value.answerCount) })}
												{value.isRemoved && ` · ${t("typeAheadValues.removedBadge")}`}
											</p>

											{value.parent && (
												<ParentLink
													valueId={value.id}
													parent={value.parent}
													// Only live parent choices are shown and counted; a link to one since
													// removed is kept by the server, and the page never sends it.
													chosen={
														relinkTo[value.id] ??
														value.parent.parentChoiceIds.filter((id) => value.parent!.choices.some((choice) => choice.id === id))
													}
													onChoose={(choiceIds) => setRelinkTo((current) => ({ ...current, [value.id]: choiceIds }))}
													onRelink={(choiceIds) => void act(() => setTypeAheadValueParents(value.id, choiceIds), value.id)}
													wording={wording}
												/>
											)}

											{draft ? (
												<div className="mt-4 grid gap-3 sm:grid-cols-2">
													<label className="block font-sans text-sm text-ink">
														{t("typeAheadValues.labelEn")}
														<input
															type="text"
															className="mt-1 w-full rounded border border-rule bg-surface-2 px-3 py-2 font-sans text-ink"
															value={draft.labelEn}
															onChange={(event) =>
																setDrafts((current) => ({ ...current, [value.id]: { ...draft, labelEn: event.target.value } }))
															}
														/>
													</label>
													<label className="block font-sans text-sm text-ink">
														{t("typeAheadValues.labelFr")}
														<input
															type="text"
															className="mt-1 w-full rounded border border-rule bg-surface-2 px-3 py-2 font-sans text-ink"
															value={draft.labelFr}
															onChange={(event) =>
																setDrafts((current) => ({ ...current, [value.id]: { ...draft, labelFr: event.target.value } }))
															}
														/>
													</label>
												</div>
											) : null}

											{draft && (
												<div
													role="group"
													aria-label={t("typeAheadValues.translate.group")}
													className="mt-3 flex flex-wrap items-center gap-3"
												>
													<TranslationDirectionSwitch
														direction={draft.direction}
														onChange={(direction) =>
															setDrafts((current) =>
																current[value.id]
																	? { ...current, [value.id]: { ...current[value.id], direction } }
																	: current,
															)
														}
													/>
													<button
														type="button"
														className="touch-target inline-flex items-center rounded border border-rule px-4 font-sans text-sm text-ink hover:bg-surface-2 disabled:opacity-40"
														disabled={!canTranslate || !canTranslateDraft(draft) || draft.translating}
														onClick={() => void translateValue(value.id)}
													>
														{draft.translating ? t("typeAheadValues.translate.working") : t("typeAheadValues.translate.action")}
													</button>
													<p className="font-sans text-xs text-ink-muted">
														{!canTranslate ? t("typeAheadValues.translate.unavailable") : t("typeAheadValues.translate.hint")}
													</p>
												</div>
											)}

											{draft?.translationError && (
												<p role="alert" className="mt-2 font-sans text-sm text-ink">
													{draft.translationError}
												</p>
											)}

											<div className="mt-3 flex flex-wrap gap-3">
												{draft ? (
													<>
														<button
															type="button"
															disabled={!draft.labelEn.trim() && !draft.labelFr.trim()}
															className="touch-target inline-flex items-center rounded bg-brand-700 px-5 font-sans font-medium text-ink-inverse hover:bg-brand-600 disabled:opacity-50"
															onClick={() => void act(() => correctTypeAheadValue(value.id, draft.labelEn, draft.labelFr), value.id)}
														>
															{t("typeAheadValues.saveCorrection")}
														</button>
														<button
															type="button"
															className="touch-target inline-flex items-center rounded border border-rule px-4 font-sans text-ink"
															onClick={() =>
																setDrafts((current) => {
																	const { [value.id]: _cancelled, ...rest } = current
																	return rest
																})
															}
														>
															{t("typeAheadValues.cancel")}
														</button>
													</>
												) : (
													<>
														<button
															type="button"
															className="touch-target inline-flex items-center rounded bg-brand-700 px-5 font-sans font-medium text-ink-inverse hover:bg-brand-600"
															onClick={() => void act(() => approveTypeAheadValue(value.id), value.id)}
														>
															{t("typeAheadValues.approve")}
														</button>
														<button
															type="button"
															className="touch-target inline-flex items-center rounded border border-rule px-4 font-sans text-ink"
															onClick={() => startCorrecting(value)}
														>
															{t("typeAheadValues.correct")}
														</button>
														{!value.isRemoved && (
															<button
																type="button"
																className="touch-target inline-flex items-center rounded border border-rule px-4 font-sans text-ink"
																onClick={() => void act(() => removeTypeAheadValue(value.id), value.id)}
															>
																{t("typeAheadValues.remove")}
															</button>
														)}
														{value.mergeTargets.length > 0 && (
															<span className="flex flex-wrap items-center gap-2">
																<label className="font-sans text-sm text-ink">
																	<span className="sr-only">{t("typeAheadValues.mergeInto")}</span>
																	<select
																		aria-label={t("typeAheadValues.mergeInto")}
																		className="touch-target rounded border border-rule bg-surface-2 px-3 font-sans text-ink"
																		value={mergeInto[value.id] ?? ""}
																		onChange={(event) =>
																			setMergeInto((current) => ({ ...current, [value.id]: event.target.value }))
																		}
																	>
																		<option value="">{t("typeAheadValues.mergeInto")}</option>
																		{sortChoices(value.mergeTargets, locale, wording).map((target) => (
																			<option key={target.id} value={target.id}>
																				{wording(target)}
																			</option>
																		))}
																	</select>
																</label>
																<button
																	type="button"
																	disabled={!mergeInto[value.id]}
																	className="touch-target inline-flex items-center rounded border border-rule px-4 font-sans text-ink disabled:opacity-50"
																	onClick={() => void act(() => mergeTypeAheadValue(value.id, mergeInto[value.id]!), value.id)}
																>
																	{t("typeAheadValues.merge")}
																</button>
															</span>
														)}
													</>
												)}
											</div>
										</li>
									)
								})}
							</ul>
						</section>
					))}
				</div>
			)}
		</main>
	)
}

/**
 * The parent choices a dependent type-ahead's value is offered under, and a
 * multi-select to add or remove them. A value is offered under at least one, so
 * the last one ticked cannot be unticked, unless the value has none yet
 * (ADR-0151).
 */
function ParentLink({
	valueId,
	parent,
	chosen,
	onChoose,
	onRelink,
	wording,
}: {
	valueId: string
	parent: NonNullable<TypeAheadValueView["parent"]>
	chosen: string[]
	onChoose: (choiceIds: string[]) => void
	onRelink: (choiceIds: string[]) => void
	wording: (choice: { labelEn: string | null; labelFr: string | null }) => string
}) {
	const { t, locale } = useLocale()
	const question = locale === "fr-CA" ? parent.questionLabelFr : parent.questionLabelEn
	const sorted = sortChoices(parent.choices, locale, wording)
	const current = sorted.filter((choice) => parent.parentChoiceIds.includes(choice.id))
	const unchanged = chosen.length === current.length && current.every((choice) => chosen.includes(choice.id))

	return (
		<div className="mt-3 flex flex-wrap items-end gap-2">
			<p data-testid="type-ahead-value-parent" className="w-full font-sans text-sm text-ink">
				{current.length > 0
					? t("typeAheadValues.offeredUnder", { question, choice: current.map(wording).join(", ") })
					: t("typeAheadValues.offeredUnderNothing", { question })}
			</p>
			<div className="min-w-64" data-testid="type-ahead-value-parent-choice">
				<MultiSelectPicker
					fieldId={`type-ahead-value-parent-${valueId}`}
					label={t("typeAheadValues.relinkTo", { question })}
					groups={[sorted.map((choice) => ({ key: choice.id, label: wording(choice) }))]}
					values={chosen}
					placeholder={t("typeAheadValues.relinkNone")}
					describedBy={undefined}
					locked={chosen.length === 1 ? chosen : []}
					lockedReason={t("typeAheadValues.lastParent")}
					onToggle={(id) => onChoose(chosen.includes(id) ? chosen.filter((ticked) => ticked !== id) : [...chosen, id])}
				/>
			</div>
			<button
				type="button"
				disabled={chosen.length === 0 || unchanged}
				className="touch-target inline-flex items-center rounded border border-rule px-4 font-sans text-ink disabled:opacity-50"
				onClick={() => onRelink(chosen)}
			>
				{t("typeAheadValues.relink")}
			</button>
		</div>
	)
}
