import { useEffect, useId, useRef, useState } from "react"
import { useLocale } from "../i18n/useLocale"
import {
	ApiError,
	NO_ANSWER_TYPES,
	OPTION_TYPES,
	TRANSLATABLE_TYPES,
	translatableByDefault,
	QUESTION_TYPES,
	translate,
	type OptionInput,
	type QuestionType,
	type QuestionView,
	type SaveQuestionRequest,
} from "../api/adminQuestions"
import type { ImportedQuestionDraftView } from "../api/adminTypeformImport"
import type { Locale } from "../i18n/locales"
import { sortChoices } from "../lib/sortChoices"
import {
	DEFAULT_TRANSLATION_DIRECTION,
	TranslationDirectionSwitch,
	translationLocales,
	type TranslationDirection,
} from "./TranslationDirectionSwitch"

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
 *
 * Each choice is translated on its own, when the administrator asks. The
 * direction switch at the top of the Choices panel says which way every
 * choice's Translate goes, English to French by default. A choice's Translate
 * is offered only once its source wording has been edited — since the editor
 * opened, or since that choice was last translated — and it replaces the other
 * language with an editable draft. Nothing is saved until Save (ADR-0141).
 *
 * The choices are the question's own and are saved in place: editing them never
 * creates a new version, and a choice a reporter typed into a type-ahead is
 * marked here, where an administrator curates it (ADR-0095). Such a choice may
 * still be missing one language; it is the only one allowed to be.
 */

export interface QuestionDraft {
	request: SaveQuestionRequest
}

export function blankDraft(): QuestionDraft {
	return {
		request: {
			// No key: the API derives one from the English wording, and an
			// administrator never sees or chooses it.
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
			isTranslatable: translatableByDefault("short_text"),
			allowFutureDates: false,
			isActive: true,
			dependsOnQuestionId: null,
			dependsOnChoiceId: null,
			groupedUnderQuestionId: null,
			options: [],
		},
	}
}

/** The wording a reader in `locale` sees for a choice: their language, or the one it has. */
function choiceLabel(option: { labelEn: string | null; labelFr: string | null }, locale: Locale): string {
	return (locale === "fr-CA" ? (option.labelFr || option.labelEn) : (option.labelEn || option.labelFr)) ?? ""
}

/** The positions an option can take (ADR-0136). */
const PINS = ["none", "first", "last"] as const

/**
 * The editor's draft of a question. Its options are listed as the form lists
 * them — pinned first, then alphabetically in `locale`, then pinned last — once,
 * here, when the question is opened, and never re-sorted while the
 * administrator edits (ADR-0136).
 */
export function draftOf(question: QuestionView, locale: Locale): QuestionDraft {
	return {
		request: {
			type: question.type,
			labelEn: question.labelEn,
			labelFr: question.labelFr,
			helpTextEn: question.helpTextEn,
			helpTextFr: question.helpTextFr,
			placeholderEn: question.placeholderEn,
			placeholderFr: question.placeholderFr,
			isRequired: question.isRequired,
			isPrivate: question.isPrivate,
			isTranslatable: question.isTranslatable,
			allowFutureDates: question.allowFutureDates,
			isActive: question.isActive,
			dependsOnQuestionId: question.dependsOnQuestionId,
			dependsOnChoiceId: question.dependsOnChoiceId,
			groupedUnderQuestionId: question.groupedUnderQuestionId,
			options: sortChoices(question.options, locale, (option) => choiceLabel(option, locale)).map((option) => ({
				code: option.code,
				// A reporter-added choice may be missing one language; the field
				// shows empty and the server keeps it missing until it is filled.
				labelEn: option.labelEn ?? "",
				labelFr: option.labelFr ?? "",
				addedByReporter: option.addedByReporter,
				pin: option.pin,
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
			isTranslatable: translatableByDefault(imported.type),
			// Typeform cannot express it, so an import never carries it (ADR-0138).
			allowFutureDates: false,
			isActive: true,
			dependsOnQuestionId: dependsOn?.id ?? null,
			// The Typeform file names the required option by code; the editor names
			// the parent's choice by ID (ADR-0128).
			dependsOnChoiceId: dependsOn?.options.find((option) => option.code === imported.dependsOnOptionCode)?.id ?? null,
			groupedUnderQuestionId: group?.id ?? null,
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

// The field a choice is translated from: marked just enough that the direction
// reads at a glance.
const sourceFieldClassName = fieldClassName.replace("border-rule", "border-ink-muted")

/**
 * What the editor remembers about one choice row, beside the draft: a stable
 * identity (a new choice has no code until it is saved), the wording its
 * Translate compares against, and that row's own request state.
 */
interface ChoiceRow {
	key: number
	baselineEn: string
	baselineFr: string
	pending: boolean
	error: string | null
}

let nextChoiceRowKey = 0

function choiceRow(option: Pick<OptionInput, "labelEn" | "labelFr">): ChoiceRow {
	nextChoiceRowKey += 1
	return { key: nextChoiceRowKey, baselineEn: option.labelEn, baselineFr: option.labelFr, pending: false, error: null }
}

function sourceOf(option: Pick<OptionInput, "labelEn" | "labelFr">, direction: TranslationDirection): string {
	return direction === "toFrench" ? option.labelEn : option.labelFr
}

/**
 * A choice's Translate is offered once its source wording has text and differs
 * from what it was when the editor opened, or when the choice was last
 * translated. A new choice starts blank, so it is dirty as soon as it is typed in.
 */
function isDirty(option: OptionInput, row: ChoiceRow, direction: TranslationDirection): boolean {
	const source = sourceOf(option, direction)
	const baseline = direction === "toFrench" ? row.baselineEn : row.baselineFr
	return source.trim().length > 0 && source !== baseline
}

function TranslateIcon() {
	return (
		<svg viewBox="0 0 24 24" width="18" height="18" fill="currentColor" aria-hidden="true">
			<path d="M12.87 15.07l-2.54-2.51.03-.03A17.52 17.52 0 0 0 14.07 6H17V4h-7V2H8v2H1v2h11.17C11.5 7.92 10.44 9.75 9 11.35 8.07 10.32 7.3 9.19 6.69 8h-2c.73 1.63 1.73 3.17 2.98 4.56l-5.09 5.02L4 19l5-5 3.11 3.11.76-2.04zM18.5 10h-2L12 22h2l1.12-3h4.75L21 22h2l-4.5-12zm-2.62 7l1.62-4.33L19.12 17h-3.24z" />
		</svg>
	)
}

/**
 * The wording fields' labels for a type. A statement is instructional text: a
 * title and a description, not a question and help text. Both still save to
 * the revision's label and help-text fields (REQ-QB-141).
 */
function wordingLabels(type: QuestionType) {
	return type === "statement"
		? { labelEn: "titleEn", labelFr: "titleFr", helpEn: "descriptionEn", helpFr: "descriptionFr" }
		: { labelEn: "labelEn", labelFr: "labelFr", helpEn: "helpEn", helpFr: "helpFr" }
}

export function QuestionEditor({
	draft,
	conditionQuestions,
	groupQuestions,
	isEditing,
	hasBeenAnswered,
	translationAvailable,
	onChange,
	onCancel,
	onSave,
}: {
	draft: QuestionDraft
	conditionQuestions: QuestionView[]
	groupQuestions: QuestionView[]
	isEditing: boolean
	hasBeenAnswered: boolean
	translationAvailable: boolean
	/** A new draft, or a function of the current one for a change that lands after a request. */
	onChange: (change: QuestionDraft | ((current: QuestionDraft) => QuestionDraft)) => void
	onCancel: () => void
	onSave: (draft: QuestionDraft) => void
}) {
	const { t, locale } = useLocale()
	const request = draft.request
	const takesOptions = OPTION_TYPES.includes(request.type)
	// Only a picker option is replaced; a type-ahead value is corrected in place
	// (ADR-0128, ADR-0129).
	const canReplace = request.type === "single_select" || request.type === "multi_select"
	// A statement or a group collects no answer, so it can be neither required,
	// private, a conditional child, nor a conditional parent (ADR-0076).
	const collectsNoAnswer = NO_ANSWER_TYPES.includes(request.type)
	const wording = wordingLabels(request.type)
	// A statement's description is often several paragraphs, so it gets room
	// for them; every other type's help text is one line.
	const helpTakesLines = request.type === "statement"
	const dependsOnParent = conditionQuestions.find((question) => question.id === request.dependsOnQuestionId)

	const [translating, setTranslating] = useState(false)
	const [translationError, setTranslationError] = useState<string | null>(null)
	const [direction, setDirection] = useState<TranslationDirection>(DEFAULT_TRANSLATION_DIRECTION)
	const [rows, setRows] = useState<ChoiceRow[]>(() => request.options.map(choiceRow))
	const unavailableId = useId()
	const choicesRef = useRef<HTMLDivElement>(null)
	const focusNewChoice = useRef(false)

	// Read when a choice's translation comes back, to drop a result the
	// administrator has since made stale.
	const latest = useRef({ request, rows, direction })
	latest.current = { request, rows, direction }

	// The rows follow the draft's options one for one. A draft replaced from
	// outside with a different number of choices starts its rows afresh.
	useEffect(() => {
		if (rows.length !== request.options.length) setRows(request.options.map(choiceRow))
	}, [rows.length, request.options])

	// A choice added from the panel header takes focus, so it is on screen
	// however long the list is.
	useEffect(() => {
		if (!focusNewChoice.current) return
		focusNewChoice.current = false
		const choices = choicesRef.current?.querySelectorAll<HTMLElement>('[data-testid="question-choice"]')
		choices?.[choices.length - 1]?.querySelector("input")?.focus()
	}, [request.options.length])

	const hasEnglish = request.labelEn.trim().length > 0
	const hasFrench = request.labelFr.trim().length > 0

	// A question is stored as one complete bilingual revision, so a half-written
	// one cannot be saved at all. Translate fills the empty side; the
	// administrator still edits and saves it deliberately (ADR-0062). A
	// single-select condition additionally needs its required option named,
	// or the API rejects the save (ADR-0074).
	const canSave =
		hasEnglish && hasFrench && (dependsOnParent?.type !== "single_select" || request.dependsOnChoiceId !== null)
	const translationDirection = hasEnglish && !hasFrench ? "toFrench" : !hasEnglish && hasFrench ? "toEnglish" : null

	function translationFailure(cause: unknown) {
		return cause instanceof ApiError ? cause.detail : t("questions.translate.failed")
	}

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
			setTranslationError(translationFailure(cause))
		} finally {
			setTranslating(false)
		}
	}

	function patchRow(key: number, changes: Partial<ChoiceRow>) {
		setRows((current) => current.map((row) => (row.key === key ? { ...row, ...changes } : row)))
	}

	/**
	 * Translates one choice in the chosen direction and replaces its other
	 * language with the result, as a draft. The result lands through a function
	 * of the current draft, on the row it was asked for, and is dropped if that
	 * row was removed, its source edited, or the direction flipped meanwhile.
	 */
	async function translateChoice(index: number) {
		const row = rows[index]
		const option = request.options[index]
		if (!row || !option) return

		const asked = direction
		const source = sourceOf(option, asked)
		const { from, to } = translationLocales(asked)
		patchRow(row.key, { pending: true, error: null })

		try {
			const { texts } = await translate([source], from, to)
			const drafted = texts[0] ?? ""
			const now = latest.current
			const at = now.rows.findIndex((candidate) => candidate.key === row.key)
			const current = now.request.options[at]
			if (at < 0 || !current || now.direction !== asked || sourceOf(current, asked) !== source) return

			onChange((draft) => ({
				request: {
					...draft.request,
					options: draft.request.options.map((candidate, position) =>
						position !== at || sourceOf(candidate, asked) !== source
							? candidate
							: asked === "toFrench"
								? { ...candidate, labelFr: drafted }
								: { ...candidate, labelEn: drafted },
					),
				},
			}))
			// Clean again until the source is edited.
			patchRow(
				row.key,
				asked === "toFrench" ? { baselineEn: source, baselineFr: drafted } : { baselineEn: drafted, baselineFr: source },
			)
		} catch (cause) {
			patchRow(row.key, { error: translationFailure(cause) })
		} finally {
			patchRow(row.key, { pending: false })
		}
	}

	function updateOption(index: number, changes: Partial<Pick<OptionInput, "labelEn" | "labelFr" | "replace" | "pin">>) {
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
				{/* Its own row, one column wide, so each English field sits beside its French one. */}
				<div className="sm:col-span-2 sm:w-[calc(50%-0.5rem)]">
					<label className={labelClassName} htmlFor="question-type">
						{t("questions.field.type")}
					</label>
					<select
						id="question-type"
						className={fieldClassName}
						value={request.type}
						onChange={(event) => {
							const type = event.target.value as QuestionType
							// Choices only mean something for the types that take
							// them; carrying them across a retype would save choices
							// the question no longer offers.
							const clearedOptions = OPTION_TYPES.includes(type) ? {} : { options: [] }
							if (!OPTION_TYPES.includes(type)) setRows([])
							// A statement or a group collects no answer, so it cannot
							// be required, private, or conditional on anything
							// (ADR-0076).
							const clearedForNoAnswer = NO_ANSWER_TYPES.includes(type)
								? { isRequired: false, isPrivate: false, dependsOnQuestionId: null, dependsOnChoiceId: null }
								: {}
							// A retype takes the new type's translation default: only
							// free text can need translation at all (ADR-0112).
							update({
								type,
								...clearedOptions,
								...clearedForNoAnswer,
								isTranslatable: translatableByDefault(type),
								// Only a date question may allow a future date (ADR-0138).
								allowFutureDates: false,
							})
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
						{t(`questions.field.${wording.labelEn}`)}
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
						{t(`questions.field.${wording.labelFr}`)}
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
						{t(`questions.field.${wording.helpEn}`)}
					</label>
					{helpTakesLines ? (
						<textarea
							id="question-help-en"
							className={`${fieldClassName} resize-y`}
							rows={6}
							value={request.helpTextEn ?? ""}
							onChange={(event) => update({ helpTextEn: event.target.value || null })}
						/>
					) : (
						<input
							id="question-help-en"
							className={fieldClassName}
							value={request.helpTextEn ?? ""}
							onChange={(event) => update({ helpTextEn: event.target.value || null })}
						/>
					)}
				</div>

				<div>
					<label className={labelClassName} htmlFor="question-help-fr">
						{t(`questions.field.${wording.helpFr}`)}
					</label>
					{helpTakesLines ? (
						<textarea
							id="question-help-fr"
							className={`${fieldClassName} resize-y`}
							rows={6}
							value={request.helpTextFr ?? ""}
							onChange={(event) => update({ helpTextFr: event.target.value || null })}
						/>
					) : (
						<input
							id="question-help-fr"
							className={fieldClassName}
							value={request.helpTextFr ?? ""}
							onChange={(event) => update({ helpTextFr: event.target.value || null })}
						/>
					)}
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

				{TRANSLATABLE_TYPES.includes(request.type) && (
					<label className="flex items-center gap-2 font-sans text-sm text-ink">
						<input
							type="checkbox"
							checked={request.isTranslatable}
							onChange={(event) => update({ isTranslatable: event.target.checked })}
						/>
						{t("questions.field.translatable")}
					</label>
				)}

				{request.type === "date" && (
					<label className="flex items-center gap-2 font-sans text-sm text-ink">
						<input
							type="checkbox"
							checked={request.allowFutureDates}
							onChange={(event) => update({ allowFutureDates: event.target.checked })}
						/>
						{t("questions.field.allowFutureDates")}
					</label>
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

			{TRANSLATABLE_TYPES.includes(request.type) && (
				<p className="font-sans text-xs text-ink-muted">{t("questions.field.translatableHelp")}</p>
			)}

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
							update({ dependsOnQuestionId: event.target.value || null, dependsOnChoiceId: null })
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
								value={request.dependsOnChoiceId ?? ""}
								required
								onChange={(event) => update({ dependsOnChoiceId: event.target.value || null })}
							>
								<option value="">{t("questions.field.dependsOnOptionNone")}</option>
								{sortChoices(dependsOnParent.options, locale, (option) => option.labelEn ?? option.labelFr ?? "").map((option) => (
									<option key={option.id} value={option.id}>
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
				<div ref={choicesRef} className="flex flex-col gap-3 rounded border border-rule bg-surface p-4">
					<div className="flex flex-wrap items-center justify-between gap-3">
						<h3 className="font-sans text-sm font-medium text-ink">{t("questions.field.options")}</h3>
						<div className="flex items-center gap-2">
							<TranslationDirectionSwitch direction={direction} onChange={setDirection} />
							<button
								type="button"
								className="touch-target rounded border border-rule px-4 font-sans text-sm text-ink hover:bg-surface-2"
								onClick={() => {
									focusNewChoice.current = true
									setRows((current) => [...current, choiceRow({ labelEn: "", labelFr: "" })])
									update({ options: [...request.options, { code: null, labelEn: "", labelFr: "" }] })
								}}
							>
								{t("questions.field.addOption")}
							</button>
						</div>
					</div>
					<p className="font-sans text-xs text-ink-muted">{t("questions.field.optionsHelp")}</p>
					{!translationAvailable && (
						<p id={unavailableId} className="font-sans text-xs text-ink-muted">
							{t("questions.translate.unavailable")}
						</p>
					)}

					{request.options.map((option, index) => {
						// Only a reporter-added choice may be saved missing a
						// language; an administrator's own choice needs both.
						const reporterAdded = option.addedByReporter === true
						const row = rows[index]
						const dirty = row !== undefined && isDirty(option, row, direction)
						const awaiting = !option.labelEn.trim()
							? t("questions.choice.awaitingEnglish")
							: !option.labelFr.trim()
								? t("questions.choice.awaitingFrench")
								: null

						return (
							<div key={row?.key ?? `new-${index}`} className="flex flex-col gap-1" data-testid="question-choice">
								{reporterAdded && (
									<p className="font-sans text-xs text-ink-muted">
										<span className="rounded border border-rule px-2 py-0.5 font-medium text-ink">
											{t("questions.choice.reporterAdded")}
										</span>
										{awaiting && <span className="ml-2">{awaiting}</span>}
									</p>
								)}
								<div className="grid gap-2 sm:grid-cols-2">
									<input
										className={direction === "toFrench" ? sourceFieldClassName : fieldClassName}
										value={option.labelEn}
										required={!reporterAdded}
										aria-label={t("questions.field.optionLabelEn")}
										placeholder={t("questions.field.optionLabelEn")}
										onChange={(event) => updateOption(index, { labelEn: event.target.value })}
									/>
									<div className="flex gap-2">
										<input
											className={direction === "toEnglish" ? sourceFieldClassName : fieldClassName}
											value={option.labelFr}
											required={!reporterAdded}
											aria-label={t("questions.field.optionLabelFr")}
											placeholder={t("questions.field.optionLabelFr")}
											onChange={(event) => updateOption(index, { labelFr: event.target.value })}
										/>
										<button
											type="button"
											className="touch-target inline-flex items-center justify-center rounded border border-rule px-3 text-ink hover:bg-surface-2 disabled:opacity-40"
											aria-label={t("questions.choice.translate")}
											title={t("questions.choice.translate")}
											aria-describedby={translationAvailable ? undefined : unavailableId}
											disabled={!translationAvailable || !dirty || row?.pending === true}
											onClick={() => void translateChoice(index)}
										>
											<TranslateIcon />
										</button>
										<button
											type="button"
											className="touch-target rounded border border-rule px-3 font-sans text-sm text-ink hover:bg-surface-2"
											aria-label={t("questions.field.removeOption")}
											onClick={() => {
												setRows((current) => current.filter((_, position) => position !== index))
												update({ options: request.options.filter((_, current) => current !== index) })
											}}
										>
											×
										</button>
									</div>
								</div>
								{row?.error && (
									<p role="alert" className="font-sans text-xs text-ink">
										{row.error}
									</p>
								)}
								<label className="flex items-center gap-2 font-sans text-xs text-ink-muted">
									{t("questions.choice.position")}
									<select
										className="rounded border border-rule bg-surface px-2 py-1 font-sans text-sm text-ink"
										value={option.pin ?? "none"}
										onChange={(event) => updateOption(index, { pin: event.target.value })}
									>
										{PINS.map((pin) => (
											<option key={pin} value={pin}>
												{t(`questions.choice.pin.${pin}`)}
											</option>
										))}
									</select>
								</label>
								{canReplace && option.code !== null && (
									<label className="flex items-center gap-2 font-sans text-xs text-ink-muted">
										<input
											type="checkbox"
											checked={option.replace === true}
											onChange={(event) => updateOption(index, { replace: event.target.checked })}
										/>
										{t("questions.choice.replace")}
									</label>
								)}
							</div>
						)
					})}
					{canReplace && request.options.some((option) => option.code !== null) && (
						<p className="font-sans text-xs text-ink-muted">{t("questions.choice.replaceHelp")}</p>
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
