import { useState } from "react"
import { useLocale } from "../i18n/useLocale"
import { ApiError, translate } from "../api/adminQuestions"
import type { ReportDetail, ReportStatus, SummarySource } from "../api/adminReports"
import { TranslateConfirmDialog } from "./TranslateConfirmDialog"

type Language = "en" | "fr"

const LOCALE: Record<Language, string> = { en: "en-CA", fr: "fr-CA" }
const OTHER: Record<Language, Language> = { en: "fr", fr: "en" }

/** What a reviewer may do in each state (REQ-MOD-062, ADR-0125). Delete is always offered. */
export type ReviewAction = "edit" | "write" | "publish" | "unpublish" | "delete"

const ACTIONS: Record<ReportStatus, ReviewAction[]> = {
	pending: ["edit", "publish", "unpublish", "delete"],
	published: ["edit", "unpublish", "delete"],
	unpublished: ["edit", "publish", "delete"],
	summary_failed: ["write", "delete"],
	submitted: ["delete"],
	summarizing: ["delete"],
}

/**
 * A report whose reporter did not consent is unpublished for good: it is never
 * summarized, and deleting it is the one thing a reviewer can do (REQ-DOM-015).
 */
function actionsFor(report: ReportDetail): ReviewAction[] {
	return report.consent === "yes" ? ACTIONS[report.status] : ["delete"]
}

const PRIMARY =
	"touch-target inline-flex items-center rounded bg-brand-700 px-5 font-sans font-medium text-ink-inverse hover:bg-brand-600 disabled:opacity-50"
const SECONDARY =
	"touch-target inline-flex items-center rounded border border-rule px-4 font-sans text-ink hover:bg-surface-2 disabled:opacity-50"
const FIELD = "mt-1 w-full rounded border border-rule bg-surface-2 px-3 py-2 font-sans text-ink"

/*
 * The action bar and the two inline forms it opens: the summary-pair editor
 * (both languages saved together) and the unpublishing note. The parent owns the
 * requests; this component owns only what the reviewer is typing.
 */
export function ReviewActions({
	report,
	busy,
	onSave,
	onPublish,
	onUnpublish,
	onDelete,
}: {
	report: ReportDetail
	busy: boolean
	onSave: (aiSummaryEn: string, aiSummaryFr: string, sourceEn: SummarySource, sourceFr: SummarySource) =>
		Promise<boolean>
	onPublish: () => void
	onUnpublish: (note: string) =>
		Promise<boolean>
	onDelete: () => void
}) {
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

	if (mode === "edit") {
		return (
			<form
				aria-label={t("reports.edit.label")}
				className="mt-4 flex flex-col gap-4 rounded border border-rule bg-surface p-4"
				onSubmit={(event) => {
					event.preventDefault()
					void save()
				}}
			>
				<label className="block font-sans text-sm text-ink">
					{t("reports.edit.en")}
					<textarea lang="en-CA" rows={6} className={FIELD} value={draft.en} onChange={(e) => type("en", e.target.value)} />
				</label>
				<label className="block font-sans text-sm text-ink">
					{t("reports.edit.fr")}
					<textarea lang="fr-CA" rows={6} className={FIELD} value={draft.fr} onChange={(e) => type("fr", e.target.value)} />
				</label>
				{(changed("en") || changed("fr")) && (
					<div role="group" aria-label={t("reports.translate.label")} className="flex flex-wrap gap-3">
						{changed("en") && (
							<button type="button" className={SECONDARY} disabled={busy || translating} onClick={() => void translateFrom("en")}>
								{t("reports.translate.toFrench")}
							</button>
						)}
						{changed("fr") && (
							<button type="button" className={SECONDARY} disabled={busy || translating} onClick={() => void translateFrom("fr")}>
								{t("reports.translate.toEnglish")}
							</button>
						)}
					</div>
				)}
				{translateError && (
					<p role="alert" className="rounded border border-brand-700 bg-surface-2 p-3 font-sans text-sm text-ink">
						{translateError}
					</p>
				)}
				{proposal && (
					<TranslateConfirmDialog
						target={proposal.target}
						current={draft[proposal.target]}
						proposed={proposal.text}
						onAccept={() => accept(proposal.target, proposal.text)}
						onKeep={() => setProposal(null)}
					/>
				)}
				<p className="font-sans text-sm text-ink-muted">{t("reports.edit.clearsApproval")}</p>
				<div className="flex flex-wrap gap-3">
					<button type="submit" className={PRIMARY} disabled={busy || !draft.en.trim() || !draft.fr.trim()}>
						{t("reports.edit.save")}
					</button>
					<button type="button" className={SECONDARY} disabled={busy} onClick={() => setMode("view")}>
						{t("reports.action.cancel")}
					</button>
				</div>
			</form>
		)
	}

	if (mode === "unpublish") {
		return (
			<form
				aria-label={t("reports.unpublish.label")}
				className="mt-4 flex flex-col gap-4 rounded border border-rule bg-surface p-4"
				onSubmit={(event) => {
					event.preventDefault()
					void unpublish()
				}}
			>
				<label className="block font-sans text-sm text-ink">
					{t("reports.unpublish.note")}
					<textarea rows={3} maxLength={2000} className={FIELD} value={note} onChange={(e) => setNote(e.target.value)} />
				</label>
				<div className="flex flex-wrap gap-3">
					<button type="submit" className={PRIMARY} disabled={busy}>
						{t("reports.unpublish.confirm")}
					</button>
					<button type="button" className={SECONDARY} disabled={busy} onClick={() => setMode("view")}>
						{t("reports.action.cancel")}
					</button>
				</div>
			</form>
		)
	}

	const actions = actionsFor(report)

	return (
		<div className="mt-4 flex flex-col gap-2">
			{actions.includes("publish") && <p className="font-sans text-sm text-ink-muted">{t("reports.publish.hint")}</p>}
			{report.consent !== "yes" && <p className="font-sans text-sm text-ink-muted">{t("reports.private.hint")}</p>}
			<div role="group" aria-label={t("reports.action.label")} className="flex flex-wrap gap-3">
				{actions.includes("edit") && (
					<button type="button" className={SECONDARY} disabled={busy} onClick={openEditor}>
						{t("reports.action.edit")}
					</button>
				)}
				{actions.includes("write") && (
					<button type="button" className={PRIMARY} disabled={busy} onClick={openEditor}>
						{t("reports.action.write")}
					</button>
				)}
				{actions.includes("publish") && (
					<button type="button" className={PRIMARY} disabled={busy} onClick={onPublish}>
						{t("reports.action.publish")}
					</button>
				)}
				{actions.includes("unpublish") && (
					<button type="button" className={SECONDARY} disabled={busy} onClick={() => setMode("unpublish")}>
						{t("reports.action.unpublish")}
					</button>
				)}
				<button type="button" className={SECONDARY} disabled={busy} onClick={onDelete}>
					{t("reports.action.delete")}
				</button>
			</div>
		</div>
	)
}
