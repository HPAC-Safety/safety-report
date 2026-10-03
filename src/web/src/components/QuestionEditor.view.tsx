import type { RefObject } from "react"
import { useLocale } from "../i18n/useLocale"
import { MultiSelectPicker } from "../report-form/MultiSelectPicker"
import {
	TranslationDirectionSwitch,
	type TranslationDirection,
} from "./TranslationDirectionSwitch"
import type { OptionInput, QuestionDraft, QuestionType, QuestionView, SaveQuestionRequest, wordingLabels } from "./questionDraft"

/*
 * The authoring form for one question: markup only (ADR-0188). Its state,
 * Translate requests and derived values are the view model in
 * QuestionEditor.tsx; the behaviour is described there.
 */

export interface QuestionEditorProps {
	draft: QuestionDraft
	conditionQuestions: QuestionView[]
	groupQuestions: QuestionView[]
	/** The questions this one's choices may depend on: earlier single-selects and type-aheads that depend on nothing (ADR-0146). */
	choiceParentQuestions?: QuestionView[]
	isEditing: boolean
	hasBeenAnswered: boolean
	translationAvailable: boolean
	/** A new draft, or a function of the current one for a change that lands after a request. */
	onChange: (change: QuestionDraft | ((current: QuestionDraft) => QuestionDraft)) => void
	onCancel: () => void
	onSave: (draft: QuestionDraft) => Promise<void> | void
}

/** One choice row, ready to render. */
export interface ChoiceItem {
	option: OptionInput
	index: number
	reactKey: string | number
	fieldKey: string | number
	reporterAdded: boolean
	awaiting: string | null
	offered: boolean
	pending: boolean
	error: string | null | undefined
	parentValues: string[]
	invalid: boolean
}

export interface QuestionEditorViewModel {
	request: SaveQuestionRequest
	choiceParentQuestions: QuestionView[]
	questionTypes: readonly QuestionType[]
	takesOptions: boolean
	canReplace: boolean
	collectsNoAnswer: boolean
	translatableType: boolean
	wording: ReturnType<typeof wordingLabels>
	helpTakesLines: boolean
	dependsOnParent: QuestionView | undefined
	dependsOnOptions: QuestionView["options"]
	takesChoiceParent: boolean
	choiceParent: QuestionView | undefined
	parentGroup: { key: string; label: string }[]
	unlinked: OptionInput[]
	translating: boolean
	translationError: string | null
	direction: TranslationDirection
	setDirection: (direction: TranslationDirection) => void
	wordingDirection: TranslationDirection
	setWordingDirection: (direction: TranslationDirection) => void
	wordingTranslated: boolean
	wordingOffered: boolean
	colonEn: boolean
	colonFr: boolean
	canSave: boolean
	choiceItems: ChoiceItem[]
	unavailableId: string
	choicesHeadingId: string
	unlinkedId: string
	choicesRef: RefObject<HTMLDivElement>
	update: (changes: Partial<SaveQuestionRequest>) => void
	updateOption: (
		index: number,
		changes: Partial<Pick<OptionInput, "labelEn" | "labelFr" | "replace" | "pin" | "parentChoiceIds">>,
	) => void
	changeType: (type: QuestionType) => void
	addChoice: () => void
	removeChoice: (index: number) => void
	toggleParentChoice: (index: number, id: string) => void
	translateWording: () => Promise<void>
	translateChoice: (index: number) => Promise<void>
}

export type QuestionEditorViewProps = QuestionEditorProps & QuestionEditorViewModel

/** The positions an option can take (ADR-0136). */
const PINS = ["none", "first", "last"] as const

const fieldClassName =
	"mt-1 w-full rounded border border-rule bg-surface px-3 py-2 font-sans text-ink placeholder:text-ink-muted"

const labelClassName = "block font-sans text-sm font-medium text-ink"

// The field a choice is translated from: marked just enough that the direction
// reads at a glance.
const sourceFieldClassName = fieldClassName.replace("border-rule", "border-ink-muted")

function TranslateIcon() {
	return (
		<svg viewBox="0 0 24 24" width="18" height="18" fill="currentColor" aria-hidden="true">
			<path d="M12.87 15.07l-2.54-2.51.03-.03A17.52 17.52 0 0 0 14.07 6H17V4h-7V2H8v2H1v2h11.17C11.5 7.92 10.44 9.75 9 11.35 8.07 10.32 7.3 9.19 6.69 8h-2c.73 1.63 1.73 3.17 2.98 4.56l-5.09 5.02L4 19l5-5 3.11 3.11.76-2.04zM18.5 10h-2L12 22h2l1.12-3h4.75L21 22h2l-4.5-12zm-2.62 7l1.62-4.33L19.12 17h-3.24z" />
		</svg>
	)
}

export function QuestionEditorView({
	draft,
	conditionQuestions,
	groupQuestions,
	choiceParentQuestions,
	isEditing,
	hasBeenAnswered,
	translationAvailable,
	onCancel,
	onSave,
	request,
	questionTypes,
	takesOptions,
	canReplace,
	collectsNoAnswer,
	translatableType,
	wording,
	helpTakesLines,
	dependsOnParent,
	dependsOnOptions,
	takesChoiceParent,
	choiceParent,
	parentGroup,
	unlinked,
	translating,
	translationError,
	direction,
	setDirection,
	wordingDirection,
	setWordingDirection,
	wordingTranslated,
	wordingOffered,
	colonEn,
	colonFr,
	canSave,
	choiceItems,
	unavailableId,
	choicesHeadingId,
	unlinkedId,
	choicesRef,
	update,
	updateOption,
	changeType,
	addChoice,
	removeChoice,
	toggleParentChoice,
	translateWording,
	translateChoice,
}: QuestionEditorViewProps) {
	const { t } = useLocale()
	// The side the wording is translated from is marked, as a choice's is.
	const englishFieldClassName = wordingDirection === "toFrench" ? sourceFieldClassName : fieldClassName
	const frenchFieldClassName = wordingDirection === "toEnglish" ? sourceFieldClassName : fieldClassName

	return (
		<form
			className="flex flex-col gap-5 rounded border border-rule bg-surface-2 p-6"
			onSubmit={(event) => {
				event.preventDefault()
				void onSave(draft)
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
						onChange={(event) => changeType(event.target.value as QuestionType)}
					>
						{questionTypes.map((type) => (
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
						className={englishFieldClassName}
						value={request.labelEn}
						required
						aria-invalid={colonEn || undefined}
						aria-describedby={colonEn ? "question-label-en-colon" : undefined}
						onChange={(event) => update({ labelEn: event.target.value })}
					/>
					{colonEn && (
						<p id="question-label-en-colon" role="alert" className="mt-1 font-sans text-sm text-brand-700">
							{t("questions.error.labelColon")}
						</p>
					)}
				</div>

				<div>
					<label className={labelClassName} htmlFor="question-label-fr">
						{t(`questions.field.${wording.labelFr}`)}
					</label>
					<input
						id="question-label-fr"
						className={frenchFieldClassName}
						value={request.labelFr}
						required
						aria-invalid={colonFr || undefined}
						aria-describedby={colonFr ? "question-label-fr-colon" : undefined}
						onChange={(event) => update({ labelFr: event.target.value })}
					/>
					{colonFr && (
						<p id="question-label-fr-colon" role="alert" className="mt-1 font-sans text-sm text-brand-700">
							{t("questions.error.labelColon")}
						</p>
					)}
				</div>

				<div>
					<label className={labelClassName} htmlFor="question-help-en">
						{t(`questions.field.${wording.helpEn}`)}
					</label>
					{helpTakesLines ? (
						<textarea
							id="question-help-en"
							className={`${englishFieldClassName} resize-y`}
							rows={6}
							value={request.helpTextEn ?? ""}
							onChange={(event) => update({ helpTextEn: event.target.value || null })}
						/>
					) : (
						<input
							id="question-help-en"
							className={englishFieldClassName}
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
							className={`${frenchFieldClassName} resize-y`}
							rows={6}
							value={request.helpTextFr ?? ""}
							onChange={(event) => update({ helpTextFr: event.target.value || null })}
						/>
					) : (
						<input
							id="question-help-fr"
							className={frenchFieldClassName}
							value={request.helpTextFr ?? ""}
							onChange={(event) => update({ helpTextFr: event.target.value || null })}
						/>
					)}
				</div>
			</div>

			<div role="group" aria-label={t("questions.translate.wordingGroup")} className="flex flex-wrap items-center gap-3">
				<TranslationDirectionSwitch direction={wordingDirection} onChange={setWordingDirection} />
				<button
					type="button"
					className="touch-target inline-flex items-center rounded border border-rule px-4 font-sans text-sm text-ink hover:bg-surface disabled:opacity-40"
					disabled={!translationAvailable || !wordingOffered || translating}
					onClick={() => void translateWording()}
				>
					{translating ? t("questions.translate.working") : t("questions.translate.action")}
				</button>

				<p className="font-sans text-xs text-ink-muted">
					{!translationAvailable
						? t("questions.translate.unavailable")
						: wordingTranslated && !wordingOffered
							? t("questions.translate.draftWarning")
							: t("questions.translate.hint")}
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

				{translatableType && (
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

			{translatableType && (
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
								{dependsOnOptions.map((option) => (
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

			{takesChoiceParent && (
				<div>
					<label className={labelClassName} htmlFor="question-choices-depend-on">
						{t("questions.field.choicesDependOn")}
					</label>
					<select
						id="question-choices-depend-on"
						className={fieldClassName}
						value={request.choicesDependOnQuestionId ?? ""}
						onChange={(event) => update({ choicesDependOnQuestionId: event.target.value || null })}
					>
						<option value="">{t("questions.field.choicesDependOnNone")}</option>
						{choiceParentQuestions.map((question) => (
							<option key={question.id} value={question.id}>
								{question.labelEn}
							</option>
						))}
					</select>
					<p className="mt-1 font-sans text-xs text-ink-muted">{t("questions.field.choicesDependOnHelp")}</p>
				</div>
			)}

			{takesOptions && (
				<div
					ref={choicesRef}
					role="group"
					aria-labelledby={choicesHeadingId}
					className="flex flex-col gap-3 rounded border border-rule bg-surface p-4"
				>
					<div className="flex flex-wrap items-center justify-between gap-3">
						<h3 id={choicesHeadingId} className="font-sans text-sm font-medium text-ink">
							{t("questions.field.options")}
						</h3>
						<div className="flex items-center gap-2">
							<TranslationDirectionSwitch direction={direction} onChange={setDirection} />
							<button
								type="button"
								className="touch-target rounded border border-rule px-4 font-sans text-sm text-ink hover:bg-surface-2"
								onClick={addChoice}
							>
								{t("questions.field.addOption")}
							</button>
						</div>
					</div>
					<p className="font-sans text-xs text-ink-muted">{t("questions.field.optionsHelp")}</p>
					{choiceParent && (
						<p className="font-sans text-xs text-ink-muted">
							{t("questions.choice.parentHelp", { question: choiceParent.labelEn })}
						</p>
					)}
					{!translationAvailable && (
						<p id={unavailableId} className="font-sans text-xs text-ink-muted">
							{t("questions.translate.unavailable")}
						</p>
					)}

					{choiceItems.map(({ option, index, reactKey, fieldKey, reporterAdded, awaiting, offered, pending, error, parentValues, invalid }) => {
						return (
							<div key={reactKey} className="flex flex-col gap-1" data-testid="question-choice">
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
											aria-busy={pending}
											disabled={!translationAvailable || !offered || pending}
											onClick={() => void translateChoice(index)}
										>
											<TranslateIcon />
											{pending && <span className="sr-only">{t("questions.translate.working")}</span>}
										</button>
										<button
											type="button"
											className="touch-target rounded border border-rule px-3 font-sans text-sm text-ink hover:bg-surface-2"
											aria-label={t("questions.field.removeOption")}
											onClick={() => removeChoice(index)}
										>
											×
										</button>
									</div>
								</div>
								{error && (
									<p role="alert" className="font-sans text-xs text-ink">
										{error}
									</p>
								)}
								{choiceParent && (
									// Every parent choice this one is offered under: one or more (ADR-0151).
									<div className="max-w-sm" data-testid="question-choice-parent">
										<MultiSelectPicker
											fieldId={`question-choice-parent-${fieldKey}`}
											label={t("questions.choice.parentChoice")}
											groups={[parentGroup]}
											// Only live parent choices are shown and counted; a link to one since
											// removed stays in the draft, untouched, and the server keeps it.
											values={parentValues}
											placeholder={t("questions.choice.parentChoiceNone")}
											invalid={invalid}
											describedBy={invalid ? unlinkedId : undefined}
											onToggle={(id) => toggleParentChoice(index, id)}
										/>
									</div>
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
					{unlinked.length > 0 && (
						<p id={unlinkedId} role="status" data-testid="question-choices-unlinked" className="font-sans text-sm text-ink">
							{t("questions.choice.unlinked", {
								choices: unlinked.map((option) => option.labelEn || option.labelFr || t("questions.choice.unnamed")).join(", "),
							})}
						</p>
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
