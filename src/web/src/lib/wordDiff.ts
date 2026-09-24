/** One run of a word-level difference between two texts. */
export interface DiffPart {
	kind: "same" | "removed" | "added"
	text: string
}

/*
 * A word-level difference by longest common subsequence, for showing a reviewer
 * what a proposed translation would change (ADR-0106). Whitespace is kept with
 * the word before it, so joining every part of one side rebuilds that side.
 * Summaries are a few paragraphs, so the quadratic table is fine.
 */
export function wordDiff(before: string, after: string): DiffPart[] {
	const a = tokens(before)
	const b = tokens(after)
	const table: number[][] = Array.from({ length: a.length + 1 }, () => Array.from({ length: b.length + 1 }, () => 0))

	for (let i = a.length - 1; i >= 0; i--) {
		for (let j = b.length - 1; j >= 0; j--) {
			table[i][j] = a[i] === b[j] ? table[i + 1][j + 1] + 1 : Math.max(table[i + 1][j], table[i][j + 1])
		}
	}

	const parts: DiffPart[] = []
	const push = (kind: DiffPart["kind"], text: string) => {
		const last = parts[parts.length - 1]
		if (last && last.kind === kind) last.text += text
		else parts.push({ kind, text })
	}

	let i = 0
	let j = 0
	while (i < a.length && j < b.length) {
		if (a[i] === b[j]) {
			push("same", a[i])
			i++
			j++
		} else if (table[i + 1][j] >= table[i][j + 1]) {
			push("removed", a[i++])
		} else {
			push("added", b[j++])
		}
	}
	while (i < a.length) push("removed", a[i++])
	while (j < b.length) push("added", b[j++])

	return parts
}

function tokens(text: string): string[] {
	return text.match(/\S+\s*|\s+/g) ?? []
}
