import { useCallback, useEffect, useState } from "react"
import { useLocale } from "../i18n/useLocale"
import { SortableList } from "../components/SortableList"
import { QuestionEditor, type QuestionDraft, blankDraft, draftFromImported, draftOf } from "../components/QuestionEditor"
import { TypeformImportDialog } from "../components/TypeformImportDialog"
import {
	ApiError,
	createQuestion,
	deleteQuestion,
	listOptionSets,
	listQuestions,
	reorderQuestions,
	reviseQuestion,
	translationAvailable,
	type OptionSetView,
	type QuestionView,
} from "../api/adminQuestions"
import { exportTypeform } from "../api/adminTypeformImport"

/*
 * The question bank, as a safety officer edits it.
 *
 * Two things here are worth knowing before changing anything:
 *
 *   1. Saving an edit does not update a question. It creates a new revision,
 *      server-side, and every report that answered an older one keeps showing
 *      exactly what it was asked (ADR-0016).
 *   2. Reordering is a save too — each moved question gets a revision, written
 *      in one transaction. The list below is therefore re-read from the
 *      response rather than kept optimistically, so what is on screen is always
 *      what the database holds.
 */

export function ManageQuestionsPage() {
	const { t } = useLocale()
	const [questions, setQuestions] = useState<QuestionView[]>([])
	const [optionSets, setOptionSets] = useState<OptionSetView[]>([])
	const [canTranslate, setCanTranslate] = useState(false)
	const [translationIsStandIn, setTranslationIsStandIn] = useState(false)
	const [draft, setDraft] = useState<QuestionDraft | null>(null)
	const [editing, setEditing] = useState<string | null>(null)
	const [error, setError] = useState<string | null>(null)
	const [loading, setLoading] = useState(true)
	const [importing, setImporting] = useState(false)
	const [exporting, setExporting] = useState(false)

	const report = useCallback(
		(cause: unknown) => setError(cause instanceof ApiError ? cause.detail : t("questions.error.unexpected")),
		[t],
	)

	const load = useCallback(async () => {
		try {
			setLoading(true)
			const [loadedQuestions, loadedSets, translation] = await Promise.all([
				listQuestions(),
				listOptionSets(),
				// Asked once, so the Translate control is disabled rather than
				// offered and then failing on a server with no credential.
				translationAvailable().catch(() => ({ available: false, standIn: false })),
			])
			setQuestions(loadedQuestions)
			setOptionSets(loadedSets)
			setCanTranslate(translation.available)
			setTranslationIsStandIn(translation.standIn)
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

	async function save(value: QuestionDraft) {
		try {
			if (editing) {
				await reviseQuestion(editing, value.request)
			} else {
				await createQuestion(value.request)
			}

			setDraft(null)
			setEditing(null)
			await load()
		} catch (cause) {
			report(cause)
		}
	}

	async function remove(question: QuestionView) {
		try {
			await deleteQuestion(question.id)
			await load()
		} catch (cause) {
			report(cause)
		}
	}

	async function exportBank() {
		try {
			setExporting(true)
			const blob = await exportTypeform()
			const url = URL.createObjectURL(blob)
			const link = document.createElement("a")

			link.href = url
			link.download = "question-bank.zip"
			link.click()
			URL.revokeObjectURL(url)
		} catch (cause) {
			report(cause)
		} finally {
			setExporting(false)
		}
	}

	async function reorder(idsInOrder: string[]) {
		try {
			setQuestions(await reorderQuestions(idsInOrder))
		} catch (cause) {
			report(cause)
			await load()
		}
	}

	// Only a yes/no or single-select question can enable another one, so those
	// are the only candidates the picker offers (ADR-0060, ADR-0074). A
	// question never offers itself.
	const conditionQuestions = questions.filter(
		(question) => (question.type === "yes_no" || question.type === "single_select") && question.id !== editing,
	)

	// Only a group question can have other questions displayed under it, and a
	// group cannot itself be grouped under another one (ADR-0076).
	const groupQuestions = questions.filter((question) => question.type === "group" && question.id !== editing)

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
					onClick={() => {
						setEditing(null)
						setDraft(blankDraft())
						setImporting(false)
					}}
				>
					{t("questions.addQuestion")}
				</button>
				<button
					type="button"
					className="touch-target inline-flex items-center rounded border border-rule px-5 font-sans text-ink hover:bg-surface-2"
					onClick={() => {
						setDraft(null)
						setEditing(null)
						setImporting(true)
					}}
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
					onReview={(imported) => {
						// A draft whose key matches a live question is the same
						// question re-imported — reviewing it opens the ordinary
						// edit flow (a new revision, or a fork if it has been
						// answered, per ADR-0071) rather than trying to create a
						// second question under the same key.
						const existing = questions.find((question) => question.key === imported.key)

						setEditing(existing?.id ?? null)
						setDraft(draftFromImported(imported, questions))
						setImporting(false)
					}}
					onClose={() => setImporting(false)}
				/>
			)}

			{draft && (
				<QuestionEditor
					draft={draft}
					optionSets={optionSets}
					conditionQuestions={conditionQuestions}
					groupQuestions={groupQuestions}
					isEditing={editing !== null}
					hasBeenAnswered={questions.some((question) => question.id === editing && question.hasBeenAnswered)}
					translationAvailable={canTranslate}
					translationIsStandIn={translationIsStandIn}
					onChange={setDraft}
					onCancel={() => {
						setDraft(null)
						setEditing(null)
					}}
					onSave={save}
				/>
			)}

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
						{(question) => (
							<QuestionRow
								question={question}
								questions={questions}
								onEdit={() => {
									setEditing(question.id)
									setDraft(draftOf(question))
								}}
								onDelete={() => void remove(question)}
							/>
						)}
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
				<p className="font-sans font-medium text-ink">{question.labelEn}</p>
				<p className="font-sans text-sm text-ink-muted">{question.labelFr}</p>
				<p className="mt-2 font-sans text-xs text-ink-muted">
					{t(`questions.type.${question.type}`)} · {t("questions.revisionNumber", { number: String(question.revisionNumber) })}
					{question.isRequired ? ` · ${t("questions.required")}` : ` · ${t("questions.optional")}`}
					{question.isPrivate ? ` · ${t("questions.private")}` : ""}
					{question.isActive ? "" : ` · ${t("questions.inactive")}`}
				</p>
				{parent && (
					<p className="mt-1 font-sans text-xs text-ink-muted">
						{question.dependsOnOptionCode
							? t("questions.dependsOnOptionSummary", {
									question: parent.labelEn,
									option:
										parent.options.find((option) => option.code === question.dependsOnOptionCode)?.labelEn ??
										question.dependsOnOptionCode,
								})
							: t("questions.dependsOnSummary", { question: parent.labelEn })}
					</p>
				)}
				{groupParent && (
					<p className="mt-1 font-sans text-xs text-ink-muted">
						{t("questions.groupedUnderSummary", { question: groupParent.labelEn })}
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
