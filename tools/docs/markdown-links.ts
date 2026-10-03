// Finds the links in a markdown (or C# doc-comment) text, for the tools that
// check and rewrite them.
//
// Dependency-free on purpose, like the other checks beside it. It reads what
// GitHub renders as a link: inline links and images, reference definitions,
// and — when asked — an HTML `href`/`src` attribute. Fenced code blocks, inline
// code spans, and HTML comments render as text, so nothing inside them is a
// link and nothing inside them is returned.

const FENCE = /^ {0,3}(`{3,}|~{3,})/

// [text](target "title") or ![alt](target). The text may hold one level of
// nested brackets; the target may hold one level of balanced parentheses.
const INLINE = /!?\[(?:[^[\]]|\[[^[\]]*\])*\]\(\s*(<[^>\n]*>|[^\s()]*(?:\([^\s()]*\)[^\s()]*)*)(?:\s+(?:"[^"]*"|'[^']*'))?\s*\)/g

// [label]: target
const REFERENCE = /^ {0,3}\[[^\]]+\]:\s*(<[^>\n]*>|\S+)/

const HTML = /\b(?:href|src)\s*=\s*"([^"]*)"/g

const SCHEME = /^[a-z][a-z0-9+.-]*:/i

/** Blanks every character of `text` matched by `pattern`, keeping offsets. */
function blank(text: string, pattern: RegExp): string {
	return text.replace(pattern, (match) => match.replace(/[^\n]/g, ' '))
}

/**
 * Splits a raw link target into its parts. `external` is true for a URL with a
 * scheme (`https:`, `mailto:`) or a protocol-relative `//host` target.
 */
export interface TargetParts {
	raw: string
	path: string
	anchor: string
	query: string
	external: boolean
	trailingSlash: boolean
}

export function splitTarget(raw: string): TargetParts {
	let target = raw.trim()
	if (target.startsWith('<') && target.endsWith('>')) target = target.slice(1, -1)

	const external = SCHEME.test(target) || target.startsWith('//')
	const hash = target.indexOf('#')
	const anchor = hash === -1 ? '' : target.slice(hash + 1)
	let rest = hash === -1 ? target : target.slice(0, hash)
	const question = rest.indexOf('?')
	const query = question === -1 ? '' : rest.slice(question + 1)
	if (question !== -1) rest = rest.slice(0, question)

	return { raw: target, path: rest, anchor, query, external, trailingSlash: rest.endsWith('/') }
}

/**
 * Every link in `text`, in order, as `{ target, line, column, kind }` plus the
 * parts from `splitTarget`. `line` is 1-based; `column` is the 0-based offset
 * of the raw target within that line, so a rewrite can replace it in place.
 */
export type LinkKind = 'link' | 'image' | 'reference' | 'html'

export interface Link extends TargetParts {
	target: string
	line: number
	column: number
	kind: LinkKind
}

export function links(text: string, { html = false }: { html?: boolean } = {}): Link[] {
	const found: Link[] = []
	const visible = blank(text, /<!--[\s\S]*?-->/g)
	const lines = visible.split('\n')

	let fence: string | null = null
	lines.forEach((source, index) => {
		const opening = FENCE.exec(source)
		if (fence) {
			if (opening && opening[1][0] === fence[0] && opening[1].length >= fence.length && source.trim() === opening[1]) fence = null
			return
		}
		if (opening) {
			fence = opening[1]
			return
		}

		// An inline code span renders as text. Blank it, keeping the columns, so
		// a backticked path in link text still leaves the link around it intact.
		const line = blank(source, /(`+)[^`]*?\1/g)
		const record = (raw: string, offset: number, kind: LinkKind): void => {
			if (raw.trim() === '') return
			found.push({ target: raw, line: index + 1, column: offset, kind, ...splitTarget(raw) })
		}

		for (const match of line.matchAll(INLINE)) {
			const raw = match[1]
			const offset = match.index + match[0].indexOf('(', match[0].lastIndexOf(']')) + 1
			record(raw, source.indexOf(raw, offset), match[0].startsWith('!') ? 'image' : 'link')
		}

		const reference = REFERENCE.exec(line)
		if (reference) record(reference[1], source.indexOf(reference[1]), 'reference')

		if (html) {
			for (const match of line.matchAll(HTML)) {
				record(match[1], match.index + match[0].indexOf('"') + 1, 'html')
			}
		}
	})

	return found
}

/**
 * The anchor GitHub gives a heading: its rendered text, lower-cased, with
 * everything but letters, digits, spaces, hyphens, and underscores removed,
 * and spaces turned into hyphens.
 */
export function slug(heading: string): string {
	const text = heading
		.replace(/!?\[([^\]]*)\]\([^)]*\)/g, '$1')
		.replace(/<[^>]+>/g, '')
		.replace(/[`*]/g, '')
		.trim()
		.toLowerCase()
	return text.replace(/[^\p{L}\p{N}\s_-]/gu, '').replace(/\s/g, '-')
}

/** Every anchor a markdown text offers: one per heading, plus explicit `id`/`name` anchors. */
export function anchors(text: string): Set<string> {
	const offered = new Set<string>()
	const counts = new Map<string, number>()
	let fence: string | null = null
	for (const line of text.split('\n')) {
		const opening = FENCE.exec(line)
		if (fence) {
			if (opening && opening[1][0] === fence[0] && opening[1].length >= fence.length && line.trim() === opening[1]) fence = null
			continue
		}
		if (opening) {
			fence = opening[1]
			continue
		}

		const heading = /^ {0,3}#{1,6}\s+(.*?)\s*#*\s*$/.exec(line)
		if (heading) {
			const base = slug(heading[1])
			const seen = counts.get(base) ?? 0
			counts.set(base, seen + 1)
			offered.add(seen === 0 ? base : `${base}-${seen}`)
		}
		for (const match of line.matchAll(/<a\s+(?:id|name)\s*=\s*"([^"]+)"/g)) offered.add(match[1])
	}
	return offered
}
