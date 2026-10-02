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
function blank(text, pattern) {
	return text.replace(pattern, (match) => match.replace(/[^\n]/g, ' '))
}

/**
 * Splits a raw link target into its parts. `external` is true for a URL with a
 * scheme (`https:`, `mailto:`) or a protocol-relative `//host` target.
 */
export function splitTarget(raw) {
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
export function links(text, { html = false } = {}) {
	const found = []
	const visible = blank(text, /<!--[\s\S]*?-->/g)
	const lines = visible.split('\n')

	let fence = null
	lines.forEach((source, index) => {
		const opening = source.match(FENCE)
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
		const record = (raw, offset, kind) => {
			if (raw.trim() === '') return
			found.push({ target: raw, line: index + 1, column: offset, kind, ...splitTarget(raw) })
		}

		for (const match of line.matchAll(INLINE)) {
			const raw = match[1]
			const offset = match.index + match[0].indexOf('(', match[0].lastIndexOf(']')) + 1
			record(raw, source.indexOf(raw, offset), match[0].startsWith('!') ? 'image' : 'link')
		}

		const reference = line.match(REFERENCE)
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
export function slug(heading) {
	const text = heading
		.replace(/!?\[([^\]]*)\]\([^)]*\)/g, '$1')
		.replace(/<[^>]+>/g, '')
		.replace(/[`*]/g, '')
		.trim()
		.toLowerCase()
	return text.replace(/[^\p{L}\p{N}\s_-]/gu, '').replace(/\s/g, '-')
}

/** Every anchor a markdown text offers: one per heading, plus explicit `id`/`name` anchors. */
export function anchors(text) {
	const offered = new Set()
	const counts = new Map()
	let fence = null
	for (const line of text.split('\n')) {
		const opening = line.match(FENCE)
		if (fence) {
			if (opening && opening[1][0] === fence[0] && opening[1].length >= fence.length && line.trim() === opening[1]) fence = null
			continue
		}
		if (opening) {
			fence = opening[1]
			continue
		}

		const heading = line.match(/^ {0,3}#{1,6}\s+(.*?)\s*#*\s*$/)
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
