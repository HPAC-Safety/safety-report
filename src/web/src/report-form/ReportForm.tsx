import { useCallback, useEffect, useMemo, useRef, useState } from "react"
import { useNavigate, useParams } from "react-router-dom"

import { useLocale } from "../i18n/useLocale"
import { fetchCurrentQuestions, type PublicQuestionView } from "../api/publicQuestions"
import {
	SubmissionNetworkError,
	SubmissionRejectedError,
	submitReport,
	type SubmitAnswer,
} from "../api/reportSubmission"
import { MAX_ATTACHMENTS, deleteUpload } from "../api/uploads"
import type { Attachment } from "./AttachmentField"
import { DiscardReportDialog } from "./DiscardReportDialog"
import {
	clearDraft,
	draftUploadIds,
	readDraft,
	writeDraft,
	type DraftAnswer,
	type DraftAttachment,
	type ReportDraft,
} from "./draft"
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
import { useStrayFileDropGuard } from "./useStrayFileDropGuard"
import { isYesNoType, yesNoWord } from "./yesNo"

type LoadState =
	| { status: "loading" }
	| { status: "error" }
	| { status: "ready"; questions: PublicQuestionView[] }

// Every state of the form sits in the same column as the not-tracked notice
// above it, so the confirmation lines up with the page rather than the viewport.
const COLUMN = "mx-auto max-w-measure px-6 py-10"

type SubmitState = { status: "idle" } | { status: "submitting" } | { status: "submitted"; id: string } | { status: "failed"; message: string; keepsLocalState: boolean }

/** Best effort, one request each: an upload this fails to erase is never claimed, and the lifecycle rule expires it. */
function deleteUploads(uploadIds: string[]) {
	for (const uploadId of uploadIds) void deleteUpload(uploadId)
}

type AttachmentMap = Record<string, Attachment[]>

/**
 * Whether any upload has finished, which is when the form asks media consent:
 * it covers photos, video, and documents alike (ADR-0117, ADR-0119).
 */
function hasFileAttached(attachments: AttachmentMap): boolean {
	return Object.values(attachments)
		.flat()
		.some((row) => row.status === "uploaded")
}
type DraftAttachmentMap = Record<string, DraftAttachment[]>

/** The finished uploads the draft keeps, per question; a refused or expired row is not worth restoring. */
function savedAttachments(attachments: AttachmentMap): DraftAttachmentMap {
	const saved: DraftAttachmentMap = {}
	for (const [revisionId, rows] of Object.entries(attachments)) {
		const uploaded = rows
			.filter((row) => row.status === "uploaded" && row.uploadId)
			.map((row) => ({ uploadId: row.uploadId!, name: row.name, size: row.size }))
		if (uploaded.length > 0) saved[revisionId] = uploaded
	}
	return saved
}

function stepQuestionId(step: FormStep): string {
	return step.question.id
}

/** The introduction is the bare form address; every other page is named by its heading question's key (ADR-0099). */
function stepPath(step: FormStep): string {
	return step.kind === "intro" ? "/report" : `/report/${encodeURIComponent(step.question.key)}`
}

export function ReportForm() {
	const { locale, t } = useLocale()
	useStrayFileDropGuard()
	const [load, setLoad] = useState<LoadState>({ status: "loading" })
	const [answers, setAnswers] = useState<AnswerMap>({})
	// Finished uploads per file-upload question. Their IDs and names go into
	// the saved draft beside the answers, so continuing it restores them
	// (ADR-0100). A file still uploading is not here — AttachmentField keeps
	// that, and only reports whether anything is in flight.
	const [attachments, setAttachments] = useState<Record<string, Attachment[]>>({})
	const [uploading, setUploading] = useState<Record<string, boolean>>({})
	// The page shown is whichever the address names. `addressSettled` stays
	// false until the form has loaded and put itself at /report: an address
	// the reporter arrived on never picks the page — the continue dialog, or
	// the introduction, does (REQ-SUB-056, REQ-SUB-057).
	const { stepKey } = useParams<{ stepKey?: string }>()
	const navigate = useNavigate()
	const [addressSettled, setAddressSettled] = useState(false)
	// Purely cosmetic: true for one paint after the step changes, so the new
	// content fades in. Never gates navigation logic — a step change is always
	// applied to the address immediately, so rapid Next/Back clicks (or an
	// impatient reporter) can never race ahead of an in-flight animation.
	const [entering, setEntering] = useState(false)
	const [attemptedAdvance, setAttemptedAdvance] = useState(false)
	const [submit, setSubmit] = useState<SubmitState>({ status: "idle" })
	const offeredDraft = useRef(false)
	const [pendingDraft, setPendingDraft] = useState<ReportDraft | null>(null)
	const [confirmingDiscard, setConfirmingDiscard] = useState(false)
	// True once the reporter has changed anything, so the draft is rewritten
	// even when that change emptied it — a removed file must leave the draft
	// too. Until then, an empty form never overwrites a draft still on offer.
	const edited = useRef(false)

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
		const { draft, expiredUploadIds } = readDraft()
		deleteUploads(expiredUploadIds) // An expired report's files go with it (REQ-SUB-067).
		if (!draft) return
		if (savedAnswerRows(load.questions, draft.answers, draft.attachments ?? {}, locale, t).length === 0) {
			deleteUploads(draftUploadIds(draft))
			clearDraft() // Nothing on this form to continue.
			return
		}
		setPendingDraft(draft)
	}, [load, locale, t])

	// Worded at render, not when offered: the questions can arrive before the
	// locale's catalogue does, and the table must not keep the untranslated keys.
	const pendingRows = useMemo<SavedAnswerRow[]>(
		() =>
			pendingDraft && load.status === "ready"
				? savedAnswerRows(load.questions, pendingDraft.answers, pendingDraft.attachments ?? {}, locale, t)
				: [],
		[pendingDraft, load, locale, t],
	)

	const steps = useMemo(() => (load.status === "ready" ? buildSteps(load.questions) : []), [load])
	const questionsById = useMemo(() => (load.status === "ready" ? indexQuestionsById(load.questions) : new Map()), [load])
	const hasAttachment = useMemo(() => hasFileAttached(attachments), [attachments])
	const visible = useMemo(
		() => visibleSteps(steps, answers, questionsById, locale, hasAttachment),
		[steps, answers, questionsById, locale, hasAttachment],
	)

	const currentIndex = !addressSettled || !stepKey ? 0 : visible.findIndex((step) => step.question.key === stepKey)
	const currentStep = currentIndex >= 0 ? (visible[currentIndex] ?? null) : null
	const currentStepId = currentStep ? stepQuestionId(currentStep) : null

	useEffect(() => {
		if (visible.length === 0) return
		if (!addressSettled) {
			setAddressSettled(true)
			if (stepKey) navigate("/report", { replace: true })
			return
		}
		// Not on the form, or a conditional page not shown right now.
		if (currentIndex < 0) {
			navigate("/report", { replace: true })
			return
		}
		// Browser Forward obeys the same rule as Next: no page past an
		// unanswered required question (REQ-SUB-054).
		const blocked = visible
			.slice(0, currentIndex)
			.find((step) => unansweredRequired(step, answers, questionsById, locale, hasAttachment).length > 0)
		if (blocked) {
			setAttemptedAdvance(true)
			navigate(stepPath(blocked), { replace: true })
		}
	}, [visible, addressSettled, currentIndex, stepKey, navigate, answers, questionsById, locale, hasAttachment])

	// Kept only while there is something worth keeping — an empty draft on a
	// browser that never opened the form would just be noise.
	useEffect(() => {
		if (submit.status === "submitted") return
		const kept = savedAttachments(attachments)
		if (!edited.current && Object.keys(answers).length === 0 && Object.keys(kept).length === 0) return
		writeDraft({ locale, answers, attachments: kept, stepKey: currentStep?.question.key })
	}, [answers, attachments, locale, submit.status, currentStep])

	const anyUploading = Object.values(uploading).some(Boolean)
	const attachedCount = Object.values(attachments)
		.flat()
		.filter((row) => row.status === "uploaded").length
	const attachmentRoom = Math.max(0, MAX_ATTACHMENTS - attachedCount)

	const updateAttachments = useCallback((revisionId: string, update: (current: Attachment[]) => Attachment[]) => {
		edited.current = true
		setAttachments((prev) => ({ ...prev, [revisionId]: update(prev[revisionId] ?? []) }))
	}, [])

	const setQuestionUploading = useCallback((revisionId: string, busy: boolean) => {
		setUploading((prev) => (prev[revisionId] === busy ? prev : { ...prev, [revisionId]: busy }))
	}, [])

	const isFirst = currentIndex === 0
	const isLast = currentIndex >= 0 && currentIndex === visible.length - 1

	function continueDraft() {
		if (!pendingDraft) return
		const draft = pendingDraft
		const restored: AnswerMap = {}
		const restoredFiles: AttachmentMap = {}
		for (const row of pendingRows) {
			if (row.kind === "answer") {
				restored[row.revisionId] = draft.answers[row.revisionId]!
				continue
			}
			restoredFiles[row.revisionId] = (draft.attachments?.[row.revisionId] ?? []).map((file) => ({
				key: `restored-${file.uploadId}`,
				name: file.name,
				size: file.size,
				status: "uploaded",
				uploadId: file.uploadId,
			}))
		}
		// Uploads saved for a question this form no longer shows have nowhere to go.
		const kept = new Set(Object.values(restoredFiles).flat().map((row) => row.uploadId))
		deleteUploads(draftUploadIds(draft).filter((uploadId) => !kept.has(uploadId)))
		setAnswers(restored)
		setAttachments(restoredFiles)
		const savedStep = steps.find((step) =>
			draft.stepKey ? step.question.key === draft.stepKey : step.question.revisionId === draft.stepRevisionId,
		)
		if (savedStep) navigate(stepPath(savedStep), { replace: true })
		setPendingDraft(null)
	}

	function startOver() {
		if (pendingDraft) deleteUploads(draftUploadIds(pendingDraft))
		clearDraft()
		setPendingDraft(null)
	}

	// Leaving the page the field is on abandons anything still uploading
	// (AttachmentField aborts it on unmount); every finished upload is erased.
	function discard() {
		deleteUploads(
			Object.values(savedAttachments(attachments))
				.flat()
				.map((file) => file.uploadId),
		)
		clearDraft()
		edited.current = false
		setAnswers({})
		setAttachments({})
		setUploading({})
		setAttemptedAdvance(false)
		setSubmit({ status: "idle" })
		setConfirmingDiscard(false)
		navigate("/report")
	}

	function setAnswer(revisionId: string, answer: DraftAnswer | undefined) {
		edited.current = true
		setAnswers((prev) => {
			const next = { ...prev }
			if (answer) next[revisionId] = answer
			else delete next[revisionId]
			return next
		})
	}

	// A history entry per page, so the browser's Back and Forward page too (REQ-SUB-053, REQ-SUB-054).
	function goTo(step: FormStep) {
		setAttemptedAdvance(false)
		navigate(stepPath(step))
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
		return unansweredRequired(currentStep, answers, questionsById, locale, hasAttachment)
	}

	function handleNext() {
		if (anyUploading) return
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
		if (anyUploading) return

		setSubmit({ status: "submitting" })

		const submitAnswers: SubmitAnswer[] = []

		for (const step of visible) {
			const questions = step.kind === "group" ? visibleChildren(step.question, answers, questionsById, locale, hasAttachment) : [step.question]

			for (const question of questions) {
				if (collectsNoAnswer(question)) continue

				const answer = answers[question.revisionId]

				if (question.type === "multi_select") {
					submitAnswers.push({
						questionRevisionId: question.revisionId,
						value: null,
						choices: answer?.kind === "options" ? answer.values : [],
						attachments: null,
					})
					continue
				}

				if (question.type === "file_upload") {
					submitAnswers.push({
						questionRevisionId: question.revisionId,
						value: null,
						choices: null,
						attachments: (attachments[question.revisionId] ?? [])
							.filter((row) => row.status === "uploaded" && row.uploadId)
							.map((row) => ({ uploadId: row.uploadId!, fileName: row.name })),
					})
					continue
				}

				const value = answer?.kind === "value" ? answer.value : null

				submitAnswers.push({
					questionRevisionId: question.revisionId,
					// Written in the language the report is sent in (ADR-0127).
					value: value !== null && isYesNoType(question.type) ? yesNoWord(value, locale) : value,
					choices: null,
					attachments: null,
				})
			}
		}

		try {
			const result = await submitReport(locale, submitAnswers)
			clearDraft()
			setSubmit({ status: "submitted", id: result.id })
		} catch (error) {
			if (error instanceof SubmissionNetworkError) {
				setSubmit({ status: "failed", message: t("report.error.network"), keepsLocalState: true })
			} else if (error instanceof SubmissionRejectedError && error.expiredUploadIds.length > 0) {
				// Only those files need attaching again; every other answer and
				// upload stays exactly as it was (REQ-SUB-051).
				const expired = new Set(error.expiredUploadIds)
				setAttachments((prev) =>
					Object.fromEntries(
						Object.entries(prev).map(([revisionId, rows]) => [
							revisionId,
							rows.map((row) => (row.uploadId && expired.has(row.uploadId) ? { ...row, status: "expired" as const } : row)),
						]),
					),
				)
				setSubmit({ status: "failed", message: t("report.attachments.expiredSummary"), keepsLocalState: true })
			} else if (error instanceof SubmissionRejectedError) {
				setSubmit({ status: "failed", message: error.detail, keepsLocalState: true })
			} else {
				setSubmit({ status: "failed", message: t("report.error.submitFailed"), keepsLocalState: true })
			}
		}
	}

	if (load.status === "loading") {
		return (
			<div className={COLUMN}>
				<p aria-busy="true" className="font-sans text-ink-muted">{t("report.loading")}</p>
			</div>
		)
	}

	if (load.status === "error") {
		return (
			<div className={COLUMN}>
				<p role="alert" className="font-sans text-brand-700">{t("report.loadError")}</p>
			</div>
		)
	}

	if (submit.status === "submitted") {
		return (
			<div className={COLUMN}>
				<div role="status" className="rounded border border-rule bg-surface-2 p-6">
					<h2 className="font-display text-xl font-bold text-ink">{t("report.submitted.title")}</h2>
					<p className="mt-2 font-sans text-ink-muted">{t("report.submitted.body")}</p>
				</div>
			</div>
		)
	}

	if (!currentStep) {
		return (
			<div className={COLUMN}>
				<p className="font-sans text-ink-muted">{t("report.loading")}</p>
			</div>
		)
	}

	const blocking = attemptedAdvance ? blockingRequirements() : []
	const hasSomethingToDiscard =
		Object.keys(answers).length > 0 || Object.values(attachments).some((rows) => rows.length > 0) || anyUploading
	const blockingIds = new Set(blocking.map((question) => question.revisionId))

	return (
		<div className={COLUMN}>
			{pendingDraft && (
				<ResumeDraftDialog rows={pendingRows} onContinue={continueDraft} onStartOver={startOver} t={t} />
			)}

			{confirmingDiscard && (
				<DiscardReportDialog onConfirm={discard} onKeep={() => setConfirmingDiscard(false)} t={t} />
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
					attachments={attachments}
					onAttachments={updateAttachments}
					onUploading={setQuestionUploading}
					attachmentRoom={attachmentRoom}
					onAnswer={setAnswer}
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
						disabled={submit.status === "submitting" || anyUploading}
						onClick={handleSubmit}
					>
						{submit.status === "submitting" ? t("report.nav.submitting") : t("report.nav.submit")}
					</button>
				) : (
					<button
						type="button"
						className="touch-target rounded bg-brand-700 px-5 font-sans text-sm font-semibold text-ink-inverse disabled:opacity-60"
						disabled={anyUploading}
						onClick={handleNext}
					>
						{t("report.nav.next")}
					</button>
				)}
			</div>

			{hasSomethingToDiscard && (
				<div className="mt-6 flex justify-center">
					<button
						type="button"
						className="touch-target rounded px-3 font-sans text-sm text-ink-muted underline hover:text-ink"
						onClick={() => setConfirmingDiscard(true)}
					>
						{t("report.discard.open")}
					</button>
				</div>
			)}
		</div>
	)
}

interface StepContentProps {
	step: FormStep
	locale: ReturnType<typeof useLocale>["locale"]
	answers: AnswerMap
	attachments: Record<string, Attachment[]>
	onAttachments: (revisionId: string, update: (current: Attachment[]) => Attachment[]) => void
	onUploading: (revisionId: string, uploading: boolean) => void
	attachmentRoom: number
	onAnswer: (revisionId: string, answer: DraftAnswer | undefined) => void
	blockingIds: Set<string>
	questionsById: Map<string, PublicQuestionView>
	t: (key: string, params?: Record<string, string | number>) => string
}

function StepContent({
	step,
	locale,
	answers,
	attachments,
	onAttachments,
	onUploading,
	attachmentRoom,
	onAnswer,
	blockingIds,
	questionsById,
	t,
}: StepContentProps) {
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
		const children = visibleChildren(step.question, answers, questionsById, locale, hasFileAttached(attachments))
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
							attachments={attachments[child.revisionId] ?? []}
							onAttachmentsChange={(update) => onAttachments(child.revisionId, update)}
							onUploadingChange={(busy) => onUploading(child.revisionId, busy)}
							attachmentRoom={attachmentRoom}
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
			attachments={attachments[step.question.revisionId] ?? []}
			onAttachmentsChange={(update) => onAttachments(step.question.revisionId, update)}
			onUploadingChange={(busy) => onUploading(step.question.revisionId, busy)}
			attachmentRoom={attachmentRoom}
			errorText={blockingIds.has(step.question.revisionId) ? t("report.required.error") : null}
			t={t}
		/>
	)
}
