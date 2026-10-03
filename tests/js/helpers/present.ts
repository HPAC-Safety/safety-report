/**
 * The value, or a failure that names what was expected. A test reads a fixture
 * or a captured value it set up a line earlier; when that is missing it should
 * fail there, in words, rather than on a property of `undefined`.
 */
export function present<T>(value: T | null | undefined, what = 'a value'): T {
	if (value === null || value === undefined) throw new Error(`Expected ${what} to be present.`)
	return value
}
