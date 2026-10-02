import { useEffect, useState } from "react"
import { useLocale } from "../i18n/useLocale"
import { ApiError } from "../api/adminQuestions"
import {
	deletePendingImportLogic,
	importTypeform,
	listPendingImportLogic,
	type ImportedQuestionDraftView,
	type PendingImportLogicView,
	type RejectedTypeformFieldView,
} from "../api/adminTypeformImport"
import { TypeformImportDialogView } from "./TypeformImportDialog.view"

/*
 * The Typeform import dialog (ADR-0077, ADR-0078).
 *
 * Importing never saves a Question by itself — it only produces drafts.
 * Clicking "Review" on a draft hands it to the caller, which opens the
 * ordinary QuestionEditor so the administrator makes the same save decision
 * as any other question. A rejected field or an unmapped logic branch is
 * shown so nothing from the source form silently disappears.
 */

export interface TypeformImportDialogProps {
	onReview: (imported: ImportedQuestionDraftView) => void
	onClose: () => void
}

export function useTypeformImportDialog({ onReview }: TypeformImportDialogProps) {
	const { t } = useLocale()
	const [english, setEnglish] = useState<File | null>(null)
	const [french, setFrench] = useState<File | null>(null)
	const [importing, setImporting] = useState(false)
	const [error, setError] = useState<string | null>(null)
	const [drafts, setDrafts] = useState<ImportedQuestionDraftView[]>([])
	const [rejected, setRejected] = useState<RejectedTypeformFieldView[]>([])
	const [pendingLogic, setPendingLogic] = useState<PendingImportLogicView[]>([])
	const [reviewedKeys, setReviewedKeys] = useState<Set<string>>(new Set())

	const report = (cause: unknown) =>
		setError(cause instanceof ApiError ? cause.detail : t("questions.import.error.unexpected"))

	async function loadPendingLogic() {
		try {
			setPendingLogic(await listPendingImportLogic())
		} catch (cause) {
			report(cause)
		}
	}

	useEffect(() => {
		void loadPendingLogic()
	}, [])

	async function runImport() {
		if (!english || !french) return

		setImporting(true)
		setError(null)

		try {
			const result = await importTypeform(english, french)
			setDrafts(result.drafts)
			setRejected(result.rejected)
			setReviewedKeys(new Set())
			await loadPendingLogic()
		} catch (cause) {
			report(cause)
		} finally {
			setImporting(false)
		}
	}

	async function removePendingLogic(id: string) {
		try {
			await deletePendingImportLogic(id)
			setPendingLogic((current) => current.filter((note) => note.id !== id))
		} catch (cause) {
			report(cause)
		}
	}

	function reviewDraft(draft: ImportedQuestionDraftView) {
		setReviewedKeys((current) => new Set(current).add(draft.key))
		onReview(draft)
	}

	return {
		english,
		french,
		importing,
		error,
		drafts,
		rejected,
		pendingLogic,
		reviewedKeys,
		onEnglishChange: setEnglish,
		onFrenchChange: setFrench,
		runImport,
		removePendingLogic,
		reviewDraft,
	}
}

export function TypeformImportDialog(props: TypeformImportDialogProps) {
	return <TypeformImportDialogView onClose={props.onClose} {...useTypeformImportDialog(props)} />
}
