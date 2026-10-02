import { Fragment } from "react"
import { Caret, ChoiceSeparator, choiceListClassName, choiceRowClassName } from "./ChoiceList.view"
import type { MultiSelectPickerModel, MultiSelectPickerProps } from "./MultiSelectPicker"

export type MultiSelectPickerViewProps = MultiSelectPickerProps & MultiSelectPickerModel

/** A "Pick several" question as a picker dropdown (issue no. 343, REQ-SUB-034). */
export function MultiSelectPickerView({
	fieldId,
	label,
	groups,
	values,
	placeholder,
	describedBy,
	onToggle,
	lockedReason,
	open,
	locked,
	invalid,
	containerRef,
	triggerRef,
	labelId,
	summaryId,
	panelId,
	lockedReasonId,
	chosen,
	onBlur,
	onToggleOpen,
}: MultiSelectPickerViewProps) {
	return (
		<div ref={containerRef} onBlur={onBlur}>
			<span id={labelId} className="block font-sans text-sm font-medium text-ink">
				{label}
			</span>
			<div className="relative">
				{/* eslint-disable-next-line jsx-a11y/role-supports-aria-props -- aria-invalid reports a required picker left empty; dropping it would change the DOM */}
				<button
					ref={triggerRef}
					id={fieldId}
					type="button"
					aria-expanded={open}
					aria-controls={panelId}
					aria-labelledby={`${labelId} ${summaryId}`}
					aria-describedby={describedBy}
					aria-invalid={invalid || undefined}
					onClick={onToggleOpen}
					className="touch-target mt-1 flex w-full items-center justify-between gap-2 rounded border border-rule bg-surface px-3 py-2 text-left font-sans text-ink"
				>
					<span id={summaryId} className={chosen.length > 0 ? "truncate" : "truncate text-ink-muted"}>
						{chosen.length > 0 ? chosen.join(", ") : placeholder}
					</span>
					<Caret />
				</button>
				{open && (
					<ul id={panelId} role="group" aria-labelledby={labelId} className={choiceListClassName}>
						{groups.map((group, index) => (
							<Fragment key={group[0].key}>
								{index > 0 && <ChoiceSeparator />}
								{group.map((option) => (
									<li key={option.key} role="presentation">
										{/* The type-ahead's row and highlight (ChoiceList), on the row pointed at or holding keyboard focus. */}
										<label
											className={`${choiceRowClassName} gap-2 hover:bg-surface-4 hover:shadow-[inset_4px_0_0_var(--color-focus)] has-[:focus-visible]:bg-surface-4 has-[:focus-visible]:shadow-[inset_4px_0_0_var(--color-focus)]`}
										>
											<input
												type="checkbox"
												checked={values.includes(option.key)}
												disabled={locked.includes(option.key)}
												aria-describedby={locked.includes(option.key) && lockedReason ? lockedReasonId : undefined}
												onChange={() => onToggle(option.key)}
											/>
											{option.label}
										</label>
									</li>
								))}
							</Fragment>
						))}
					</ul>
				)}
				{lockedReason && (
					<span id={lockedReasonId} className="sr-only">
						{lockedReason}
					</span>
				)}
			</div>
		</div>
	)
}
