import { useState } from "react"
import { useLocale } from "../i18n/useLocale"
import {
	ApiError,
	NO_ANSWER_TYPES,
	OPTION_TYPES,
	QUESTION_TYPES,
	translate,
	type OptionSetView,
	type QuestionType,
	type QuestionView,
	type SaveQuestionRequest,
} from "../api/adminQuestions"
import type { ImportedQuestionDraftView } from "../api/adminTypeformImport"

/*
 * The authoring form for one question.
 *
 * Both official languages are authored here, together, because a revision is
 * born complete — there is no partially translated question in the database,
 * and Save stays disabled until both languages are present.
 *
 * Translate fills the empty side from the filled one. It is a drafting aid, not
 * a pipeline: the result lands in an ordinary editable field, the administrator
 * corrects it, and what they save is theirs. Nothing records that a machine
 * suggested it, because the person who pressed Save is accountable for the
 * wording either way (ADR-0062). The call goes to our own API — the credential
 * never reaches this page.
 */

export interface QuestionDraft {
	request: SaveQuestionRequest
}

export function blankDraft(): QuestionDraft {
	return {
		request: {
			key: "",
			type: "short_text",
			labelEn: "",
			labelFr: "",
			helpTextEn: null,
			helpTextFr: null,
			placeholderEn: null,
			placeholderFr: null,
			isRequired: false,
			// Private by default: a question whose answers could identify
			// someone is the safe assumption, and an administrator opts out
			// deliberately. See ADR-0038.
			isPrivate: true,
			isActive: true,
			dependsOnQuestionId: null,
			dependsOnOptionCode: null,
			optionSetId: null,
			groupedUnderQuestionId: null,
			allowsReporterAdditions: false,
			options: [],
		},
	}
}

export function draftOf(question: QuestionView): QuestionDraft {
	return {
		request: {
			key: question.key,
			type: question.type,
			labelEn: question.labelEn,
			labelFr: question.labelFr,
			helpTextEn: question.helpTextEn,
			helpTextFr: question.helpTextFr,
			placeholderEn: question.placeholderEn,
			placeholderFr: question.placeholderFr,
			isRequired: question.isRequired,
			isPrivate: question.isPrivate,
			isActive: question.isActive,
			dependsOnQuestionId: question.dependsOnQuestionId,
			dependsOnOptionCode: question.dependsOnOptionCode,
			optionSetId: question.optionSetId,
			groupedUnderQuestionId: question.groupedUnderQuestionId,
			allowsReporterAdditions: question.allowsReporterAdditions,
			options: question.options.map((option) => ({
				code: option.code,
				labelEn: option.labelEn,
				labelFr: option.labelFr,
			})),
		},
	}
}

/**
 * An imported field references its group parent by Typeform key, not a
 * database id — the parent may not exist yet if it hasn't been reviewed and
 * saved. Resolved against the live question list at the moment the draft is
 * opened for review.
 */
export function draftFromImported(imported: ImportedQuestionDraftView, questions: QuestionView[]): QuestionDraft {
	const group = imported.groupedUnderKey
		? questions.find((question) => question.key === imported.groupedUnderKey)
		: undefined
	const dependsOn = imported.dependsOnKey
		? questions.find((question) => question.key === imported.dependsOnKey)
		: undefined

	return {
		request: {
			key: imported.key,
			type: imported.type,
			labelEn: imported.labelEn,
			labelFr: imported.labelFr,
			helpTextEn: imported.helpTextEn,
			helpTextFr: imported.helpTextFr,
			placeholderEn: null,
			placeholderFr: null,
			isRequired: imported.isRequired,
			isPrivate: imported.isPrivate,
			isActive: true,
			dependsOnQuestionId: dependsOn?.id ?? null,
			dependsOnOptionCode: dependsOn ? imported.dependsOnOptionCode : null,
			optionSetId: null,
			groupedUnderQuestionId: group?.id ?? null,
			allowsReporterAdditions: imported.allowsReporterAdditions,
			options: imported.options.map((option) => ({
				code: option.code,
				labelEn: option.labelEn,
				labelFr: option.labelFr,
			})),
		},
	}
}

const fieldClassName =
	"mt-1 w-full rounded border border-rule bg-surface px-3 py-2 font-sans text-ink placeholder:text-ink-muted"

const labelClassName = "block font-sans text-sm font-medium text-ink"

export function QuestionEditor({
	draft,
	optionSets,
	conditionQuestions,
	groupQuestions,
	isEditing,
	hasBeenAnswered,
	translationAvailable,
	translationIsStandIn,
	onChange,
	onCancel,
	onSave,
}: {
	draft: QuestionDraft
	optionSets: OptionSetView[]
	conditionQuestions: QuestionView[]
	groupQuestions: QuestionView[]
	isEditing: boolean
	hasBeenAnswered: boolean
	translationAvailable: boolean
	translationIsStandIn: boolean
	onChange: (draft: QuestionDraft) => void
	onCancel: () => void
	onSave: (draft: QuestionDraft) => void
}) {
	const { t } = useLocale()
	const request = draft.request
	const takesOptions = OPTION_TYPES.includes(request.type)
	// A statement or a group collects no answer, so it can be neither required,
	// private, a conditional child, nor a conditional parent (ADR-0076).
	const collectsNoAnswer = NO_ANSWER_TYPES.includes(request.type)
	const dependsOnParent = conditionQuestions.find((question) => question.id === request.dependsOnQuestionId)

	const [translating, setTranslating] = useState(false)
	const [translationError, setTranslationError] = useState<string | null>(null)

	const hasEnglish = request.labelEn.trim().length > 0
	const hasFrench = request.labelFr.trim().length > 0

	// A question is stored as one complete bilingual revision, so a half-written
	// one cannot be saved at all. Translate fills the empty side; the
	// administrator still edits and saves it deliberately (ADR-0062). A
	// single-select condition additionally needs its required option named,
	// or the API rejects the save (ADR-0074).
	const canSave =
		hasEnglish && hasFrench && (dependsOnParent?.type !== "single_select" || request.dependsOnOptionCode !== null)
	const translationDirection = hasEnglish && !hasFrench ? "toFrench" : !hasEnglish && hasFrench ? "toEnglish" : null

	function update(changes: Partial<SaveQuestionRequest>) {
		onChange({ request: { ...request, ...changes } })
	}

	async function translateMissingLanguage() {
		if (!translationDirection) return

		const toFrench = translationDirection === "toFrench"
		const from = toFrench ? "en-CA" : "fr-CA"
		const to = toFrench ? "fr-CA" : "en-CA"

		setTranslating(true)
		setTranslationError(null)

		try {
			// Label, help text, placeholder, and every option label in one
			// request rather than one per field.
			const source = toFrench
				? [request.labelEn, request.helpTextEn ?? "", request.placeholderEn ?? "", ...request.options.map((o) => o.labelEn)]
				: [request.labelFr, request.helpTextFr ?? "", request.placeholderFr ?? "", ...request.options.map((o) => o.labelFr)]

			const { texts } = await translate(source, from, to)
			const [label, help, placeholder, ...optionLabels] = texts

			const options = request.options.map((option, index) =>
				toFrench
					? { ...option, labelFr: optionLabels[index] ?? option.labelFr }
					: { ...option, labelEn: optionLabels[index] ?? option.labelEn },
			)

			update(
				toFrench
					? {
							labelFr: label,
							helpTextFr: help || null,
							placeholderFr: placeholder || null,
							options,
						}
					: {
							labelEn: label,
							helpTextEn: help || null,
							placeholderEn: placeholder || null,
							options,
						},
			)
		} catch (cause) {
			setTranslationError(cause instanceof ApiError ? cause.detail : t("questions.translate.failed"))
		} finally {
			setTranslating(false)
		}
	}

	function updateOption(index: number, changes: Partial<{ labelEn: string; labelFr: string }>) {
		const options = request.options.map((option, current) => (current === index ? { ...option, ...changes } : option))
		update({ options })
	}

	return (
		<form
			className="flex flex-col gap-5 rounded border border-rule bg-surface-2 p-6"
			onSubmit={(event) => {
				event.preventDefault()
				onSave(draft)
			}}
		>
			<h2 className="font-display text-xl font-bold">
				{isEditing ? t("questions.editorTitleEdit") : t("questions.editorTitleNew")}
			</h2>

			{isEditing && hasBeenAnswered && (
				<p role="status" className="rounded border border-rule bg-surface-2 p-4 font-sans text-sm text-ink">
					{t("questions.forkWarning")}
				</p>
			)}

			<div className="grid gap-4 sm:grid-cols-2">
				<div>
					<label className={labelClassName} htmlFor="question-key">
						{t("questions.field.key")}
					</label>
					<input
						id="question-key"
						className={fieldClassName}
						value={request.key ?? ""}
						readOnly={isEditing}
						required
						onChange={(event) => update({ key: event.target.value })}
					/>
					<p className="mt-1 font-sans text-xs text-ink-muted">{t("questions.field.keyHelp")}</p>
				</div>

				<div>
					<label className={labelClassName} htmlFor="question-type">
						{t("questions.field.type")}
					</label>
					<select
						id="question-type"
						className={fieldClassName}
						value={request.type}
						onChange={(event) => {
							const type = event.target.value as QuestionType
							// Options and a shared list only mean something for the
							// types that take them; carrying them across a retype
							// would save choices the question no longer offers.
							const clearedOptions = OPTION_TYPES.includes(type) ? {} : { options: [], optionSetId: null }
							// A statement or a group collects no answer, so it cannot
							// be required, private, or conditional on anything
							// (ADR-0076).
							const clearedForNoAnswer = NO_ANSWER_TYPES.includes(type)
								? { isRequired: false, isPrivate: false, dependsOnQuestionId: null, dependsOnOptionCode: null }
								: {}
							// Only multi-select is author-controlled; autocomplete is
							// always on regardless of what is sent, and every other
							// type rejects the flag outright (ADR-0063, ADR-0077).
							const clearedReporterAdditions =
								type === "multi_select" ? {} : { allowsReporterAdditions: false }
							update({ type, ...clearedOptions, ...clearedForNoAnswer, ...clearedReporterAdditions })
						}}
					>
						{QUESTION_TYPES.map((type) => (
							<option key={type} value={type}>
								{t(`questions.type.${type}`)}
							</option>
						))}
					</select>
				</div>

				<div>
					<label className={labelClassName} htmlFor="question-label-en">
						{t("questions.field.labelEn")}
					</label>
					<input
						id="question-label-en"
						className={fieldClassName}
						value={request.labelEn}
						required
						onChange={(event) => update({ labelEn: event.target.value })}
					/>
				</div>

				<div>
					<label className={labelClassName} htmlFor="question-label-fr">
						{t("questions.field.labelFr")}
					</label>
					<input
						id="question-label-fr"
						className={fieldClassName}
						value={request.labelFr}
						required
						onChange={(event) => update({ labelFr: event.target.value })}
					/>
				</div>

				<div>
					<label className={labelClassName} htmlFor="question-help-en">
						{t("questions.field.helpEn")}
					</label>
					<input
						id="question-help-en"
						className={fieldClassName}
						value={request.helpTextEn ?? ""}
						onChange={(event) => update({ helpTextEn: event.target.value || null })}
					/>
				</div>

				<div>
					<label className={labelClassName} htmlFor="question-help-fr">
						{t("questions.field.helpFr")}
					</label>
					<input
						id="question-help-fr"
						className={fieldClassName}
						value={request.helpTextFr ?? ""}
						onChange={(event) => update({ helpTextFr: event.target.value || null })}
					/>
				</div>
			</div>

			<div className="flex flex-wrap items-center gap-3">
				<button
					type="button"
					className="touch-target inline-flex items-center rounded border border-rule px-4 font-sans text-sm text-ink hover:bg-surface disabled:opacity-40"
					disabled={!translationAvailable || translationDirection === null || translating}
					onClick={() => void translateMissingLanguage()}
				>
					{translating ? t("questions.translate.working") : t("questions.translate.action")}
				</button>

				<p className="font-sans text-xs text-ink-muted">
					{!translationAvailable
						? t("questions.translate.unavailable")
						: translationIsStandIn
							? t("questions.translate.standIn")
							: translationDirection === null
								? t("questions.translate.hint")
								: t("questions.translate.draftWarning")}
				</p>
			</div>

			{translationError && (
				<p role="alert" className="font-sans text-sm text-ink">
					{translationError}
				</p>
			)}

			<fieldset className="flex flex-wrap gap-6">
				<legend className="font-sans text-sm font-medium text-ink">{t("questions.field.behaviour")}</legend>

				{!collectsNoAnswer && (
					<>
						<label className="flex items-center gap-2 font-sans text-sm text-ink">
							<input
								type="checkbox"
								checked={request.isRequired}
								onChange={(event) => update({ isRequired: event.target.checked })}
							/>
							{t("questions.field.required")}
						</label>

						<label className="flex items-center gap-2 font-sans text-sm text-ink">
							<input
								type="checkbox"
								checked={request.isPrivate}
								onChange={(event) => update({ isPrivate: event.target.checked })}
							/>
							{t("questions.field.private")}
						</label>
					</>
				)}

				<label className="flex items-center gap-2 font-sans text-sm text-ink">
					<input
						type="checkbox"
						checked={request.isActive}
						onChange={(event) => update({ isActive: event.target.checked })}
					/>
					{t("questions.field.active")}
				</label>
			</fieldset>

			{!collectsNoAnswer && <p className="font-sans text-xs text-ink-muted">{t("questions.field.privateHelp")}</p>}

			{!collectsNoAnswer && (
				<div>
					<label className={labelClassName} htmlFor="question-depends-on">
						{t("questions.field.dependsOn")}
					</label>
					<select
						id="question-depends-on"
						className={fieldClassName}
						value={request.dependsOnQuestionId ?? ""}
						onChange={(event) =>
							update({ dependsOnQuestionId: event.target.value || null, dependsOnOptionCode: null })
						}
					>
						<option value="">{t("questions.field.dependsOnNone")}</option>
						{conditionQuestions.map((question) => (
							<option key={question.id} value={question.id}>
								{question.labelEn}
							</option>
						))}
					</select>
					<p className="mt-1 font-sans text-xs text-ink-muted">{t("questions.field.dependsOnHelp")}</p>

					{dependsOnParent?.type === "single_select" && (
						<div className="mt-3">
							<label className={labelClassName} htmlFor="question-depends-on-option">
								{t("questions.field.dependsOnOption")}
							</label>
							<select
								id="question-depends-on-option"
								className={fieldClassName}
								value={request.dependsOnOptionCode ?? ""}
								required
								onChange={(event) => update({ dependsOnOptionCode: event.target.value || null })}
							>
								<option value="">{t("questions.field.dependsOnOptionNone")}</option>
								{dependsOnParent.options.map((option) => (
									<option key={option.code} value={option.code}>
										{option.labelEn}
									</option>
								))}
							</select>
							<p className="mt-1 font-sans text-xs text-ink-muted">{t("questions.field.dependsOnOptionHelp")}</p>
						</div>
					)}
				</div>
			)}

			{request.type !== "group" && (
				<div>
					<label className={labelClassName} htmlFor="question-grouped-under">
						{t("questions.field.groupedUnder")}
					</label>
					<select
						id="question-grouped-under"
						className={fieldClassName}
						value={request.groupedUnderQuestionId ?? ""}
						onChange={(event) => update({ groupedUnderQuestionId: event.target.value || null })}
					>
						<option value="">{t("questions.field.groupedUnderNone")}</option>
						{groupQuestions.map((question) => (
							<option key={question.id} value={question.id}>
								{question.labelEn}
							</option>
						))}
					</select>
					<p className="mt-1 font-sans text-xs text-ink-muted">{t("questions.field.groupedUnderHelp")}</p>
				</div>
			)}

			{takesOptions && (
				<div className="flex flex-col gap-3 rounded border border-rule bg-surface p-4">
					<label className={labelClassName} htmlFor="question-option-set">
						{t("questions.field.optionSet")}
					</label>
					<select
						id="question-option-set"
						className={fieldClassName}
						value={request.optionSetId ?? ""}
						onChange={(event) => update({ optionSetId: event.target.value || null })}
					>
						<option value="">{t("questions.field.optionSetNone")}</option>
						{optionSets.map((set) => (
							<option key={set.id} value={set.id}>
								{set.nameEn}
							</option>
						))}
					</select>
					<p className="font-sans text-xs text-ink-muted">{t("questions.field.optionSetHelp")}</p>

					{request.type === "multi_select" && (
						<div>
							<label className="flex items-center gap-2 font-sans text-sm text-ink">
								<input
									type="checkbox"
									checked={request.allowsReporterAdditions}
									onChange={(event) => update({ allowsReporterAdditions: event.target.checked })}
								/>
								{t("questions.field.allowsReporterAdditions")}
							</label>
							<p className="mt-1 font-sans text-xs text-ink-muted">
								{t("questions.field.allowsReporterAdditionsHelp")}
							</p>
						</div>
					)}

					{!request.optionSetId && (
						<>
							<h3 className="font-sans text-sm font-medium text-ink">{t("questions.field.options")}</h3>

							{request.options.map((option, index) => (
								<div key={index} className="grid gap-2 sm:grid-cols-2">
									<input
										className={fieldClassName}
										value={option.labelEn}
										required
										aria-label={t("questions.field.optionLabelEn")}
										placeholder={t("questions.field.optionLabelEn")}
										onChange={(event) => updateOption(index, { labelEn: event.target.value })}
									/>
									<div className="flex gap-2">
										<input
											className={fieldClassName}
											value={option.labelFr}
											required
											aria-label={t("questions.field.optionLabelFr")}
											placeholder={t("questions.field.optionLabelFr")}
											onChange={(event) => updateOption(index, { labelFr: event.target.value })}
										/>
										<button
											type="button"
											className="touch-target rounded border border-rule px-3 font-sans text-sm text-ink hover:bg-surface-2"
											aria-label={t("questions.field.removeOption")}
											onClick={() =>
												update({ options: request.options.filter((_, current) => current !== index) })
											}
										>
											×
										</button>
									</div>
								</div>
							))}

							<button
								type="button"
								className="touch-target self-start rounded border border-rule px-4 font-sans text-sm text-ink hover:bg-surface-2"
								onClick={() =>
									update({ options: [...request.options, { code: null, labelEn: "", labelFr: "" }] })
								}
							>
								{t("questions.field.addOption")}
							</button>
						</>
					)}
				</div>
			)}

			<div className="flex gap-3">
				<button
					type="submit"
					className="touch-target inline-flex items-center rounded bg-brand-700 px-5 font-sans font-medium text-ink-inverse hover:bg-brand-600 disabled:opacity-40 disabled:hover:bg-brand-700"
					disabled={!canSave}
				>
					{t("questions.save")}
				</button>
				<button
					type="button"
					className="touch-target inline-flex items-center rounded border border-rule px-5 font-sans text-ink hover:bg-surface-2"
					onClick={onCancel}
				>
					{t("questions.cancel")}
				</button>
			</div>
		</form>
	)
}
