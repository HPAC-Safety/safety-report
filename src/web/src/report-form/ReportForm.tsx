import { useCallback, useEffect, useMemo, useRef, useState } from "react"
import { useNavigate, useParams } from "react-router-dom"

import { useLocale } from "../i18n/useLocale"
import { fetchCurrentQuestions, type PublicQuestionView } from "../api/publicQuestions"
import { SubmissionNetworkError, SubmissionRejectedError, submitReport } from "../api/reportSubmission"
import { MAX_ATTACHMENTS, deleteUpload } from "../api/uploads"
import type { Attachment } from "./AttachmentField"
import { markRejectedUploads, hasFileAttached, savedAttachments, type AttachmentMap } from "./attachmentMap"
import {
	clearDraft,
	draftExpiresAtMs,
	draftUploadIds,
	readDraft,
	writeDraft,
	type DraftAnswer,
	type ReportDraft,
} from "./draft"
import { clearedAnswerCount, savedAnswerRows, type SavedAnswerRow } from "./ResumeDraftDialog"
import { ReportFormView, type SubmitState } from "./ReportForm.view"
import { useUnsavedChangesGuard } from "../hooks/useUnsavedChangesGuard"
import { stepPath } from "./stepPath"
import {
	answerProblem,
	blockingQuestions,
	buildSteps,
	consistentAnswers,
	indexQuestionsById,
	isMalformed,
	visibleSteps,
	type AnswerMap,
	type FormStep,
} from "./steps"
import { buildSubmitAnswers } from "./submitAnswers"
import { useStrayFileDropGuard } from "./useStrayFileDropGuard"

type LoadState =
	| { status: "loading" }
	| { status: "error" }
	| { status: "ready"; questions: PublicQuestionView[] }

/** Best effort, one request each: an upload this fails to erase is never claimed, and the lifecycle rule expires it. */
function deleteUploads(uploadIds: string[]) {
	for (const uploadId of uploadIds) void deleteUpload(uploadId)
}

export function useReportForm() {
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
	// True when a restore left saved answers out because their question revision
	// is no longer current; one notice tells the reporter (ADR-0185).
	const [clearedNotice, setClearedNotice] = useState(false)
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
		const rows = savedAnswerRows(load.questions, draft.answers, draft.attachments ?? {}, locale, t)
		if (rows.length === 0) {
			deleteUploads(draftUploadIds(draft))
			clearDraft() // Nothing on this form to continue.
			setClearedNotice(clearedAnswerCount(draft.answers, draft.attachments ?? {}, rows) > 0)
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
	const questionsById = useMemo(() => (load.status === "ready" ? indexQuestionsById(load.questions) : new Map<string, PublicQuestionView>()), [load])
	const hasAttachment = useMemo(() => hasFileAttached(attachments), [attachments])
	const visible = useMemo(
		() => visibleSteps(steps, answers, questionsById, hasAttachment),
		// `locale` is not read here: it makes `visible` a new array when the language
		// changes, which re-runs the effect below, so it stays (no behaviour change).
		// eslint-disable-next-line react-hooks/exhaustive-deps -- see the comment above: locale is a deliberate trigger
		[steps, answers, questionsById, locale, hasAttachment],
	)

	const currentIndex = !addressSettled || !stepKey ? 0 : visible.findIndex((step) => step.question.key === stepKey)
	const currentStep = currentIndex >= 0 ? (visible[currentIndex] ?? null) : null
	const currentStepId = currentStep ? currentStep.question.id : null

	useEffect(() => {
		if (visible.length === 0) return
		if (!addressSettled) {
			setAddressSettled(true)
			if (stepKey) void navigate("/report", { replace: true })
			return
		}
		// Not on the form, or a conditional page not shown right now.
		if (currentIndex < 0) {
			void navigate("/report", { replace: true })
			return
		}
		// Browser Forward obeys the same rule as Next: no page past an
		// unanswered required question (REQ-SUB-054) or a malformed answer.
		const blocked = visible
			.slice(0, currentIndex)
			.find((step) => blockingQuestions(step, answers, questionsById, hasAttachment).length > 0)
		if (blocked) {
			setAttemptedAdvance(true)
			void navigate(stepPath(blocked), { replace: true })
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

	// Unsubmitted answers (issue no. 659). They are saved in this browser for
	// 15 days (ADR-0100), so leaving says so, naming the day they expire, and
	// closing the tab prompts only while an upload — the one thing leaving
	// loses — is in flight (issue no. 748). With nothing saved yet, the shared
	// wording stands. Moving between the form's own steps stays within
	// "/report" and is never blocked.
	const dirty =
		submit.status !== "submitted" &&
		(Object.keys(answers).length > 0 || Object.values(attachments).some((rows) => rows.length > 0) || anyUploading)
	const leaveCopy = useCallback(() => {
		const expiresAtMs = draftExpiresAtMs()
		if (expiresAtMs === null) return undefined
		const date = new Intl.DateTimeFormat(locale, { dateStyle: "long" }).format(new Date(expiresAtMs))
		const saved = t("report.leave.body", { date })
		return {
			title: t("report.leave.title"),
			body: anyUploading ? `${saved} ${t("report.leave.uploading")}` : saved,
			leave: t("unsavedChanges.leave"),
			stay: t("report.leave.keepWorking"),
		}
	}, [anyUploading, locale, t])
	useUnsavedChangesGuard(dirty, { withinPath: "/report", unloadPrompt: anyUploading, copy: leaveCopy })

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
				// eslint-disable-next-line @typescript-eslint/no-non-null-assertion -- a row is listed only for an answer the draft holds
				restored[row.revisionId] = draft.answers[row.revisionId]!
				continue
			}
			// Defensive: the draft is untrusted storage. An attachments row is only
			// listed for files the draft names, so no test can reach the fallback.
			/* v8 ignore next */
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
		// A saved child answer its saved parent's answer no longer allows is dropped (ADR-0146).
		setAnswers(consistentAnswers(restored, questionsById))
		setAttachments(restoredFiles)
		setClearedNotice(clearedAnswerCount(draft.answers, draft.attachments ?? {}, pendingRows) > 0)
		const savedStep = steps.find((step) =>
			draft.stepKey ? step.question.key === draft.stepKey : step.question.revisionId === draft.stepRevisionId,
		)
		if (savedStep) void navigate(stepPath(savedStep), { replace: true })
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
		setClearedNotice(false)
		setAnswers({})
		setAttachments({})
		setUploading({})
		setAttemptedAdvance(false)
		setSubmit({ status: "idle" })
		setConfirmingDiscard(false)
		void navigate("/report")
	}

	function setAnswer(revisionId: string, answer: DraftAnswer | undefined) {
		edited.current = true
		setAnswers((prev) => {
			const { [revisionId]: _removed, ...without } = prev
			const next = answer ? { ...prev, [revisionId]: answer } : without
			// A changed parent answer clears a child answer it no longer allows (ADR-0146).
			return consistentAnswers(next, questionsById)
		})
	}

	// A history entry per page, so the browser's Back and Forward page too (REQ-SUB-053, REQ-SUB-054).
	function goTo(step: FormStep) {
		setAttemptedAdvance(false)
		void navigate(stepPath(step))
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
		return blockingQuestions(currentStep, answers, questionsById, hasAttachment)
	}

	function handleNext() {
		if (anyUploading) return
		const blocking = blockingRequirements()
		if (blocking.length > 0) {
			setAttemptedAdvance(true)
			return
		}
		const next = visible[currentIndex + 1] as FormStep | undefined
		if (next) goTo(next)
	}

	function handleBack() {
		const previous = visible[currentIndex - 1] as FormStep | undefined
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

		const submitAnswers = buildSubmitAnswers(visible, answers, attachments, questionsById, hasAttachment)

		try {
			const result = await submitReport(locale, submitAnswers)
			clearDraft()
			setSubmit({ status: "submitted", id: result.id })
		} catch (error) {
			if (error instanceof SubmissionNetworkError) {
				setSubmit({ status: "failed", message: t("report.error.network"), keepsLocalState: true })
			} else if (
				error instanceof SubmissionRejectedError &&
				(error.expiredUploadIds.length > 0 || error.refusedUploads.length > 0)
			) {
				// Only those files are marked — expired ones to attach again,
				// refused ones with the reason sniffing gave; every other answer and
				// upload stays exactly as it was (REQ-SUB-051, REQ-SUB-076).
				const expired = new Set(error.expiredUploadIds)
				const refused = new Map(error.refusedUploads.map((upload) => [upload.uploadId, upload.reason]))
				// A refused upload is never claimed; it is erased now rather than
				// left for the lifecycle rule.
				deleteUploads([...refused.keys()])
				setAttachments((prev) => markRejectedUploads(prev, expired, refused))
				setSubmit({
					status: "failed",
					message: t(expired.size > 0 ? "report.attachments.expiredSummary" : "report.attachments.refusedSummary"),
					keepsLocalState: true,
				})
			} else if (error instanceof SubmissionRejectedError) {
				setSubmit({ status: "failed", message: error.detail, keepsLocalState: true })
			} else {
				setSubmit({ status: "failed", message: t("report.error.submitFailed"), keepsLocalState: true })
			}
		}
	}

	const blocking = attemptedAdvance ? blockingRequirements() : []
	const hasSomethingToDiscard =
		Object.keys(answers).length > 0 || Object.values(attachments).some((rows) => rows.length > 0) || anyUploading
	// Each blocking question's message: a malformed email, phone, or date answer
	// says what it needs (REQ-SUB-087, REQ-SUB-088, REQ-SUB-101); anything else
	// is unanswered.
	const blockingMessages = new Map(
		blocking.map((question) => [
			question.revisionId,
			t(answerProblem(question, answers[question.revisionId]) ?? "report.required.error"),
		]),
	)
	const anyUnanswered = blocking.some((question) => !isMalformed(question, answers[question.revisionId]))

	return {
		loadStatus: load.status,
		submit,
		currentStep,
		currentIndex,
		visibleCount: visible.length,
		pendingDraft: pendingDraft !== null,
		pendingRows,
		onContinue: continueDraft,
		onStartOver: startOver,
		confirmingDiscard,
		onDiscard: discard,
		onKeepReport: () => setConfirmingDiscard(false),
		onOpenDiscard: () => setConfirmingDiscard(true),
		clearedNotice,
		blocking,
		anyUnanswered,
		entering,
		answers,
		attachments,
		onAttachments: updateAttachments,
		onUploading: setQuestionUploading,
		attachmentRoom,
		onAnswer: setAnswer,
		blockingMessages,
		questionsById,
		isFirst,
		isLast,
		anyUploading,
		onBack: handleBack,
		onNext: handleNext,
		onSubmit: handleSubmit,
		hasSomethingToDiscard,
		privacyNotice: t("report.privacy.localStorage"),
	}
}

export function ReportForm() {
	return <ReportFormView {...useReportForm()} />
}
