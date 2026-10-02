import { useCallback, useEffect, useRef, useState } from "react"
import { useLocale } from "../i18n/useLocale"
import type { QuestionEditorProps } from "../components/QuestionEditor.view"
import { blankDraft, draftFromImported, draftOf, type ImportedQuestionDraftView, type QuestionDraft, type QuestionView } from "../components/questionDraft"
import { useUnsavedChangesGuard } from "../hooks/useUnsavedChangesGuard"
import {
	ApiError,
	createQuestion,
	deleteQuestion,
	listQuestions,
	reorderQuestions,
	reviseQuestion,
	translationAvailable,
} from "../api/adminQuestions"
import { exportTypeform } from "../api/adminTypeformImport"
import { ManageQuestionsPageView, type ManageQuestionsPageViewProps } from "./ManageQuestionsPage.view"

/*
 * The question bank, as a safety officer edits it.
 *
 * Two things here are worth knowing before changing anything:
 *
 *   1. Saving an edit does not update a question. It creates a new revision,
 *      server-side, and every report that answered an older one keeps showing
 *      exactly what it was asked (ADR-0016). A question's choices are the
 *      exception: they are its own and are edited in place (ADR-0095).
 *   2. Reordering is a save too — each moved question gets a revision, written
 *      in one transaction. The list below is therefore re-read from the
 *      response rather than kept optimistically, so what is on screen is always
 *      what the database holds.
 */

/** The question bank page's view model: loading, saving, removing, exporting, reordering, and the editor's lists. */
export function useManageQuestionsPage(): ManageQuestionsPageViewProps {
	const { t, locale } = useLocale()
	const [questions, setQuestions] = useState<QuestionView[]>([])
	const [canTranslate, setCanTranslate] = useState(false)
	const [draft, setDraft] = useState<QuestionDraft | null>(null)
	const [editing, setEditing] = useState<string | null>(null)
	const [error, setError] = useState<string | null>(null)
	const [loading, setLoading] = useState(true)

	// The draft as it stood when the editor opened, whether blank or an
	// existing question's — captured at the moment it opened, alongside
	// `setDraft`, so a direct switch from editing one question to another
	// (no intervening close) compares the new draft against its own opening
	// state, never the previous question's. `null` while no editor is open.
	const openedDraft = useRef<QuestionDraft | null>(null)
	function openDraft(next: QuestionDraft | null) {
		openedDraft.current = next
		setDraft(next)
	}
	const dirty = draft !== null && JSON.stringify(draft) !== JSON.stringify(openedDraft.current)
	useUnsavedChangesGuard(dirty)
	const [importing, setImporting] = useState(false)
	const [exporting, setExporting] = useState(false)

	const report = useCallback(
		(cause: unknown) => setError(cause instanceof ApiError ? cause.detail : t("questions.error.unexpected")),
		[t],
	)

	const load = useCallback(async () => {
		try {
			setLoading(true)
			const [loadedQuestions, translation] = await Promise.all([
				listQuestions(),
				// Asked once, so the Translate control is disabled rather than
				// offered and then failing on a server with no credential.
				translationAvailable().catch(() => ({ available: false })),
			])
			setQuestions(loadedQuestions)
			setCanTranslate(translation.available)
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

			openDraft(null)
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
			// Reloaded first, so the refusal stays on screen: a parent must stay
			// above the questions whose choices depend on it (ADR-0146).
			await load()
			report(cause)
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

	// A single-select's or type-ahead's choices may depend on an earlier
	// single-select or type-ahead that depends on nothing itself, one level deep —
	// so none, for a question other questions' choices already depend on (ADR-0146).
	const editingQuestion = questions.find((question) => question.id === editing)
	// Where a question is asked: on its group's page when it has one (ADR-0076).
	const formPosition = (question: QuestionView): [number, number] => {
		const group = questions.find((candidate) => candidate.id === question.groupedUnderQuestionId)
		return group ? [group.displayOrder, question.displayOrder] : [question.displayOrder, -Infinity]
	}
	const asksBefore = (first: QuestionView, second: QuestionView) => {
		const [a, b] = [formPosition(first), formPosition(second)]
		return a[0] < b[0] || (a[0] === b[0] && a[1] < b[1])
	}
	const choiceParentQuestions = questions.some((question) => question.choicesDependOnQuestionId === editing && editing)
		? []
		: questions.filter(
				(question) =>
					(question.type === "single_select" || question.type === "autocomplete") &&
					question.id !== editing &&
					question.choicesDependOnQuestionId === null &&
					(!editingQuestion || asksBefore(question, editingQuestion)),
			)

	const editor: QuestionEditorProps | null = draft && {
		draft,
		conditionQuestions,
		groupQuestions,
		choiceParentQuestions,
		isEditing: editing !== null,
		hasBeenAnswered: questions.some((question) => question.id === editing && question.hasBeenAnswered),
		translationAvailable: canTranslate,
		// A choice's translation lands as a function of the current draft, so a
		// result arriving after other edits keeps them.
		onChange: (change) =>
			typeof change === "function"
				? setDraft((current) => (current === null ? current : change(current)))
				: setDraft(change),
		onCancel: () => {
			openDraft(null)
			setEditing(null)
		},
		onSave: save,
	}

	return {
		questions,
		loading,
		error,
		editing,
		importing,
		exporting,
		editor,
		startNew: () => {
			setEditing(null)
			openDraft(blankDraft())
			setImporting(false)
		},
		startImport: () => {
			openDraft(null)
			setEditing(null)
			setImporting(true)
		},
		closeImport: () => setImporting(false),
		reviewImported: (imported: ImportedQuestionDraftView) => {
			// A draft whose key matches a live question is the same
			// question re-imported — reviewing it opens the ordinary
			// edit flow (a new revision, or a fork if it has been
			// answered, per ADR-0071) rather than trying to create a
			// second question under the same key.
			const existing = questions.find((question) => question.key === imported.key)

			setEditing(existing?.id ?? null)
			openDraft(draftFromImported(imported, questions))
			setImporting(false)
		},
		exportBank,
		editQuestion: (question: QuestionView) => {
			setImporting(false)
			setEditing(question.id)
			openDraft(draftOf(question, locale))
		},
		remove,
		reorder,
	}
}

export function ManageQuestionsPage() {
	return <ManageQuestionsPageView {...useManageQuestionsPage()} />
}
