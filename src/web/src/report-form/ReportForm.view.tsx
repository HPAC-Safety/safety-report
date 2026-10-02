import type { Locale } from "../i18n/locales"
import { useLocale } from "../i18n/useLocale"
import type { Attachment } from "./AttachmentField"
import { DiscardReportDialog } from "./DiscardReportDialog"
import type { DraftAnswer } from "./draft"
import { QuestionField } from "./QuestionField"
import { ResumeDraftDialog, type SavedAnswerRow } from "./ResumeDraftDialog"
import { choiceScope, collectsNoAnswer, questionHelp, questionLabel, visibleChildren, type AnswerMap, type FormStep } from "./steps"
import { hasFileAttached } from "./attachmentMap"

type PublicQuestionView = FormStep["question"]

// Every state of the form sits in the same column as the not-tracked notice
// above it, so the confirmation lines up with the page rather than the viewport.
const COLUMN = "mx-auto max-w-measure px-6 py-10"

export type SubmitState = { status: "idle" } | { status: "submitting" } | { status: "submitted"; id: string } | { status: "failed"; message: string; keepsLocalState: boolean }

export interface ReportFormViewProps {
	loadStatus: "loading" | "error" | "ready"
	submit: SubmitState
	currentStep: FormStep | null
	currentIndex: number
	visibleCount: number
	pendingDraft: boolean
	pendingRows: SavedAnswerRow[]
	onContinue: () => void
	onStartOver: () => void
	confirmingDiscard: boolean
	onDiscard: () => void
	onKeepReport: () => void
	onOpenDiscard: () => void
	clearedNotice: boolean
	blocking: PublicQuestionView[]
	anyUnanswered: boolean
	entering: boolean
	answers: AnswerMap
	attachments: Record<string, Attachment[]>
	onAttachments: (revisionId: string, update: (current: Attachment[]) => Attachment[]) => void
	onUploading: (revisionId: string, uploading: boolean) => void
	attachmentRoom: number
	onAnswer: (revisionId: string, answer: DraftAnswer | undefined) => void
	blockingMessages: Map<string, string>
	questionsById: Map<string, PublicQuestionView>
	isFirst: boolean
	isLast: boolean
	anyUploading: boolean
	onBack: () => void
	onNext: () => void
	onSubmit: () => void
	hasSomethingToDiscard: boolean
	/** The not-tracked notice above the form, worded by the view model. */
	privacyNotice: string
}

export function ReportFormView({
	loadStatus,
	submit,
	currentStep,
	currentIndex,
	visibleCount,
	pendingDraft,
	pendingRows,
	onContinue,
	onStartOver,
	confirmingDiscard,
	onDiscard,
	onKeepReport,
	onOpenDiscard,
	clearedNotice,
	blocking,
	anyUnanswered,
	entering,
	answers,
	attachments,
	onAttachments,
	onUploading,
	attachmentRoom,
	onAnswer,
	blockingMessages,
	questionsById,
	isFirst,
	isLast,
	anyUploading,
	onBack,
	onNext,
	onSubmit,
	hasSomethingToDiscard,
	privacyNotice,
}: ReportFormViewProps) {
	const { locale, t } = useLocale()

	if (loadStatus === "loading") {
		return (
			<div className={COLUMN}>
				<p aria-busy="true" className="font-sans text-ink-muted">{t("report.loading")}</p>
			</div>
		)
	}

	if (loadStatus === "error") {
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

	return (
		<div className={COLUMN}>
			{pendingDraft && (
				<ResumeDraftDialog rows={pendingRows} onContinue={onContinue} onStartOver={onStartOver} t={t} />
			)}

			{confirmingDiscard && (
				<DiscardReportDialog onConfirm={onDiscard} onKeep={onKeepReport} t={t} />
			)}

			<p role="status" className="sr-only">
				{t("report.progress", { current: currentIndex + 1, total: visibleCount })}
			</p>

			<div className="rounded border border-rule bg-surface p-4 sm:p-6">
				<p className="font-sans text-xs text-ink-muted">
					{privacyNotice}
				</p>
			</div>

			{clearedNotice && (
				<p
					data-testid="cleared-answers-notice"
					className="mt-4 rounded border border-rule bg-surface-2 p-4 font-sans text-sm text-ink"
				>
					{t("report.resume.cleared")}
				</p>
			)}

			{blocking.length > 0 && (
				<div role="alert" className="mt-4 rounded border border-brand-700 bg-surface p-4">
					<p className="font-sans text-sm font-semibold text-brand-700">
						{t(anyUnanswered ? "report.required.summary" : "report.invalid.summary")}
					</p>
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
					onAttachments={onAttachments}
					onUploading={onUploading}
					attachmentRoom={attachmentRoom}
					onAnswer={onAnswer}
					blockingMessages={blockingMessages}
					questionsById={questionsById}
					t={t}
				/>
			</div>

			<div className="mt-8 flex items-center justify-between gap-4">
				{!isFirst && currentStep.kind !== "intro" ? (
					<button
						type="button"
						className="touch-target rounded border border-rule px-4 font-sans text-sm text-ink hover:bg-surface-2"
						onClick={onBack}
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
						onClick={onSubmit}
					>
						{submit.status === "submitting" ? t("report.nav.submitting") : t("report.nav.submit")}
					</button>
				) : (
					<button
						type="button"
						className="touch-target rounded bg-brand-700 px-5 font-sans text-sm font-semibold text-ink-inverse disabled:opacity-60"
						disabled={anyUploading}
						onClick={onNext}
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
						onClick={onOpenDiscard}
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
	locale: Locale
	answers: AnswerMap
	attachments: Record<string, Attachment[]>
	onAttachments: (revisionId: string, update: (current: Attachment[]) => Attachment[]) => void
	onUploading: (revisionId: string, uploading: boolean) => void
	attachmentRoom: number
	onAnswer: (revisionId: string, answer: DraftAnswer | undefined) => void
	blockingMessages: Map<string, string>
	questionsById: Map<string, PublicQuestionView>
	t: (key: string, params?: Record<string, string | number>) => string
}

/**
 * A statement's description, in the reader's language, keeping the line breaks
 * it was written with so its paragraphs stay apart (REQ-QB-143).
 */
function StatementDescription({ question, locale }: { question: PublicQuestionView; locale: Locale }) {
	const description = questionHelp(question, locale)
	if (!description) return null
	return <p className="mt-3 whitespace-pre-line font-sans text-ink-muted">{description}</p>
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
	blockingMessages,
	questionsById,
	t,
}: StepContentProps) {
	// A picker or type-ahead whose choices depend on another question offers only
	// what that question's answer allows, and waits, disabled, until it is
	// answered (ADR-0146).
	function dependent(question: PublicQuestionView) {
		const scope = choiceScope(question, answers, questionsById)
		if (scope.kind === "all") return { question, disabled: false, note: null }

		const parent = questionLabel(scope.parent, locale)
		if (scope.kind === "waiting") {
			const note = t("report.dependent.answerFirst", { question: parent })
			return { question: { ...question, options: [] }, disabled: true, note, announcement: "" }
		}

		const picker = question.type !== "autocomplete"
		const empty = scope.options.length === 0
		const note = empty ? t(picker ? "report.dependent.nothingUnder" : "report.dependent.typeOne", { question: parent }) : null
		return {
			question: { ...question, options: scope.options },
			disabled: picker && empty,
			note,
			announcement: note ?? t("report.dependent.ready", { question: questionLabel(question, locale), parent }),
		}
	}

	if (step.kind === "intro") {
		return (
			<div>
				<h1 className="font-display text-2xl font-bold text-ink">{questionLabel(step.question, locale)}</h1>
				<StatementDescription question={step.question} locale={locale} />
			</div>
		)
	}

	if (step.kind === "group") {
		const children = visibleChildren(step.question, answers, questionsById, hasFileAttached(attachments))
		return (
			<fieldset>
				<legend className="font-display text-lg font-semibold text-ink">{questionLabel(step.question, locale)}</legend>
				<div className="mt-4">
					{children.map((child) =>
						// A statement under a group is a sub-heading with its
						// description, never an input (REQ-QB-143).
						collectsNoAnswer(child) ? (
							<div key={child.revisionId} className="mb-6">
								<h3 className="font-display text-base font-semibold text-ink">{questionLabel(child, locale)}</h3>
								<StatementDescription question={child} locale={locale} />
							</div>
						) : (
							<QuestionField
								key={child.revisionId}
								{...dependent(child)}
								locale={locale}
								answer={answers[child.revisionId]}
								onChange={(answer) => onAnswer(child.revisionId, answer)}
								attachments={attachments[child.revisionId] ?? []}
								onAttachmentsChange={(update) => onAttachments(child.revisionId, update)}
								onUploadingChange={(busy) => onUploading(child.revisionId, busy)}
								attachmentRoom={attachmentRoom}
								errorText={blockingMessages.get(child.revisionId) ?? null}
								t={t}
							/>
						),
					)}
				</div>
			</fieldset>
		)
	}

	if (collectsNoAnswer(step.question)) {
		return (
			<div>
				<h2 className="font-display text-xl font-bold text-ink">{questionLabel(step.question, locale)}</h2>
				<StatementDescription question={step.question} locale={locale} />
			</div>
		)
	}

	return (
		<QuestionField
			{...dependent(step.question)}
			locale={locale}
			answer={answers[step.question.revisionId]}
			onChange={(answer) => onAnswer(step.question.revisionId, answer)}
			attachments={attachments[step.question.revisionId] ?? []}
			onAttachmentsChange={(update) => onAttachments(step.question.revisionId, update)}
			onUploadingChange={(busy) => onUploading(step.question.revisionId, busy)}
			attachmentRoom={attachmentRoom}
			errorText={blockingMessages.get(step.question.revisionId) ?? null}
			t={t}
		/>
	)
}
