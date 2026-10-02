import type { ChangeEvent } from "react"

import type { Locale } from "../i18n/locales"
import type { PublicQuestionView } from "../api/publicQuestions"
import type { AttachmentFieldProps, Attachment } from "./AttachmentField"
import type { DateFieldProps } from "./DateField"
import type { DraftAnswer } from "./draft"
import type { EmailFieldProps } from "./EmailField"
import type { MultiSelectPickerProps } from "./MultiSelectPicker"
import type { PhoneFieldProps } from "./PhoneField"
import { QuestionFieldView } from "./QuestionField.view"
import type { SingleSelectFieldProps } from "./SingleSelectField"
import type { TypeAheadFieldProps } from "./TypeAheadField"
import { optionFor, optionGroups, optionLabel, questionHelp, questionLabel, questionPlaceholder, questionPrompt } from "./steps"

const fieldClassName =
	"mt-1 w-full rounded border border-rule bg-surface px-3 py-2 font-sans text-ink placeholder:text-ink-muted"

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
	/** True while the question cannot be answered: a picker or type-ahead waiting on its parent's answer (ADR-0146). */
	disabled?: boolean
	/** Why it cannot be answered, or that its parent's answer leaves nothing to pick; null when there is nothing to say. */
	note?: string | null
	/**
	 * For a question whose choices depend on another's: what a screen reader is
	 * told, politely, when the parent's answer changes what it offers. Undefined
	 * for any other question (ADR-0146).
	 */
	announcement?: string
}

/** How the question is drawn: the control its type needs, and the props that control takes. */
export type QuestionFieldControl =
	| { kind: "boolean"; name: string; describedBy: string | undefined; value: string; onPick: (token: "yes" | "no") => void }
	| { kind: "typeAhead"; props: TypeAheadFieldProps }
	| { kind: "singleSelect"; props: SingleSelectFieldProps }
	| { kind: "multiSelect"; props: Omit<MultiSelectPickerProps, "label"> }
	| { kind: "file"; props: AttachmentFieldProps }
	| { kind: "phone"; props: PhoneFieldProps }
	| { kind: "email"; props: EmailFieldProps }
	| { kind: "date"; props: DateFieldProps }
	| {
			kind: "text"
			multiline: boolean
			id: string
			type: string
			className: string
			value: string
			describedBy: string | undefined
			placeholder: string | undefined
			onChange: (event: ChangeEvent<HTMLInputElement | HTMLTextAreaElement>) => void
	  }

/**
 * The view model of one answerable question, in whichever shape its type
 * needs. Not used for `statement`/`group`, which collect no answer.
 */
export function useQuestionField({
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
	disabled = false,
	note = null,
}: QuestionFieldProps) {
	const fieldId = `question-${question.revisionId}`
	const errorId = `${fieldId}-error`
	const helpId = `${fieldId}-help`
	const noteId = `${fieldId}-note`
	const help = questionHelp(question, locale)
	const describedBy = [note ? noteId : null, help ? helpId : null, errorText ? errorId : null].filter(Boolean).join(" ") || undefined
	const placeholder = questionPlaceholder(question, locale) ?? undefined
	const answerText = answer?.kind === "value" ? answer.value : ""
	const onValue = (value: string) => onChange(value ? { kind: "value", value } : undefined)

	const control = ((): QuestionFieldControl => {
		switch (question.type) {
			case "yes_no":
			case "checkbox":
				return { kind: "boolean", name: fieldId, describedBy, value: answerText, onPick: (token) => onChange({ kind: "value", value: token }) }

			case "autocomplete": {
				// A type-ahead choice picked from its list is held by its ID, and shown in the reader's language.
				const picked = answer?.kind === "value" && answer.choice ? question.options.find((option) => option.id === answer.choice) : undefined
				return {
					kind: "typeAhead",
					props: {
						fieldId,
						label: questionLabel(question, locale),
						groups: optionGroups(question, locale).map((group) =>
							group.map((option) => ({
								key: option.id,
								label: optionLabel(option, locale),
								lang: option.onlyIn ?? undefined,
								aliases: option.aliases,
							})),
						),
						value: picked ? optionLabel(picked, locale) : answerText,
						selectedKey: picked?.id,
						placeholder,
						describedBy,
						locale,
						onChange: (typed, choice) =>
							onChange(typed ? { kind: "value", value: typed, ...(choice ? { choice } : {}) } : undefined),
						t,
						disabled,
					},
				}
			}

			case "single_select":
				return {
					kind: "singleSelect",
					props: {
						fieldId,
						label: questionLabel(question, locale),
						groups: optionGroups(question, locale).map((group) =>
							group.map((option) => ({ key: option.id, label: optionLabel(option, locale), lang: option.onlyIn ?? undefined })),
						),
						// A draft saved before answers named choices holds a label; it still finds its choice.
						selectedKey: answerText ? optionFor(question, answerText)?.id : undefined,
						placeholder: t("report.select.placeholder"),
						describedBy,
						locale,
						onChange: (choice) => onChange(choice ? { kind: "value", value: choice } : undefined),
						disabled,
					},
				}

			case "multi_select": {
				// The chosen choices' IDs; a draft saved before answers named choices holds labels.
				const values = (answer?.kind === "options" ? answer.values : []).map((stored) => optionFor(question, stored)?.id ?? stored)
				return {
					kind: "multiSelect",
					props: {
						fieldId,
						groups: optionGroups(question, locale).map((group) =>
							group.map((option) => ({ key: option.id, label: optionLabel(option, locale) })),
						),
						values,
						placeholder: t("report.multiSelect.placeholder"),
						describedBy,
						onToggle: (id) => {
							const next = values.includes(id) ? values.filter((entry) => entry !== id) : [...values, id]
							onChange(next.length > 0 ? { kind: "options", values: next } : undefined)
						},
					},
				}
			}

			case "file_upload":
				return {
					kind: "file",
					props: {
						fieldId,
						describedBy,
						attachments,
						onAttachmentsChange,
						onBusyChange: onUploadingChange,
						remaining: attachmentRoom,
						t,
					},
				}

			case "phone":
				return { kind: "phone", props: { fieldId, describedBy, answer, onChange, locale, t } }

			case "email":
				return {
					kind: "email",
					props: { fieldId, className: fieldClassName, describedBy, placeholder, value: answerText, onChange: onValue, t },
				}

			case "date":
				return {
					kind: "date",
					props: {
						fieldId,
						className: fieldClassName,
						describedBy,
						placeholder,
						value: answerText,
						allowFutureDates: question.allowFutureDates,
						locale,
						onChange: onValue,
						t,
					},
				}

			default:
				// Every remaining type stores one plain string: short/long text, number,
				// time (ADR-0072).
				return {
					kind: "text",
					multiline: question.type === "long_text",
					id: fieldId,
					type: INPUT_TYPE_BY_QUESTION_TYPE[question.type] ?? "text",
					className: fieldClassName,
					value: answerText,
					describedBy,
					placeholder,
					onChange: (event) => onValue(event.target.value),
				}
		}
	})()

	return {
		fieldId,
		errorId,
		helpId,
		noteId,
		help,
		note,
		prompt: questionPrompt(question, locale),
		// A yes/no question's legend names the group; every other label names its field.
		labelFor: question.type === "yes_no" ? undefined : fieldId,
		control,
	}
}

export type QuestionFieldModel = ReturnType<typeof useQuestionField>

/** One answerable question, in whichever shape its type needs. Not used for `statement`/`group`, which collect no answer. */
export function QuestionField(props: QuestionFieldProps) {
	return <QuestionFieldView {...props} {...useQuestionField(props)} />
}
