import { CalendarDateField } from "./CalendarDateField"
import type { DateFieldModel, DateFieldProps } from "./DateField"

export type DateFieldViewProps = DateFieldProps & DateFieldModel

/**
 * A date question (REQ-SUB-098..107, ADR-0138): the native date input on a
 * touch device, otherwise a text field with a one-month calendar popover.
 */
export function DateFieldView({ coarse, max, ...props }: DateFieldViewProps) {
	if (coarse) {
		const { fieldId, className, describedBy, value, onChange } = props
		return (
			<input
				id={fieldId}
				type="date"
				className={className}
				value={value}
				max={max}
				aria-describedby={describedBy}
				onChange={(event) => onChange(event.target.value)}
			/>
		)
	}
	return <CalendarDateField {...props} />
}
