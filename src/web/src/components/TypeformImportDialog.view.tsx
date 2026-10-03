import { useLocale } from "../i18n/useLocale"
import { labelWithColon } from "../lib/questionPrompt"
import type {
	ImportedQuestionDraftView,
	PendingImportLogicView,
	RejectedTypeformFieldView,
} from "./typeformImportTypes"

const fieldClassName =
	"mt-1 w-full rounded border border-rule bg-surface px-3 py-2 font-sans text-ink placeholder:text-ink-muted"

const rowButtonClassName =
	"touch-target inline-flex items-center rounded border border-rule px-3 font-sans text-sm text-ink hover:bg-surface-2"

export interface TypeformImportDialogViewProps {
	onClose: () => void
	english: File | null
	french: File | null
	importing: boolean
	error: string | null
	drafts: ImportedQuestionDraftView[]
	rejected: RejectedTypeformFieldView[]
	pendingLogic: PendingImportLogicView[]
	reviewedKeys: Set<string>
	onEnglishChange: (file: File | null) => void
	onFrenchChange: (file: File | null) => void
	runImport: () => Promise<void>
	removePendingLogic: (id: string) => Promise<void>
	reviewDraft: (draft: ImportedQuestionDraftView) => void
}

export function TypeformImportDialogView({
	onClose,
	english,
	french,
	importing,
	error,
	drafts,
	rejected,
	pendingLogic,
	reviewedKeys,
	onEnglishChange,
	onFrenchChange,
	runImport,
	removePendingLogic,
	reviewDraft,
}: TypeformImportDialogViewProps) {
	const { t } = useLocale()

	return (
		<div className="mt-6 flex flex-col gap-5 rounded border border-rule bg-surface-2 p-6">
			<h2 className="font-display text-xl font-bold">{t("questions.import.title")}</h2>
			<p className="font-sans text-sm text-ink-muted">{t("questions.import.intro")}</p>

			{error && (
				<p role="alert" className="rounded border border-brand-700 bg-surface p-4 font-sans text-ink">
					{error}
				</p>
			)}

			<div className="grid gap-4 sm:grid-cols-2">
				<div>
					<label className="block font-sans text-sm font-medium text-ink" htmlFor="typeform-import-english">
						{t("questions.import.englishFile")}
					</label>
					<input
						id="typeform-import-english"
						type="file"
						accept="application/json"
						className={fieldClassName}
						onChange={(event) => onEnglishChange(event.target.files?.[0] ?? null)}
					/>
				</div>

				<div>
					<label className="block font-sans text-sm font-medium text-ink" htmlFor="typeform-import-french">
						{t("questions.import.frenchFile")}
					</label>
					<input
						id="typeform-import-french"
						type="file"
						accept="application/json"
						className={fieldClassName}
						onChange={(event) => onFrenchChange(event.target.files?.[0] ?? null)}
					/>
				</div>
			</div>

			<div className="flex gap-3">
				<button
					type="button"
					className="touch-target inline-flex items-center rounded bg-brand-700 px-5 font-sans font-medium text-ink-inverse hover:bg-brand-600 disabled:opacity-40"
					disabled={!english || !french || importing}
					onClick={() => void runImport()}
				>
					{importing ? t("questions.import.working") : t("questions.import.action")}
				</button>
				<button
					type="button"
					className="touch-target inline-flex items-center rounded border border-rule px-5 font-sans text-ink hover:bg-surface"
					onClick={onClose}
				>
					{t("questions.cancel")}
				</button>
			</div>

			{drafts.length > 0 && (
				<div>
					<h3 className="font-display text-lg font-bold">{t("questions.import.draftsTitle")}</h3>
					<ul className="mt-3 flex flex-col gap-2">
						{drafts.map((draft) => (
							<li
								key={draft.key}
								className="flex flex-wrap items-center justify-between gap-3 rounded border border-rule bg-surface p-3"
							>
								<div className="min-w-0">
									<p className="font-sans font-medium text-ink" lang="en-CA">
										{labelWithColon(draft.labelEn, draft.type, "en-CA")}
									</p>
									<p className="font-sans text-sm text-ink-muted" lang="fr-CA">
										{labelWithColon(draft.labelFr, draft.type, "fr-CA")}
									</p>
									{draft.frenchDefaultedToEnglish && (
										<p className="mt-1 font-sans text-xs text-ink-muted">
											{t("questions.import.frenchDefaultedToEnglish")}
										</p>
									)}
								</div>
								<button
									type="button"
									className={rowButtonClassName}
									onClick={() => reviewDraft(draft)}
								>
									{reviewedKeys.has(draft.key) ? t("questions.import.reviewed") : t("questions.import.review")}
								</button>
							</li>
						))}
					</ul>
				</div>
			)}

			{rejected.length > 0 && (
				<div>
					<h3 className="font-display text-lg font-bold">{t("questions.import.rejectedTitle")}</h3>
					<p className="mt-1 font-sans text-sm text-ink-muted">{t("questions.import.rejectedIntro")}</p>
					<ul className="mt-3 flex flex-col gap-2">
						{rejected.map((field) => (
							<li key={field.ref} className="rounded border border-rule bg-surface p-3">
								<p className="font-sans font-medium text-ink">{field.title}</p>
								<p className="font-sans text-xs text-ink-muted">{field.typeformType}</p>
							</li>
						))}
					</ul>
				</div>
			)}

			{pendingLogic.length > 0 && (
				<div>
					<h3 className="font-display text-lg font-bold">{t("questions.import.pendingLogicTitle")}</h3>
					<p className="mt-1 font-sans text-sm text-ink-muted">{t("questions.import.pendingLogicIntro")}</p>
					<ul className="mt-3 flex flex-col gap-2">
						{pendingLogic.map((note) => (
							<li
								key={note.id}
								className="flex flex-wrap items-center justify-between gap-3 rounded border border-rule bg-surface p-3"
							>
								<p className="min-w-0 font-sans text-sm text-ink">{note.fieldTitle}</p>
								<button type="button" className={rowButtonClassName} onClick={() => void removePendingLogic(note.id)}>
									{t("questions.import.pendingLogicResolve")}
								</button>
							</li>
						))}
					</ul>
				</div>
			)}
		</div>
	)
}
