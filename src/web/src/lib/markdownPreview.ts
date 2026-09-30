/*
 * The public feed's three-line preview of a summary (REQ-MOD-210): the first
 * section's body as plain text, without its heading and without any Markdown
 * characters. A summary with no heading (one written before sections, or by a
 * reviewer) previews whole.
 */

const HEADING = /^ {0,3}#{1,6}(\s|$)/

/** Plain text of a Markdown span: no emphasis marks, list markers, link syntax, or images. */
function plain(markdown: string): string {
	return markdown
		.replace(/!\[[^\]]*\]\([^)]*\)/g, "")
		.replace(/\[([^\]]*)\]\([^)]*\)/g, "$1")
		.replace(/^ {0,3}(?:[-*+]|\d+[.)])\s+/gm, "")
		.replace(/(\*\*|__)(.+?)\1/g, "$2")
		.replace(/(\*|_)(.+?)\1/g, "$2")
		.replace(/`([^`]*)`/g, "$1")
		.replace(/[ \t]+\n/g, "\n")
		.replace(/\n{3,}/g, "\n\n")
		.trim()
}

/** The body of the first section of `markdown`, as plain text. */
export function firstSectionPreview(markdown: string): string {
	const lines = markdown.replace(/\r\n?/g, "\n").split("\n")
	const first = lines.findIndex((line) => HEADING.test(line))
	if (first === -1) return plain(lines.join("\n"))

	const after = lines.slice(first + 1)
	const next = after.findIndex((line) => HEADING.test(line))
	return plain((next === -1 ? after : after.slice(0, next)).join("\n"))
}
