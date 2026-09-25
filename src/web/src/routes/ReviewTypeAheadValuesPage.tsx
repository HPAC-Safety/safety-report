import { useCallback, useEffect, useState } from "react"
import { useLocale } from "../i18n/useLocale"
import {
	ApiError,
	approveTypeAheadValue,
	correctTypeAheadValue,
	listTypeAheadValuesAwaitingReview,
	removeTypeAheadValue,
	type TypeAheadValueView,
} from "../api/adminQuestions"

/*
 * The type-ahead values waiting for a Safety Officer or Administrator
 * (ADR-0129). A reporter's new value is offered to the next reporter at once;
 * this is where someone who reads the reports looks at it afterwards.
 *
 * Each value can be approved as it stands, corrected in place — the same
 * value, so every answer that names it reads the correction — or removed:
 * no longer offered, while every answer that named it still does. A removed
 * value a reporter typed again comes back here, still removed.
 */

type Draft = { labelEn: string; labelFr: string }

// A call signature rather than an arrow type: tools/check-hardcoded-strings.mjs
// is a line scanner and reads an arrow's `>` as the end of a tag.
type ReviewAction = { (): Promise<void> }

export function ReviewTypeAheadValuesPage() {
	const { t, locale } = useLocale()
	const [values, setValues] = useState<TypeAheadValueView[]>([])
	const [drafts, setDrafts] = useState<Record<string, Draft>>({})
	const [error, setError] = useState<string | null>(null)
	const [loading, setLoading] = useState(true)

	const report = useCallback(
		(cause: unknown) => setError(cause instanceof ApiError ? cause.detail : t("typeAheadValues.error.unexpected")),
		[t],
	)

	const load = useCallback(async () => {
		try {
			setLoading(true)
			const queue = await listTypeAheadValuesAwaitingReview()
			setValues(queue.values)
			setError(null)
		} catch (cause) {
			report(cause)
		} finally {
			setLoading(false)
		}
	}, [report])

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
			await load()
		} catch (cause) {
			report(cause)
		}
	}

	function startCorrecting(value: TypeAheadValueView) {
		setDrafts((current) => ({ ...current, [value.id]: { labelEn: value.labelEn ?? "", labelFr: value.labelFr ?? "" } }))
	}

	function wording(value: TypeAheadValueView) {
		return (locale === "fr-CA" ? (value.labelFr ?? value.labelEn) : (value.labelEn ?? value.labelFr)) ?? ""
	}

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
				<ul aria-label={t("typeAheadValues.title")} className="mt-8 flex flex-col gap-4">
					{values.map((value) => {
						const draft = drafts[value.id]
						return (
							<li key={value.id} data-testid="type-ahead-value" className="rounded border border-rule bg-surface p-4">
								<p className="font-sans text-xs uppercase tracking-wide text-ink-muted">
									{locale === "fr-CA" ? value.questionLabelFr : value.questionLabelEn}
								</p>
								<p className="mt-1 font-sans text-lg font-medium text-ink">{wording(value)}</p>
								<p className="font-sans text-sm text-ink-muted">
									{value.typedIn
										? t("typeAheadValues.typedIn", { locale: value.typedIn })
										: t("typeAheadValues.written")}
									{" · "}
									{t("typeAheadValues.answerCount", { count: String(value.answerCount) })}
									{value.isRemoved && ` · ${t("typeAheadValues.removedBadge")}`}
								</p>

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
										</>
									)}
								</div>
							</li>
						)
					})}
				</ul>
			)}
		</main>
	)
}
