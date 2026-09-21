import { useLocale } from "../i18n/useLocale"
import {
	OPTION_TYPES,
	QUESTION_TYPES,
	type OptionSetView,
	type QuestionType,
	type QuestionView,
	type SaveQuestionRequest,
} from "../api/adminQuestions"

/*
 * The authoring form for one question.
 *
 * Both official languages are authored here, together, because a revision is
 * born complete — there is no partially translated question, and no translation
 * service writes question text (ADR-0021, product invariant #1). The French
 * fields are therefore as required as the English ones.
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
			sectionKey: null,
			dependsOnQuestionId: null,
			optionSetId: null,
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
			sectionKey: question.sectionKey,
			dependsOnQuestionId: question.dependsOnQuestionId,
			optionSetId: question.optionSetId,
			options: question.options.map((option) => ({
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
	booleanQuestions,
	isEditing,
	onChange,
	onCancel,
	onSave,
}: {
	draft: QuestionDraft
	optionSets: OptionSetView[]
	booleanQuestions: QuestionView[]
	isEditing: boolean
	onChange: (draft: QuestionDraft) => void
	onCancel: () => void
	onSave: (draft: QuestionDraft) => void
}) {
	const { t } = useLocale()
	const request = draft.request
	const takesOptions = OPTION_TYPES.includes(request.type)

	function update(changes: Partial<SaveQuestionRequest>) {
		onChange({ request: { ...request, ...changes } })
	}

	function updateOption(index: number, changes: Partial<{ code: string; labelEn: string; labelFr: string }>) {
		const options = request.options.map((option, current) => (current === index ? { ...option, ...changes } : option))
		update({ options })
	}

	return (
		<form
			className="mt-6 flex flex-col gap-5 rounded border border-rule bg-surface-2 p-6"
			onSubmit={(event) => {
				event.preventDefault()
				onSave(draft)
			}}
		>
			<h2 className="font-display text-xl font-bold">
				{isEditing ? t("questions.editorTitleEdit") : t("questions.editorTitleNew")}
			</h2>

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
							update(
								OPTION_TYPES.includes(type)
									? { type }
									: { type, options: [], optionSetId: null },
							)
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

			<fieldset className="flex flex-wrap gap-6">
				<legend className="font-sans text-sm font-medium text-ink">{t("questions.field.behaviour")}</legend>

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

				<label className="flex items-center gap-2 font-sans text-sm text-ink">
					<input
						type="checkbox"
						checked={request.isActive}
						onChange={(event) => update({ isActive: event.target.checked })}
					/>
					{t("questions.field.active")}
				</label>
			</fieldset>

			<p className="font-sans text-xs text-ink-muted">{t("questions.field.privateHelp")}</p>

			<div>
				<label className={labelClassName} htmlFor="question-depends-on">
					{t("questions.field.dependsOn")}
				</label>
				<select
					id="question-depends-on"
					className={fieldClassName}
					value={request.dependsOnQuestionId ?? ""}
					onChange={(event) => update({ dependsOnQuestionId: event.target.value || null })}
				>
					<option value="">{t("questions.field.dependsOnNone")}</option>
					{booleanQuestions.map((question) => (
						<option key={question.id} value={question.id}>
							{question.labelEn}
						</option>
					))}
				</select>
				<p className="mt-1 font-sans text-xs text-ink-muted">{t("questions.field.dependsOnHelp")}</p>
			</div>

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

					{!request.optionSetId && (
						<>
							<h3 className="font-sans text-sm font-medium text-ink">{t("questions.field.options")}</h3>

							{request.options.map((option, index) => (
								<div key={index} className="grid gap-2 sm:grid-cols-3">
									<input
										className={fieldClassName}
										value={option.code}
										required
										aria-label={t("questions.field.optionCode")}
										placeholder={t("questions.field.optionCode")}
										onChange={(event) => updateOption(index, { code: event.target.value })}
									/>
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
									update({ options: [...request.options, { code: "", labelEn: "", labelFr: "" }] })
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
					className="touch-target inline-flex items-center rounded bg-brand-700 px-5 font-sans font-medium text-ink-inverse hover:bg-brand-600"
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
