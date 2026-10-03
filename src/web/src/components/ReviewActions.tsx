import { useState } from "react"
import { useLocale } from "../i18n/useLocale"
import { ApiError, translate } from "../api/adminQuestions"
import type { ReportDetail, SummarySource } from "../api/adminReports"
import { useUnsavedChangesGuard } from "../hooks/useUnsavedChangesGuard"
import { actionsFor } from "./reportReviewActions"
import { ReviewActionsView } from "./ReviewActions.view"

export type { ReportDetail, SummarySource } from "../api/adminReports"
export { actionsFor, type ReviewAction } from "./reportReviewActions"

export type Language = "en" | "fr"

const LOCALE: Record<Language, string> = { en: "en-CA", fr: "fr-CA" }
const OTHER: Record<Language, Language> = { en: "fr", fr: "en" }

export interface ReviewActionsProps {
	report: ReportDetail
	busy: boolean
	// Split across lines on purpose: tools/web/check-hardcoded-strings.ts is a
	// line scanner, and `=> Promise<…>` on one line reads to it as JSX text.
	onSave: (aiSummaryEn: string, aiSummaryFr: string, sourceEn: SummarySource, sourceFr: SummarySource) =>
		Promise<boolean>
	onPublish: () => void
	onUnpublish: (note: string) =>
		Promise<boolean>
	onDelete: () => void
}

/*
 * The action bar and the two inline forms it opens: the summary-pair editor
 * (both languages saved together) and the unpublishing note. The parent owns the
 * requests; this component owns only what the reviewer is typing.
 */
export function useReviewActions({ report, busy, onSave, onUnpublish }: ReviewActionsProps) {
	const { t } = useLocale()
	const [mode, setMode] = useState<"view" | "edit" | "unpublish">("view")
	const [original, setOriginal] = useState<Record<Language, string>>({ en: "", fr: "" })
	const [draft, setDraft] = useState<Record<Language, string>>({ en: "", fr: "" })
	// How each draft got its current text: typed by the reviewer, filled by an
	// accepted translation, or untouched since the editor opened (ADR-0108).
	const [source, setSource] = useState<Record<Language, "typed" | "translated" | null>>({ en: null, fr: null })
	const [proposal, setProposal] = useState<{ target: Language; text: string } | null>(null)
	const [translating, setTranslating] = useState(false)
	const [translateError, setTranslateError] = useState<string | null>(null)
	const [note, setNote] = useState("")

	const dirty =
		(mode === "edit" && (draft.en !== original.en || draft.fr !== original.fr)) ||
		(mode === "unpublish" && note.trim() !== "")
	useUnsavedChangesGuard(dirty)

	function openEditor() {
		const opened = { en: report.summary?.aiSummaryEn ?? "", fr: report.summary?.aiSummaryFr ?? "" }
		setOriginal(opened)
		setDraft(opened)
		setSource({ en: null, fr: null })
		setTranslateError(null)
		setMode("edit")
	}

	function type(language: Language, text: string) {
		setDraft((current) => ({ ...current, [language]: text }))
		setSource((current) => ({ ...current, [language]: "typed" }))
	}

	/** A language the reviewer typed a change into; an accepted translation does not count. */
	const changed = (language: Language) =>
		source[language] === "typed" && draft[language].trim() !== "" && draft[language] !== original[language]

	async function translateFrom(language: Language) {
		const target = OTHER[language]
		setTranslating(true)
		setTranslateError(null)
		try {
			const result = await translate([draft[language]], LOCALE[language], LOCALE[target])
			const text = result.texts[0] ?? ""
			// Nothing to overwrite: fill it straight away. Otherwise ask first (REQ-MOD-072).
			if (draft[target].trim() === "") accept(target, text)
			else setProposal({ target, text })
		} catch (cause) {
			setTranslateError(cause instanceof ApiError ? cause.detail : t("reports.translate.error"))
		} finally {
			setTranslating(false)
		}
	}

	function accept(target: Language, text: string) {
		setDraft((current) => ({ ...current, [target]: text }))
		setSource((current) => ({ ...current, [target]: "translated" }))
		setProposal(null)
	}

	/** A revision holds a change: saving is offered only once a language differs from the current text (REQ-MOD-207). */
	const edited = draft.en !== original.en || draft.fr !== original.fr

	const savedSource = (language: Language): SummarySource =>
		source[language] === "translated" ? "machine" : "human"

	async function save() {
		if (await onSave(draft.en, draft.fr, savedSource("en"), savedSource("fr"))) setMode("view")
	}

	async function unpublish() {
		if (await onUnpublish(note)) {
			setNote("")
			setMode("view")
		}
	}

	return {
		mode,
		draft,
		proposal,
		translating,
		translateError,
		note,
		changedEn: changed("en"),
		changedFr: changed("fr"),
		saveDisabled: busy || !edited || !draft.en.trim() || !draft.fr.trim(),
		actions: actionsFor(report),
		openEditor,
		openUnpublish: () => setMode("unpublish"),
		cancel: () => setMode("view"),
		type,
		changeNote: setNote,
		translateFrom: (language: Language) => void translateFrom(language),
		accept,
		keepCurrent: () => setProposal(null),
		submitEdit: () => void save(),
		submitUnpublish: () => void unpublish(),
	}
}

export function ReviewActions(props: ReviewActionsProps) {
	return <ReviewActionsView {...props} {...useReviewActions(props)} />
}
