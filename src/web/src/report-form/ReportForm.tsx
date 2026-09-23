import { useEffect, useMemo, useRef, useState } from "react"

import { useLocale } from "../i18n/useLocale"
import { fetchCurrentQuestions, type PublicQuestionView } from "../api/publicQuestions"
import {
	SubmissionNetworkError,
	SubmissionRejectedError,
	submitReport,
	type SubmitAnswer,
} from "../api/reportSubmission"
import { clearDraft, readDraft, writeDraft, type DraftAnswer, type ReportDraft } from "./draft"
import { QuestionField } from "./QuestionField"
import { ResumeDraftDialog, savedAnswerRows, type SavedAnswerRow } from "./ResumeDraftDialog"
import {
	buildSteps,
	collectsNoAnswer,
	indexQuestionsById,
	questionLabel,
	unansweredRequired,
	visibleChildren,
	visibleSteps,
	type AnswerMap,
	type FormStep,
} from "./steps"

type LoadState =
	| { status: "loading" }
	| { status: "error" }
	| { status: "ready"; questions: PublicQuestionView[] }

type SubmitState = { status: "idle" } | { status: "submitting" } | { status: "submitted"; id: string } | { status: "failed"; message: string; keepsLocalState: boolean }

function stepQuestionId(step: FormStep): string {
	return step.question.id
}

export function ReportForm() {
	const { locale, t } = useLocale()
	const [load, setLoad] = useState<LoadState>({ status: "loading" })
	const [answers, setAnswers] = useState<AnswerMap>({})
	const [fileAnswers, setFileAnswers] = useState<Record<string, File[]>>({})
	const [currentStepId, setCurrentStepId] = useState<string | null>(null)
	// Purely cosmetic: true for one paint after the step changes, so the new
	// content fades in. Never gates navigation logic — a step change is always
	// applied to `currentStepId` immediately, so rapid Next/Back clicks (or an
	// impatient reporter) can never race ahead of an in-flight animation.
	const [entering, setEntering] = useState(false)
	const [attemptedAdvance, setAttemptedAdvance] = useState(false)
	const [submit, setSubmit] = useState<SubmitState>({ status: "idle" })
	const offeredDraft = useRef(false)
	const [pendingDraft, setPendingDraft] = useState<{ draft: ReportDraft; rows: SavedAnswerRow[] } | null>(null)

	useEffect(() => {
		let cancelled = false
		fetchCurrentQuestions()
			.then((questions) => {
				if (cancelled) return
				setLoad({ status: "ready", questions })
			})
			.catch(() => {
				if (!cancelled) setLoad({ status: "error" })
			})
		return () => {
			cancelled = true
		}
	}, [])

	// Offered once, from whatever the browser already held, as soon as the form
	// is known — never restored without the reporter saying so, and never
	// overwriting an in-progress edit on a later render.
	useEffect(() => {
		if (load.status !== "ready" || offeredDraft.current) return
		offeredDraft.current = true
		const draft = readDraft()
		if (!draft) return
		const rows = savedAnswerRows(load.questions, draft.answers, locale, t)
		if (rows.length === 0) {
			clearDraft() // Nothing on this form to continue.
			return
		}
		setPendingDraft({ draft, rows })
	}, [load, locale, t])

	const steps = useMemo(() => (load.status === "ready" ? buildSteps(load.questions) : []), [load])
	const questionsById = useMemo(() => (load.status === "ready" ? indexQuestionsById(load.questions) : new Map()), [load])
	const visible = useMemo(
		() => visibleSteps(steps, answers, questionsById, locale),
		[steps, answers, questionsById, locale],
	)

	useEffect(() => {
		if (visible.length === 0) return
		setCurrentStepId((current) => {
			if (current && visible.some((step) => stepQuestionId(step) === current)) return current
			return stepQuestionId(visible[0]!)
		})
	}, [visible])

	// Kept only while there is something worth keeping — an empty draft on a
	// browser that never opened the form would just be noise.
	useEffect(() => {
		if (submit.status === "submitted") return
		if (Object.keys(answers).length === 0) return
		const stepRevisionId = visible.find((step) => stepQuestionId(step) === currentStepId)?.question.revisionId
		writeDraft({ locale, answers, stepRevisionId })
	}, [answers, locale, submit.status, visible, currentStepId])

	const currentIndex = currentStepId ? visible.findIndex((step) => stepQuestionId(step) === currentStepId) : -1
	const currentStep = currentIndex >= 0 ? visible[currentIndex] : null
	const isFirst = currentIndex === 0
	const isLast = currentIndex >= 0 && currentIndex === visible.length - 1

	function continueDraft() {
		if (!pendingDraft) return
		const { draft, rows } = pendingDraft
		const restored: AnswerMap = {}
		for (const row of rows) restored[row.revisionId] = draft.answers[row.revisionId]!
		setAnswers(restored)
		const savedStep = steps.find((step) => step.question.revisionId === draft.stepRevisionId)
		if (savedStep) setCurrentStepId(stepQuestionId(savedStep))
		setPendingDraft(null)
	}

	function startOver() {
		clearDraft()
		setPendingDraft(null)
	}

	function setAnswer(revisionId: string, answer: DraftAnswer | undefined) {
		setAnswers((prev) => {
			const next = { ...prev }
			if (answer) next[revisionId] = answer
			else delete next[revisionId]
			return next
		})
	}

	function goTo(step: FormStep) {
		setAttemptedAdvance(false)
		setCurrentStepId(stepQuestionId(step))
	}

	// Fades the new step in on every change, including the very first render.
	// `requestAnimationFrame` rather than a bare state flip so the browser
	// actually paints the "opacity-0" frame before transitioning to
	// "opacity-100" — otherwise the two states would collapse into one paint
	// and there would be nothing to transition.
	useEffect(() => {
		if (!currentStepId) return
		setEntering(false)
		const frame = requestAnimationFrame(() => setEntering(true))
		return () => cancelAnimationFrame(frame)
	}, [currentStepId])

	function blockingRequirements(): PublicQuestionView[] {
		if (!currentStep) return []
		return unansweredRequired(currentStep, answers, questionsById, locale)
	}

	function handleNext() {
		const blocking = blockingRequirements()
		if (blocking.length > 0) {
			setAttemptedAdvance(true)
			return
		}
		const next = visible[currentIndex + 1]
		if (next) goTo(next)
	}

	function handleBack() {
		const previous = visible[currentIndex - 1]
		if (previous) goTo(previous)
	}

	async function handleSubmit() {
		const blocking = blockingRequirements()
		if (blocking.length > 0) {
			setAttemptedAdvance(true)
			return
		}
		if (submit.status === "submitting") return // Duplicate submission is prevented at the state level, not just a disabled button.

		setSubmit({ status: "submitting" })

		const submitAnswers: SubmitAnswer[] = []
		const files: File[] = []

		for (const step of visible) {
			const questions = step.kind === "group" ? visibleChildren(step.question, answers, questionsById, locale) : [step.question]

			for (const question of questions) {
				if (collectsNoAnswer(question)) continue

				const answer = answers[question.revisionId]

				if (question.type === "multi_select") {
					submitAnswers.push({
						questionRevisionId: question.revisionId,
						value: null,
						optionCodes: answer?.kind === "options" ? answer.values : [],
						attachmentPartIndexes: null,
					})
					continue
				}

				if (question.type === "file_upload") {
					const questionFiles = fileAnswers[question.revisionId] ?? []
					const indexes = questionFiles.map((_, offset) => files.length + offset)
					files.push(...questionFiles)
					submitAnswers.push({
						questionRevisionId: question.revisionId,
						value: null,
						optionCodes: null,
						attachmentPartIndexes: indexes,
					})
					continue
				}

				submitAnswers.push({
					questionRevisionId: question.revisionId,
					value: answer?.kind === "value" ? answer.value : null,
					optionCodes: null,
					attachmentPartIndexes: null,
				})
			}
		}

		try {
			const result = await submitReport(locale, submitAnswers, files)
			clearDraft()
			setSubmit({ status: "submitted", id: result.id })
		} catch (error) {
			if (error instanceof SubmissionNetworkError) {
				setSubmit({ status: "failed", message: t("report.error.network"), keepsLocalState: true })
			} else if (error instanceof SubmissionRejectedError) {
				setSubmit({ status: "failed", message: error.detail, keepsLocalState: true })
			} else {
				setSubmit({ status: "failed", message: t("report.error.submitFailed"), keepsLocalState: true })
			}
		}
	}

	if (load.status === "loading") {
		return <p aria-busy="true" className="font-sans text-ink-muted">{t("report.loading")}</p>
	}

	if (load.status === "error") {
		return <p role="alert" className="font-sans text-brand-700">{t("report.loadError")}</p>
	}

	if (submit.status === "submitted") {
		return (
			<div role="status" className="rounded border border-rule bg-surface-2 p-6">
				<h2 className="font-display text-xl font-bold text-ink">{t("report.submitted.title")}</h2>
				<p className="mt-2 font-sans text-ink-muted">{t("report.submitted.body")}</p>
			</div>
		)
	}

	if (!currentStep) {
		return <p className="font-sans text-ink-muted">{t("report.loading")}</p>
	}

	const blocking = attemptedAdvance ? blockingRequirements() : []
	const blockingIds = new Set(blocking.map((question) => question.revisionId))

	return (
		<div className="mx-auto max-w-measure px-6 py-10">
			{pendingDraft && (
				<ResumeDraftDialog rows={pendingDraft.rows} onContinue={continueDraft} onStartOver={startOver} t={t} />
			)}

			<p role="status" className="sr-only">
				{t("report.progress", { current: currentIndex + 1, total: visible.length })}
			</p>

			<div className="rounded border border-rule bg-surface p-4 sm:p-6">
				<p className="font-sans text-xs text-ink-muted">
					{t("report.privacy.localStorage")}
				</p>
			</div>

			{blocking.length > 0 && (
				<div role="alert" className="mt-4 rounded border border-brand-700 bg-surface p-4">
					<p className="font-sans text-sm font-semibold text-brand-700">{t("report.required.summary")}</p>
					<ul className="mt-1 list-disc pl-5 font-sans text-sm text-brand-700">
						{blocking.map((question) => (
							<li key={question.revisionId}>
								<a href={`#question-${question.revisionId}`}>{questionLabel(question, locale)}</a>
							</li>
						))}
					</ul>
				</div>
			)}

			{submit.status === "failed" && (
				<p role="alert" className="mt-4 rounded border border-brand-700 bg-surface p-4 font-sans text-sm text-brand-700">
					{submit.message}
				</p>
			)}

			<div className={`mt-6 transition-opacity duration-200 ${entering ? "opacity-100" : "opacity-0"}`}>
				<StepContent
					step={currentStep}
					locale={locale}
					answers={answers}
					fileAnswers={fileAnswers}
					onAnswer={setAnswer}
					onFiles={(revisionId, files) => setFileAnswers((prev) => ({ ...prev, [revisionId]: files }))}
					blockingIds={blockingIds}
					questionsById={questionsById}
					t={t}
				/>
			</div>

			<div className="mt-8 flex items-center justify-between gap-4">
				{!isFirst && currentStep.kind !== "intro" ? (
					<button
						type="button"
						className="touch-target rounded border border-rule px-4 font-sans text-sm text-ink hover:bg-surface-2"
						onClick={handleBack}
					>
						{t("report.nav.back")}
					</button>
				) : (
					<span />
				)}

				{isLast ? (
					<button
						type="button"
						className="touch-target rounded bg-brand-700 px-5 font-sans text-sm font-semibold text-ink-inverse disabled:opacity-60"
						disabled={submit.status === "submitting"}
						onClick={handleSubmit}
					>
						{submit.status === "submitting" ? t("report.nav.submitting") : t("report.nav.submit")}
					</button>
				) : (
					<button
						type="button"
						className="touch-target rounded bg-brand-700 px-5 font-sans text-sm font-semibold text-ink-inverse"
						onClick={handleNext}
					>
						{t("report.nav.next")}
					</button>
				)}
			</div>
		</div>
	)
}

interface StepContentProps {
	step: FormStep
	locale: ReturnType<typeof useLocale>["locale"]
	answers: AnswerMap
	fileAnswers: Record<string, File[]>
	onAnswer: (revisionId: string, answer: DraftAnswer | undefined) => void
	onFiles: (revisionId: string, files: File[]) => void
	blockingIds: Set<string>
	questionsById: Map<string, PublicQuestionView>
	t: (key: string, params?: Record<string, string | number>) => string
}

function StepContent({ step, locale, answers, fileAnswers, onAnswer, onFiles, blockingIds, questionsById, t }: StepContentProps) {
	if (step.kind === "intro") {
		return (
			<div>
				<h1 className="font-display text-2xl font-bold text-ink">{questionLabel(step.question, locale)}</h1>
				{step.question.helpTextEn && (
					<p className="mt-3 font-sans text-ink-muted">
						{locale === "fr-CA" ? step.question.helpTextFr : step.question.helpTextEn}
					</p>
				)}
			</div>
		)
	}

	if (step.kind === "group") {
		const children = visibleChildren(step.question, answers, questionsById, locale)
		return (
			<fieldset>
				<legend className="font-display text-lg font-semibold text-ink">{questionLabel(step.question, locale)}</legend>
				<div className="mt-4">
					{children.map((child) => (
						<QuestionField
							key={child.revisionId}
							question={child}
							locale={locale}
							answer={answers[child.revisionId]}
							onChange={(answer) => onAnswer(child.revisionId, answer)}
							files={fileAnswers[child.revisionId] ?? []}
							onFilesChange={(files) => onFiles(child.revisionId, files)}
							errorText={blockingIds.has(child.revisionId) ? t("report.required.error") : null}
							t={t}
						/>
					))}
				</div>
			</fieldset>
		)
	}

	if (collectsNoAnswer(step.question)) {
		return (
			<div>
				<h2 className="font-display text-xl font-bold text-ink">{questionLabel(step.question, locale)}</h2>
			</div>
		)
	}

	return (
		<QuestionField
			question={step.question}
			locale={locale}
			answer={answers[step.question.revisionId]}
			onChange={(answer) => onAnswer(step.question.revisionId, answer)}
			files={fileAnswers[step.question.revisionId] ?? []}
			onFilesChange={(files) => onFiles(step.question.revisionId, files)}
			errorText={blockingIds.has(step.question.revisionId) ? t("report.required.error") : null}
			t={t}
		/>
	)
}
