import { ChoiceOptions, choiceListClassName, choiceRowClassName } from "./ChoiceList.view"
import type { TypeAheadFieldModel, TypeAheadFieldProps } from "./TypeAheadField"

export type TypeAheadFieldViewProps = TypeAheadFieldProps & TypeAheadFieldModel

/** A type-ahead question as a picker the form draws (REQ-QB-159, ADR-0140). */
export function TypeAheadFieldView({
	label,
	value,
	placeholder,
	describedBy,
	t,
	fieldId,
	disabled,
	containerRef,
	inputRef,
	listId,
	optionId,
	expanded,
	belowThreshold,
	noMatch,
	shown,
	activeId,
	activeKey,
	onBlur,
	onClick,
	onInputChange,
	onKeyDown,
	isSelected,
	onPoint,
	onListClick,
}: TypeAheadFieldViewProps) {
	return (
		// Presentational: it only takes a click on a listbox row, for the row it reached (REQ-QB-267).
		<div ref={containerRef} role="presentation" className="relative mt-1" onBlur={onBlur} onClick={onListClick}>
			<input
				ref={inputRef}
				id={fieldId}
				type="text"
				role="combobox"
				autoComplete="off"
				aria-autocomplete="list"
				aria-expanded={expanded}
				aria-controls={listId}
				aria-activedescendant={activeId}
				aria-describedby={describedBy}
				className="w-full rounded border border-rule bg-surface py-2 px-3 font-sans text-ink placeholder:text-ink-muted disabled:cursor-not-allowed disabled:bg-surface-2 disabled:text-ink-muted"
				value={value}
				placeholder={placeholder}
				disabled={disabled}
				onClick={onClick}
				onChange={onInputChange}
				onKeyDown={onKeyDown}
			/>
			{/* The listbox is always in the page so aria-controls names it; it is hidden while closed. */}
			<ul id={listId} role="listbox" aria-label={label} hidden={!expanded} className={choiceListClassName}>
				{belowThreshold ? (
					// Presentational, like ChoiceSeparator: not an option, so Up, Down, and Enter pick nothing.
					<li role="presentation" data-hint="" className={`${choiceRowClassName} cursor-default text-ink-muted`}>
						{t("report.typeAhead.typeToSeeChoices")}
					</li>
				) : (
					<ChoiceOptions
						groups={shown}
						optionId={optionId}
						activeKey={activeKey}
						isSelected={isSelected}
						onPoint={onPoint}
					/>
				)}
			</ul>
			{/*
			 * Always in the page, so a change is announced through this polite live
			 * status the moment it happens. The hint is already visible as the
			 * list's own row, so this copy stays screen-reader only; "no match"
			 * has no visible row of its own, so it also draws the floating box.
			 */}
			<p
				role="status"
				className={
					noMatch
						? "absolute left-0 right-0 top-full z-40 mt-1 rounded border border-rule bg-surface px-3 py-2 font-sans text-sm text-ink-muted shadow-lg"
						: "sr-only"
				}
			>
				{noMatch ? t("report.typeAhead.noMatches") : expanded && belowThreshold ? t("report.typeAhead.typeToSeeChoices") : ""}
			</p>
		</div>
	)
}
