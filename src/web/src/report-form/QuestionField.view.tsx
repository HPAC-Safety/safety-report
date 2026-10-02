import { AttachmentField } from "./AttachmentField"
import { DateField } from "./DateField"
import { EmailField } from "./EmailField"
import { MultiSelectPicker } from "./MultiSelectPicker"
import { PhoneField } from "./PhoneField"
import type { QuestionFieldModel, QuestionFieldProps } from "./QuestionField"
import { SingleSelectField } from "./SingleSelectField"
import { TypeAheadField } from "./TypeAheadField"

const labelClassName = "block font-sans text-sm font-medium text-ink"

export type QuestionFieldViewProps = QuestionFieldProps & QuestionFieldModel

/** One answerable question, in whichever shape its type needs. Not used for `statement`/`group`, which collect no answer. */
export function QuestionFieldView({
	question,
	errorText,
	t,
	announcement,
	errorId,
	helpId,
	noteId,
	help,
	note,
	prompt,
	labelFor,
	control,
}: QuestionFieldViewProps) {
	const requiredBadge = question.isRequired && (
		<span className="ml-1 font-sans text-xs font-normal text-ink-muted">{t("report.required.badge")}</span>
	)

	const label = (
		<label className={labelClassName} htmlFor={labelFor}>
			{prompt}
			{requiredBadge}
		</label>
	)

	const errorNode = errorText ? (
		<p id={errorId} role="alert" className="mt-1 font-sans text-sm text-brand-700">
			{errorText}
		</p>
	) : null

	// The live region is in the page before its text changes, so the change is
	// announced when the parent is answered and the field opens.
	const noteNode = (
		<>
			{note ? (
				<p id={noteId} data-testid="question-note" className="mt-1 font-sans text-sm text-ink-muted">
					{note}
				</p>
			) : null}
			{announcement !== undefined && (
				<p role="status" className="sr-only" data-testid="question-announcement">
					{announcement}
				</p>
			)}
		</>
	)

	const helpNode = help ? (
		<p id={helpId} className="mt-1 font-sans text-xs text-ink-muted">
			{help}
		</p>
	) : null

	switch (control.kind) {
		case "boolean":
			return (
				<fieldset className="mb-6" aria-describedby={control.describedBy}>
					<legend className={labelClassName}>
						{prompt}
						{requiredBadge}
					</legend>
					<div className="mt-2 flex gap-4">
						{(["yes", "no"] as const).map((token) => (
							<label key={token} className="touch-target inline-flex items-center gap-2 font-sans text-ink">
								<input
									type="radio"
									name={control.name}
									checked={control.value === token}
									onChange={() => control.onPick(token)}
								/>
								{token === "yes" ? t("report.booleanYes") : t("report.booleanNo")}
							</label>
						))}
					</div>
					{helpNode}
					{errorNode}
				</fieldset>
			)

		case "typeAhead":
			return (
				<div className="mb-6">
					{label}
					<TypeAheadField {...control.props} />
					{noteNode}
					{helpNode}
					{errorNode}
				</div>
			)

		case "singleSelect":
			return (
				<div className="mb-6">
					{label}
					<SingleSelectField {...control.props} />
					{noteNode}
					{helpNode}
					{errorNode}
				</div>
			)

		case "multiSelect":
			return (
				<div className="mb-6">
					<MultiSelectPicker
						{...control.props}
						label={
							<>
								{prompt}
								{requiredBadge}
							</>
						}
					/>
					{helpNode}
					{errorNode}
				</div>
			)

		case "file":
			return (
				<div className="mb-6">
					{label}
					<AttachmentField {...control.props} />
					<p className="mt-1 font-sans text-xs text-ink-muted">{t("report.attachments.keptWithReport")}</p>
					{helpNode}
					{errorNode}
				</div>
			)

		case "phone":
			return (
				<div className="mb-6">
					{label}
					<PhoneField {...control.props} />
					{helpNode}
					{errorNode}
				</div>
			)

		case "email":
			return (
				<div className="mb-6">
					{label}
					<EmailField {...control.props} />
					{helpNode}
					{errorNode}
				</div>
			)

		case "date":
			return (
				<div className="mb-6">
					{label}
					<DateField {...control.props} />
					{helpNode}
					{errorNode}
				</div>
			)

		case "text":
			return (
				<div className="mb-6">
					{label}
					{control.multiline ? (
						<textarea
							id={control.id}
							className={control.className}
							rows={5}
							value={control.value}
							aria-describedby={control.describedBy}
							placeholder={control.placeholder}
							onChange={control.onChange}
						/>
					) : (
						<input
							id={control.id}
							type={control.type}
							className={control.className}
							value={control.value}
							aria-describedby={control.describedBy}
							placeholder={control.placeholder}
							onChange={control.onChange}
						/>
					)}
					{helpNode}
					{errorNode}
				</div>
			)
	}
}
