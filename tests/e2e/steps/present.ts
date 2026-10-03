/**
 * The value, or a failure that names what the step expected to find. A step
 * reads a stub or a measurement it set up a line earlier; when that is missing
 * the test should fail there, in words, rather than on a property of `null`.
 * Not for code that runs in the page (`page.evaluate`): it cannot see imports.
 */
export function present<T>(value: T | null | undefined, what = "a value"): T {
	if (value === null || value === undefined) throw new Error(`Expected ${what} to be present.`)
	return value
}
