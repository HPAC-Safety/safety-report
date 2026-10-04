/*
 * A key is named only in a scenario's Examples cell, as a noun phrase the
 * step reads through its placeholder (CONV-004, #815): "the Enter key", "the
 * down arrow key twice", "the Alt and down arrow keys", "the m key". The step
 * says what the actor does with it ("uses <key>", "closes the lightbox with
 * <key>"); this turns the cell into the presses Playwright makes.
 */
const KEY_NAMES: Record<string, string> = {
	"down arrow": "ArrowDown",
	"up arrow": "ArrowUp",
	"left arrow": "ArrowLeft",
	"right arrow": "ArrowRight",
	"Alt and down arrow": "Alt+ArrowDown",
	"Page Up": "PageUp",
	"Page Down": "PageDown",
}

/** The key presses an Examples cell names, in order. */
export function keyPresses(cell: string): string[] {
	const named = /^the (.+?) keys?( twice)?$/.exec(cell)
	if (!named) throw new Error(`Unknown keys: ${cell}`)
	const key = KEY_NAMES[named[1]] ?? named[1]
	return named[2] ? [key, key] : [key]
}

/** The one key an Examples cell names. */
export function keyPress(cell: string): string {
	const [key, ...more] = keyPresses(cell)
	if (more.length > 0) throw new Error(`One key expected: ${cell}`)
	return key
}
