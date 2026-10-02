const UNITS = ["byte", "kilobyte", "megabyte", "gigabyte"] as const

/** A byte count in the reader's language: "2.4 MB", "2,4 Mo". */
export function byteSizeLabel(bytes: number, locale: string): string {
	let value = bytes
	let unit = 0
	while (value >= 1024 && unit !== UNITS.length - 1) {
		value /= 1024
		unit += 1
	}
	return new Intl.NumberFormat(locale, {
		style: "unit",
		unit: UNITS[unit],
		unitDisplay: "short",
		maximumFractionDigits: unit === 0 ? 0 : 1,
	}).format(value)
}
