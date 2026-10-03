import { SortableList } from "../components/SortableList"
import { QuestionEditor } from "../components/QuestionEditor"
import { TypeformImportDialog } from "../components/TypeformImportDialog"
import { useLocale } from "../i18n/useLocale"
import { labelWithColon } from "../lib/questionPrompt"
import type { QuestionEditorProps } from "../components/QuestionEditor.view"
import type { ImportedQuestionDraftView, QuestionView } from "../components/questionDraft"

/*
 * The question bank's page: markup only (ADR-0188). Loading, saving, removing,
 * exporting and reordering are the view model in ManageQuestionsPage.tsx.
 */

export interface ManageQuestionsPageViewProps {
	questions: QuestionView[]
	loading: boolean
	error: string | null
	editing: string | null
	importing: boolean
	exporting: boolean
	/** The props of the open editor, or null while none is open. */
	editor: QuestionEditorProps | null
	startNew: () => void
	startImport: () => void
	closeImport: () => void
	reviewImported: (imported: ImportedQuestionDraftView) => void
	exportBank: () => Promise<void>
	editQuestion: (question: QuestionView) => void
	remove: (question: QuestionView) => Promise<void>
	reorder: (idsInOrder: string[]) => Promise<void>
}

export function ManageQuestionsPageView({
	questions,
	loading,
	error,
	editing,
	importing,
	exporting,
	editor,
	startNew,
	startImport,
	closeImport,
	reviewImported,
	exportBank,
	editQuestion,
	remove,
	reorder,
}: ManageQuestionsPageViewProps) {
	const { t } = useLocale()
	const editorElement = editor && <QuestionEditor {...editor} />

	return (
		<main className="mx-auto max-w-4xl px-6 py-12">
			<h1 className="font-display text-3xl font-bold">{t("questions.title")}</h1>
			<p className="mt-2 font-sans text-ink-muted">{t("questions.intro")}</p>

			{error && (
				<p role="alert" className="mt-6 rounded border border-brand-700 bg-surface-2 p-4 font-sans text-ink">
					{error}
				</p>
			)}

			<div className="mt-8 flex flex-wrap gap-3">
				<button
					type="button"
					className="touch-target inline-flex items-center rounded bg-brand-700 px-5 font-sans font-medium text-ink-inverse hover:bg-brand-600"
					onClick={startNew}
				>
					{t("questions.addQuestion")}
				</button>
				<button
					type="button"
					className="touch-target inline-flex items-center rounded border border-rule px-5 font-sans text-ink hover:bg-surface-2"
					onClick={startImport}
				>
					{t("questions.import.openDialog")}
				</button>
				<button
					type="button"
					className="touch-target inline-flex items-center rounded border border-rule px-5 font-sans text-ink hover:bg-surface-2 disabled:opacity-40"
					disabled={exporting}
					onClick={() => void exportBank()}
				>
					{exporting ? t("questions.export.working") : t("questions.export.action")}
				</button>
			</div>

			{importing && (
				<TypeformImportDialog
					onReview={reviewImported}
					onClose={closeImport}
				/>
			)}

			{/* A new question is authored above the list; an existing one is
			    edited in its own row, where the administrator clicked Edit. */}
			{editor && editing === null && <div className="mt-6">{editorElement}</div>}

			<h2 className="mt-12 font-display text-2xl font-bold">{t("questions.listTitle")}</h2>

			{loading ? (
				<p className="mt-4 font-sans text-ink-muted">{t("questions.loading")}</p>
			) : questions.length === 0 ? (
				<p className="mt-4 font-sans text-ink-muted">{t("questions.empty")}</p>
			) : (
				<div className="mt-4">
					<SortableList
						items={questions}
						getId={(question) => question.id}
						onReorder={reorder}
						label={t("questions.listTitle")}
					>
						{(question) =>
							editor && question.id === editing ? (
								editorElement
							) : (
								<QuestionRow
									question={question}
									questions={questions}
									onEdit={() => editQuestion(question)}
									onDelete={() => void remove(question)}
								/>
							)
						}
					</SortableList>
				</div>
			)}
		</main>
	)
}

const rowButtonClassName =
	"touch-target inline-flex items-center rounded border border-rule px-3 font-sans text-sm text-ink hover:bg-surface-2"

function QuestionRow({
	question,
	questions,
	onEdit,
	onDelete,
}: {
	question: QuestionView
	questions: QuestionView[]
	onEdit: () => void
	onDelete: () => void
}) {
	const { t } = useLocale()
	const parent = questions.find((candidate) => candidate.id === question.dependsOnQuestionId)
	const groupParent = questions.find((candidate) => candidate.id === question.groupedUnderQuestionId)

	return (
		<div className="flex flex-wrap items-start justify-between gap-4">
			<div className="min-w-0">
				<p className="font-sans font-medium text-ink" lang="en-CA" data-label="en">
					{labelWithColon(question.labelEn, question.type, "en-CA")}
				</p>
				<p className="font-sans text-sm text-ink-muted" lang="fr-CA" data-label="fr">
					{labelWithColon(question.labelFr, question.type, "fr-CA")}
				</p>
				<p className="mt-2 font-sans text-xs text-ink-muted">
					{t(`questions.type.${question.type}`)} · {t("questions.revisionNumber", { number: String(question.revisionNumber) })}
					{question.isRequired ? ` · ${t("questions.required")}` : ` · ${t("questions.optional")}`}
					{question.isPrivate ? ` · ${t("questions.private")}` : ""}
					{question.isActive ? "" : ` · ${t("questions.inactive")}`}
				</p>
				{parent && (
					<p className="mt-1 font-sans text-xs text-ink-muted">
						{question.dependsOnChoiceId
							? t("questions.dependsOnOptionSummary", {
									question: parent.labelEn,
									option:
										parent.options.find((option) => option.id === question.dependsOnChoiceId)?.labelEn ??
										question.dependsOnChoiceId,
								})
							: t("questions.dependsOnSummary", { question: parent.labelEn })}
					</p>
				)}
				{groupParent && (
					<p className="mt-1 font-sans text-xs text-ink-muted">
						{t("questions.groupedUnderSummary", { question: groupParent.labelEn })}
					</p>
				)}
				{question.reporterChoicesAwaitingReview > 0 && (
					<p className="mt-2 font-sans text-xs font-medium text-ink">
						{t("questions.reporterChoicesAwaiting", { count: String(question.reporterChoicesAwaitingReview) })}
					</p>
				)}
			</div>

			<div className="flex gap-2">
				<button type="button" className={rowButtonClassName} onClick={onEdit}>
					{t("questions.edit")}
				</button>
				{!question.isSystem && (
					<button type="button" className={rowButtonClassName} onClick={onDelete}>
						{t("questions.delete")}
					</button>
				)}
			</div>
		</div>
	)
}
