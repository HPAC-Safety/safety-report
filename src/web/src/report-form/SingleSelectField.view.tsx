import { Caret, ChoiceOptions, choiceListClassName } from "./ChoiceList.view"
import type { SingleSelectFieldModel, SingleSelectFieldProps } from "./SingleSelectField"

export type SingleSelectFieldViewProps = SingleSelectFieldProps & SingleSelectFieldModel

/**
 * A single-select question as a picker the form draws (REQ-QB-208, ADR-0150):
 * a field with a caret, and the type-ahead's list directly beneath it.
 */
export function SingleSelectFieldView({
	fieldId,
	label,
	groups,
	placeholder,
	describedBy,
	open,
	disabled,
	containerRef,
	fieldRef,
	listId,
	optionId,
	none,
	chosen,
	activeId,
	activeKey,
	onBlur,
	onClick,
	onKeyDown,
	isSelected,
	onPoint,
	onPick,
}: SingleSelectFieldViewProps) {
	return (
		<div ref={containerRef} className="relative mt-1" onBlur={onBlur}>
			<button
				ref={fieldRef}
				id={fieldId}
				type="button"
				role="combobox"
				aria-haspopup="listbox"
				aria-expanded={open}
				aria-controls={listId}
				aria-activedescendant={activeId}
				aria-describedby={describedBy}
				disabled={disabled}
				className="relative block w-full rounded border border-rule bg-surface py-2 pl-3 pr-11 text-left font-sans text-ink disabled:cursor-not-allowed disabled:bg-surface-2 disabled:text-ink-muted"
				onClick={onClick}
				onKeyDown={onKeyDown}
			>
				<span className={chosen ? "block truncate" : "block truncate text-ink-muted"} lang={chosen?.lang}>
					{chosen ? chosen.label : placeholder}
				</span>
				<span className={`absolute inset-y-0 right-0 flex w-11 items-center justify-center ${disabled ? "opacity-40" : ""}`}>
					<Caret />
				</span>
			</button>
			{/* The listbox is always in the page so aria-controls names it; it is hidden while closed. */}
			<ul id={listId} role="listbox" aria-label={label} hidden={!open} className={choiceListClassName}>
				<ChoiceOptions
					groups={groups}
					leading={none}
					optionId={optionId}
					activeKey={activeKey}
					isSelected={isSelected}
					onPoint={onPoint}
					onPick={onPick}
				/>
			</ul>
		</div>
	)
}
