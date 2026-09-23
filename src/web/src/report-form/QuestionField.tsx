import type { Locale } from "../i18n/locales"
import type { PublicQuestionView } from "../api/publicQuestions"
import { MAX_ATTACHMENTS, MAX_ATTACHMENT_BYTES } from "../api/uploads"
import { AttachmentField, type Attachment } from "./AttachmentField"
import type { DraftAnswer } from "./draft"
import { MultiSelectPicker } from "./MultiSelectPicker"
import { optionLabel, questionHelp, questionLabel, questionPlaceholder } from "./steps"

const fieldClassName =
	"mt-1 w-full rounded border border-rule bg-surface px-3 py-2 font-sans text-ink placeholder:text-ink-muted"

const labelClassName = "block font-sans text-sm font-medium text-ink"

const INPUT_TYPE_BY_QUESTION_TYPE: Record<string, string> = {
	email: "email",
	phone: "tel",
	number: "number",
	date: "date",
	time: "time",
}

export interface QuestionFieldProps {
	question: PublicQuestionView
	locale: Locale
	answer: DraftAnswer | undefined
	onChange: (answer: DraftAnswer | undefined) => void
	attachments: Attachment[]
	onAttachmentsChange: (update: (current: Attachment[]) => Attachment[]) => void
	onUploadingChange: (uploading: boolean) => void
	/** How many more files the whole report may still take. */
	attachmentRoom: number
	errorText: string | null
	t: (key: string, params?: Record<string, string | number>) => string
}

/** One answerable question, in whichever shape its type needs. Not used for `statement`/`group`, which collect no answer. */
export function QuestionField({
	question,
	locale,
	answer,
	onChange,
	attachments,
	onAttachmentsChange,
	onUploadingChange,
	attachmentRoom,
	errorText,
	t,
}: QuestionFieldProps) {
	const fieldId = `question-${question.revisionId}`
	const errorId = `${fieldId}-error`
	const helpId = `${fieldId}-help`
	const help = questionHelp(question, locale)
	const describedBy = [help ? helpId : null, errorText ? errorId : null].filter(Boolean).join(" ") || undefined

	const label = (
		<label className={labelClassName} htmlFor={question.type === "yes_no" ? undefined : fieldId}>
			{questionLabel(question, locale)}
			{question.isRequired && (
				<span className="ml-1 font-sans text-xs font-normal text-ink-muted">{t("report.required.badge")}</span>
			)}
		</label>
	)

	const errorNode = errorText ? (
		<p id={errorId} role="alert" className="mt-1 font-sans text-sm text-brand-700">
			{errorText}
		</p>
	) : null

	const helpNode = help ? (
		<p id={helpId} className="mt-1 font-sans text-xs text-ink-muted">
			{help}
		</p>
	) : null

	if (question.type === "yes_no" || question.type === "checkbox") {
		const value = answer?.kind === "value" ? answer.value : ""
		return (
			<fieldset className="mb-6" aria-describedby={describedBy}>
				<legend className={labelClassName}>
					{questionLabel(question, locale)}
					{question.isRequired && (
						<span className="ml-1 font-sans text-xs font-normal text-ink-muted">{t("report.required.badge")}</span>
					)}
				</legend>
				<div className="mt-2 flex gap-4">
					{(["yes", "no"] as const).map((token) => (
						<label key={token} className="touch-target inline-flex items-center gap-2 font-sans text-ink">
							<input
								type="radio"
								name={fieldId}
								checked={value === token}
								onChange={() => onChange({ kind: "value", value: token })}
							/>
							{token === "yes" ? t("report.booleanYes") : t("report.booleanNo")}
						</label>
					))}
				</div>
				{helpNode}
				{errorNode}
			</fieldset>
		)
	}

	if (question.type === "single_select" || question.type === "autocomplete") {
		const value = answer?.kind === "value" ? answer.value : ""
		const listId = `${fieldId}-list`
		return (
			<div className="mb-6">
				{label}
				{question.type === "autocomplete" ? (
					<>
						<input
							id={fieldId}
							list={listId}
							className={fieldClassName}
							value={value}
							aria-describedby={describedBy}
							placeholder={questionPlaceholder(question, locale) ?? undefined}
							onChange={(event) => onChange(event.target.value ? { kind: "value", value: event.target.value } : undefined)}
						/>
						<datalist id={listId}>
							{question.options.map((option) => (
								// A reporter-added choice may exist in one language only; it is
								// offered in that language, and says so to assistive technology.
								<option key={option.code} value={optionLabel(option, locale)} lang={option.onlyIn ?? undefined} />
							))}
						</datalist>
					</>
				) : (
					<select
						id={fieldId}
						className={fieldClassName}
						value={value}
						aria-describedby={describedBy}
						onChange={(event) => onChange(event.target.value ? { kind: "value", value: event.target.value } : undefined)}
					>
						<option value="">{t("report.select.placeholder")}</option>
						{question.options.map((option) => (
							<option key={option.code} value={optionLabel(option, locale)}>
								{optionLabel(option, locale)}
							</option>
						))}
					</select>
				)}
				{helpNode}
				{errorNode}
			</div>
		)
	}

	if (question.type === "multi_select") {
		const values = answer?.kind === "options" ? answer.values : []
		const toggle = (label: string) => {
			const next = values.includes(label) ? values.filter((entry) => entry !== label) : [...values, label]
			onChange(next.length > 0 ? { kind: "options", values: next } : undefined)
		}
		return (
			<div className="mb-6">
				<MultiSelectPicker
					fieldId={fieldId}
					label={
						<>
							{questionLabel(question, locale)}
							{question.isRequired && (
								<span className="ml-1 font-sans text-xs font-normal text-ink-muted">{t("report.required.badge")}</span>
							)}
						</>
					}
					options={question.options.map((option) => ({ key: option.code, label: optionLabel(option, locale) }))}
					values={values}
					placeholder={t("report.multiSelect.placeholder")}
					describedBy={describedBy}
					onToggle={toggle}
				/>
				{helpNode}
				{errorNode}
			</div>
		)
	}

	if (question.type === "file_upload") {
		return (
			<div className="mb-6">
				{label}
				<p className="mt-1 font-sans text-xs text-ink-muted">
					{t("report.attachments.guidance", {
						count: MAX_ATTACHMENTS,
						size: MAX_ATTACHMENT_BYTES / (1024 * 1024),
					})}
				</p>
				<AttachmentField
					fieldId={fieldId}
					describedBy={describedBy}
					attachments={attachments}
					onAttachmentsChange={onAttachmentsChange}
					onBusyChange={onUploadingChange}
					remaining={attachmentRoom}
					t={t}
				/>
				<p className="mt-1 font-sans text-xs text-ink-muted">{t("report.attachments.notRestored")}</p>
				{helpNode}
				{errorNode}
			</div>
		)
	}

	// Every remaining type stores one plain string: short/long text, email,
	// phone, number, date, time (ADR-0072).
	const value = answer?.kind === "value" ? answer.value : ""
	const inputType = INPUT_TYPE_BY_QUESTION_TYPE[question.type] ?? "text"

	return (
		<div className="mb-6">
			{label}
			{question.type === "long_text" ? (
				<textarea
					id={fieldId}
					className={fieldClassName}
					rows={5}
					value={value}
					aria-describedby={describedBy}
					placeholder={questionPlaceholder(question, locale) ?? undefined}
					onChange={(event) => onChange(event.target.value ? { kind: "value", value: event.target.value } : undefined)}
				/>
			) : (
				<input
					id={fieldId}
					type={inputType}
					className={fieldClassName}
					value={value}
					aria-describedby={describedBy}
					placeholder={questionPlaceholder(question, locale) ?? undefined}
					onChange={(event) => onChange(event.target.value ? { kind: "value", value: event.target.value } : undefined)}
				/>
			)}
			{helpNode}
			{errorNode}
		</div>
	)
}
