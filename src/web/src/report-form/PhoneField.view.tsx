import { callingCodeOf, flagOf, phonePlaceholder } from "../lib/phoneNumber"
import type { PhoneFieldModel, PhoneFieldProps } from "./PhoneField"

export type PhoneFieldViewProps = PhoneFieldProps & PhoneFieldModel

/**
 * A phone question (REQ-SUB-085..091, ADR-0137). The country picker is a native
 * `select`, laid invisibly over what it shows, so it keeps the platform's own
 * keyboard and screen-reader behaviour.
 */
export function PhoneFieldView({ fieldId, describedBy, t, value, country, countries, onInputChange, onCountryChange }: PhoneFieldViewProps) {
	return (
		<div className="mt-1 flex gap-2">
			<div className="touch-target relative flex shrink-0 items-center rounded border border-rule bg-surface px-3 font-sans text-ink focus-within:outline focus-within:outline-2 focus-within:outline-brand-700">
				<span id={`${fieldId}-country-shown`} aria-hidden="true" className="whitespace-nowrap">
					{flagOf(country)} +{callingCodeOf(country)}
				</span>
				<select
					id={`${fieldId}-country`}
					aria-label={t("report.phone.country")}
					className="absolute inset-0 h-full w-full cursor-pointer opacity-0"
					value={country}
					onChange={onCountryChange}
				>
					{countries.map((entry) => (
						<option key={entry.code} value={entry.code}>
							{`${entry.flag} ${entry.name} (+${entry.callingCode})`}
						</option>
					))}
				</select>
			</div>
			<input
				id={fieldId}
				type="tel"
				inputMode="tel"
				autoComplete="tel"
				className="w-full rounded border border-rule bg-surface px-3 py-2 font-sans text-ink placeholder:text-ink-muted"
				value={value}
				aria-describedby={describedBy}
				placeholder={phonePlaceholder(country)}
				onChange={onInputChange}
			/>
		</div>
	)
}
