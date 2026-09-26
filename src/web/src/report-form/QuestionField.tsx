import { Fragment } from "react"
import type { Locale } from "../i18n/locales"
import type { PublicQuestionView } from "../api/publicQuestions"
import { AttachmentField, type Attachment } from "./AttachmentField"
import { DateField } from "./DateField"
import type { DraftAnswer } from "./draft"
import { EmailField } from "./EmailField"
import { MultiSelectPicker } from "./MultiSelectPicker"
import { PhoneField } from "./PhoneField"
import { TypeAheadField } from "./TypeAheadField"
import { optionFor, optionGroups, optionLabel, questionHelp, questionLabel, questionPlaceholder } from "./steps"

const fieldClassName =
	"mt-1 w-full rounded border border-rule bg-surface px-3 py-2 font-sans text-ink placeholder:text-ink-muted"

const labelClassName = "block font-sans text-sm font-medium text-ink"

const INPUT_TYPE_BY_QUESTION_TYPE: Record<string, string> = {
	number: "number",
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
		// A type-ahead choice picked from its list is held by its ID, and shown in the reader's language.
		const picked = answer?.kind === "value" && answer.choice ? question.options.find((option) => option.id === answer.choice) : undefined
		const groups = optionGroups(question, locale)
		return (
			<div className="mb-6">
				{label}
				{question.type === "autocomplete" ? (
					<TypeAheadField
						fieldId={fieldId}
						label={questionLabel(question, locale)}
						groups={groups.map((group) =>
							group.map((option) => ({ key: option.id, label: optionLabel(option, locale), lang: option.onlyIn ?? undefined })),
						)}
						value={picked ? optionLabel(picked, locale) : value}
						selectedKey={picked?.id}
						placeholder={questionPlaceholder(question, locale) ?? undefined}
						describedBy={describedBy}
						locale={locale}
						onChange={(typed, choice) =>
							onChange(typed ? { kind: "value", value: typed, ...(choice ? { choice } : {}) } : undefined)
						}
						t={t}
					/>
				) : (
					<select
						id={fieldId}
						className={fieldClassName}
						value={value ? (optionFor(question, value)?.id ?? "") : ""}
						aria-describedby={describedBy}
						onChange={(event) => onChange(event.target.value ? { kind: "value", value: event.target.value } : undefined)}
					>
						<option value="">{t("report.select.placeholder")}</option>
						{groups.map((group, index) => (
							<Fragment key={group[0].id}>
								{/* React 18 allows no hr element in a select, so a separator is a disabled option (ADR-0136). */}
								{index > 0 && (
									<option disabled aria-hidden="true" value="" data-separator>
										──────────
									</option>
								)}
								{group.map((option) => (
									<option key={option.id} value={option.id}>
										{optionLabel(option, locale)}
									</option>
								))}
							</Fragment>
						))}
					</select>
				)}
				{helpNode}
				{errorNode}
			</div>
		)
	}

	if (question.type === "multi_select") {
		// The chosen choices' IDs; a draft saved before answers named choices holds labels.
		const values = (answer?.kind === "options" ? answer.values : []).map((stored) => optionFor(question, stored)?.id ?? stored)
		const toggle = (id: string) => {
			const next = values.includes(id) ? values.filter((entry) => entry !== id) : [...values, id]
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
					groups={optionGroups(question, locale).map((group) =>
						group.map((option) => ({ key: option.id, label: optionLabel(option, locale) })),
					)}
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
				<AttachmentField
					fieldId={fieldId}
					describedBy={describedBy}
					attachments={attachments}
					onAttachmentsChange={onAttachmentsChange}
					onBusyChange={onUploadingChange}
					remaining={attachmentRoom}
					t={t}
				/>
				<p className="mt-1 font-sans text-xs text-ink-muted">{t("report.attachments.keptWithReport")}</p>
				{helpNode}
				{errorNode}
			</div>
		)
	}

	if (question.type === "phone") {
		return (
			<div className="mb-6">
				{label}
				<PhoneField fieldId={fieldId} describedBy={describedBy} answer={answer} onChange={onChange} locale={locale} t={t} />
				{helpNode}
				{errorNode}
			</div>
		)
	}

	if (question.type === "email") {
		return (
			<div className="mb-6">
				{label}
				<EmailField
					fieldId={fieldId}
					className={fieldClassName}
					describedBy={describedBy}
					placeholder={questionPlaceholder(question, locale) ?? undefined}
					value={answer?.kind === "value" ? answer.value : ""}
					onChange={(value) => onChange(value ? { kind: "value", value } : undefined)}
					t={t}
				/>
				{helpNode}
				{errorNode}
			</div>
		)
	}

	if (question.type === "date") {
		return (
			<div className="mb-6">
				{label}
				<DateField
					fieldId={fieldId}
					className={fieldClassName}
					describedBy={describedBy}
					placeholder={questionPlaceholder(question, locale) ?? undefined}
					value={answer?.kind === "value" ? answer.value : ""}
					allowFutureDates={question.allowFutureDates}
					locale={locale}
					onChange={(value) => onChange(value ? { kind: "value", value } : undefined)}
					t={t}
				/>
				{helpNode}
				{errorNode}
			</div>
		)
	}

	// Every remaining type stores one plain string: short/long text, number,
	// time (ADR-0072).
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
