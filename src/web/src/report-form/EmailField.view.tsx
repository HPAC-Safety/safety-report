import type { EmailFieldModel, EmailFieldProps } from "./EmailField"

export type EmailFieldViewProps = EmailFieldProps & EmailFieldModel

/** An email question (REQ-SUB-085, REQ-SUB-092..095): the field and its suggestion listbox. */
export function EmailFieldView({
	fieldId,
	className,
	describedBy,
	placeholder,
	value,
	t,
	listId,
	suggestions,
	showing,
	active,
	onFocus,
	onBlur,
	onKeyDown,
	onInputChange,
	onChoose,
}: EmailFieldViewProps) {
	return (
		<div className="relative">
			<input
				id={fieldId}
				type="email"
				inputMode="email"
				autoComplete="email"
				role="combobox"
				aria-autocomplete="list"
				aria-expanded={showing}
				aria-controls={listId}
				aria-activedescendant={showing && active >= 0 ? `${listId}-${active}` : undefined}
				className={className}
				value={value}
				aria-describedby={describedBy}
				placeholder={placeholder}
				onFocus={onFocus}
				onBlur={onBlur}
				onKeyDown={onKeyDown}
				onChange={onInputChange}
			/>
			<ul
				id={listId}
				role="listbox"
				aria-label={t("report.email.suggestions")}
				hidden={!showing}
				className="mt-1 rounded border border-rule bg-surface py-1 font-sans text-ink"
			>
				{suggestions.map((suggestion, index) => (
					<li
						key={suggestion}
						id={`${listId}-${index}`}
						role="option"
						aria-selected={index === active}
						className={`touch-target flex cursor-pointer items-center px-3 ${index === active ? "bg-surface-2" : "hover:bg-surface-2"}`}
						// Chosen on press, before the field loses focus and the list closes.
						onMouseDown={(event) => {
							event.preventDefault()
							onChoose(suggestion)
						}}
					>
						{suggestion}
					</li>
				))}
			</ul>
		</div>
	)
}
