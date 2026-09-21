import { useCallback, useEffect, useState } from "react"
import { useLocale } from "../i18n/useLocale"
import {
	ApiError,
	listAnswersAwaitingTranslation,
	supplyAnswerTranslation,
	translate,
	translationAvailable,
	type AwaitingTranslationView,
} from "../api/adminQuestions"

/*
 * The answers waiting for a second official language.
 *
 * A reporter picks a value from a picker or a type-ahead in whichever language
 * they are using, and nothing on the submission path translates it (ADR-0072).
 * The answer is stored exactly as they gave it and flagged, and this is where
 * an administrator clears that flag — by typing the other language, or by
 * pressing Translate and saving what comes back.
 *
 * The reporter's own value is never editable here. Their words are the record
 * of what they answered; what this screen adds is the other language beside it.
 */

export function ManageAnswerTranslationsPage() {
	const { t } = useLocale()
	const [answers, setAnswers] = useState<AwaitingTranslationView[]>([])
	const [drafts, setDrafts] = useState<Record<string, string>>({})
	const [canTranslate, setCanTranslate] = useState(false)
	const [error, setError] = useState<string | null>(null)
	const [loading, setLoading] = useState(true)

	const report = useCallback(
		(cause: unknown) =>
			setError(cause instanceof ApiError ? cause.detail : t("answerTranslations.error.unexpected")),
		[t],
	)

	const load = useCallback(async () => {
		try {
			setLoading(true)
			const queue = await listAnswersAwaitingTranslation()
			setAnswers(queue.answers)
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

	useEffect(() => {
		translationAvailable()
			.then((availability) => setCanTranslate(availability.available))
			.catch(() => setCanTranslate(false))
	}, [])

	async function draftTranslation(answer: AwaitingTranslationView) {
		try {
			const result = await translate([answer.value], answer.locale, answer.into)
			setDrafts((current) => ({ ...current, [answer.id]: result.texts[0] ?? "" }))
		} catch (cause) {
			report(cause)
		}
	}

	async function save(answer: AwaitingTranslationView) {
		try {
			await supplyAnswerTranslation(answer.id, drafts[answer.id] ?? "")
			setDrafts((current) => {
				const { [answer.id]: _removed, ...rest } = current
				return rest
			})
			await load()
		} catch (cause) {
			report(cause)
		}
	}

	return (
		<main className="mx-auto max-w-4xl px-6 py-12">
			<h1 className="font-display text-3xl font-bold">{t("answerTranslations.title")}</h1>
			<p className="mt-2 font-sans text-ink-muted">{t("answerTranslations.intro")}</p>

			{error && (
				<p role="alert" className="mt-6 rounded border border-brand-700 bg-surface-2 p-4 font-sans text-ink">
					{error}
				</p>
			)}

			{answers.length > 0 && (
				<p className="mt-6 rounded border border-rule bg-surface-2 p-4 font-sans text-sm text-ink">
					{t("answerTranslations.waitingCount", { count: String(answers.length) })}
				</p>
			)}

			{loading ? (
				<p className="mt-8 font-sans text-ink-muted">{t("answerTranslations.loading")}</p>
			) : answers.length === 0 ? (
				<p className="mt-8 font-sans text-ink-muted">{t("answerTranslations.empty")}</p>
			) : (
				<ul aria-label={t("answerTranslations.title")} className="mt-8 flex flex-col gap-4">
					{answers.map((answer) => (
						<li key={answer.id} className="rounded border border-rule bg-surface p-4">
							<p className="font-sans text-xs uppercase tracking-wide text-ink-muted">
								{answer.questionKey}
							</p>
							<p className="mt-1 font-sans font-medium text-ink">{answer.value}</p>
							<p className="font-sans text-sm text-ink-muted">
								{t("answerTranslations.givenIn", { locale: answer.locale })}
							</p>

							<label className="mt-4 block font-sans text-sm text-ink">
								{t("answerTranslations.supply", { locale: answer.into })}
								<input
									type="text"
									className="mt-1 w-full rounded border border-rule bg-surface-2 px-3 py-2 font-sans text-ink"
									value={drafts[answer.id] ?? ""}
									onChange={(event) =>
										setDrafts((current) => ({ ...current, [answer.id]: event.target.value }))
									}
								/>
							</label>

							<div className="mt-3 flex flex-wrap gap-3">
								<button
									type="button"
									disabled={!canTranslate}
									className="touch-target inline-flex items-center rounded border border-rule px-4 font-sans text-ink disabled:opacity-50"
									onClick={() => void draftTranslation(answer)}
								>
									{t("answerTranslations.translate")}
								</button>
								<button
									type="button"
									disabled={!(drafts[answer.id] ?? "").trim()}
									className="touch-target inline-flex items-center rounded bg-brand-700 px-5 font-sans font-medium text-ink-inverse hover:bg-brand-600 disabled:opacity-50"
									onClick={() => void save(answer)}
								>
									{t("answerTranslations.save")}
								</button>
							</div>
						</li>
					))}
				</ul>
			)}
		</main>
	)
}
