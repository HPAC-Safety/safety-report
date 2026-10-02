#!/usr/bin/env node
// The markup/logic split and the test boundary in src/web/src (ADR-0188).
//
// A component's logic lives in Foo.tsx and its markup in Foo.view.tsx. This
// guard holds the view to being markup, and keeps test code out of anything
// that ships:
//
//   view       every *.view.tsx has a sibling Foo.tsx; calls no hook but
//              useLocale; imports no value from api/ (a type-only import is
//              erased at compile time and allowed); imports no runtime value
//              from its own sibling Foo.tsx (that is a runtime circular
//              import: a shared value lives in a module both import); and
//              touches no localStorage, sessionStorage, fetch, timer or
//              XMLHttpRequest, and holds no module-level let or var. Comments
//              and string literals are not code: a catalogue key such as
//              "report.privacy.localStorage" is not a use of localStorage;
//   ignore     a v8 or istanbul ignore hint, in any file, has a comment on
//              the line directly above it giving the reason (ADR-0188);
//   release    no file that is not a test imports a *.test.* file, vitest,
//              @vitest/*, or @testing-library/*;
//   strict     (STRICT below) every component .tsx has its .view.tsx, except
//              main.tsx, routes.tsx and tests.
//
// STRICT is a constant here, not a flag: a rule that holds only where a CI
// command line says so is not a rule (ADR-0073). It was false while the
// components were split area by area (#756); the last pull request turned it on.
//
// Plain node:fs and regular expressions, no dependency, like
// tools/check-hardcoded-strings.mjs: a pragmatic line-based scan, comments
// blanked first so a line number stays true.
import { readdirSync, readFileSync, statSync } from 'node:fs'
import { join } from 'node:path'
import { fileURLToPath } from 'node:url'

export const ROOT = 'src/web/src'
export const STRICT = true

const EXEMPT_FROM_STRICT = new Set(['main.tsx', 'routes.tsx'])

const FORBIDDEN_IN_VIEW = [
	[/\blocalStorage\b/, 'localStorage'],
	[/\bsessionStorage\b/, 'sessionStorage'],
	[/\bfetch\s*\(/, 'fetch('],
	[/\bsetTimeout\b/, 'setTimeout'],
	[/\bsetInterval\b/, 'setInterval'],
	[/\bXMLHttpRequest\b/, 'XMLHttpRequest'],
]

const HOOK_CALL = /\b(use[A-Z]\w*)\s*(?:<[^()]*?>)?\s*\(/g
const IGNORE_HINT = /\b(?:v8|istanbul)\s+ignore\b/
const MODULE_LEVEL_MUTABLE = /^(let|var)\s+\w+/
const MODULE_SPECIFIER = /(?:\bfrom\s*|\bimport\s*\(?\s*|\brequire\s*\(\s*)(["'])([^"']+)\1/g

const isTest = (path) => /\.test\.tsx?$/.test(path)
const isView = (path) => path.endsWith('.view.tsx')
const baseName = (path) => path.slice(path.lastIndexOf('/') + 1)

/**
 * Walks `source` once, blanking comments (and, when `strings` is set, the text
 * of string and template literals, keeping the quotes and any `${…}`
 * expression), and keeping every newline so a line number stays true. A quote
 * in JSX text ends at the line's end rather than running on.
 */
function scrub(source, { strings }) {
	let out = ''
	let i = 0
	const templates = [] // brace depth of each open `${` inside a template
	let depth = 0
	const blank = (text) => text.replace(/[^\n]/g, ' ')

	function template(resuming) {
		if (!resuming) {
			out += '`'
			i += 1
		}
		while (i < source.length) {
			const c = source[i]
			if (c === '\\') {
				out += strings ? blank(source.slice(i, i + 2)) : source.slice(i, i + 2)
				i += 2
			} else if (c === '`') {
				out += '`'
				i += 1
				return
			} else if (c === '$' && source[i + 1] === '{') {
				out += '${'
				i += 2
				templates.push(depth)
				depth += 1
				return
			} else {
				out += strings ? blank(c) : c
				i += 1
			}
		}
	}

	while (i < source.length) {
		const c = source[i]
		const next = source[i + 1]
		if (c === '/' && next === '/') {
			const end = source.indexOf('\n', i)
			const stop = end === -1 ? source.length : end
			out += blank(source.slice(i, stop))
			i = stop
		} else if (c === '/' && next === '*') {
			const end = source.indexOf('*/', i + 2)
			const stop = end === -1 ? source.length : end + 2
			out += blank(source.slice(i, stop))
			i = stop
		} else if (c === '"' || c === "'") {
			let j = i + 1
			while (j < source.length && source[j] !== c && source[j] !== '\n') j += source[j] === '\\' ? 2 : 1
			const closed = source[j] === c
			const body = source.slice(i + 1, j)
			out += c + (strings ? blank(body) : body) + (closed ? c : '')
			i = closed ? j + 1 : j
		} else if (c === '`') {
			template()
		} else {
			if (c === '{') depth += 1
			if (c === '}') {
				depth -= 1
				if (templates.length > 0 && templates.at(-1) === depth) {
					templates.pop()
					out += '}'
					i += 1
					template(true)
					continue
				}
			}
			out += c
			i += 1
		}
	}
	return out
}

/** Blanks comments, keeping every newline so line numbers stay true. */
export function stripComments(source) {
	return scrub(source, { strings: false })
}

/** Blanks comments and the text of string literals, keeping line numbers true. */
export function stripCommentsAndStrings(source) {
	return scrub(source, { strings: true })
}

const IMPORT_STATEMENT = /\bimport\s+(type\s+)?([\w$\s,{}*]*?)\s*\bfrom\s*(["'])([^"']+)\3/g
const REEXPORT_STATEMENT = /\bexport\s+(type\s+)?(\{[^}]*\}|\*(?:\s+as\s+[\w$]+)?)\s*from\s*(["'])([^"']+)\3/g
const SIDE_EFFECT_IMPORT = /\bimport\s*(["'])([^"']+)\1/g
const DYNAMIC_IMPORT = /(?:\bimport\s*\(\s*|\brequire\s*\(\s*)(["'])([^"']+)\1/g

/** True when every name an import clause brings in is a type, so it is erased at compile time. */
function clauseIsTypeOnly(clause) {
	const braces = clause.match(/\{([^}]*)\}/)
	if (!braces) return false
	const outside = clause.replace(braces[0], '').replace(/[,\s]/g, '')
	if (outside !== '') return false
	const names = braces[1].split(',').map((name) => name.trim()).filter(Boolean)
	return names.length > 0 && names.every((name) => /^type\s/.test(name))
}

/** Every module a source imports or re-exports from, with its line and whether only types come in. */
export function importsOf(source) {
	const code = stripComments(source)
	const found = []
	const lineOf = (index) => code.slice(0, index).split('\n').length
	for (const match of code.matchAll(IMPORT_STATEMENT)) {
		found.push({ line: lineOf(match.index), specifier: match[4], typeOnly: Boolean(match[1]) || clauseIsTypeOnly(match[2]) })
	}
	for (const match of code.matchAll(REEXPORT_STATEMENT)) {
		found.push({ line: lineOf(match.index), specifier: match[4], typeOnly: Boolean(match[1]) || clauseIsTypeOnly(match[2]) })
	}
	for (const match of code.matchAll(SIDE_EFFECT_IMPORT)) {
		found.push({ line: lineOf(match.index), specifier: match[2], typeOnly: false })
	}
	for (const match of code.matchAll(DYNAMIC_IMPORT)) {
		found.push({ line: lineOf(match.index), specifier: match[2], typeOnly: false })
	}
	return found
}

function isApiImport(specifier) {
	return specifier.split('/').includes('api')
}

function isTestOnlyImport(specifier) {
	return (
		/\.test(\.[cm]?[jt]sx?)?$/.test(specifier) ||
		specifier === 'vitest' ||
		specifier.startsWith('vitest/') ||
		specifier.startsWith('@vitest/') ||
		specifier.startsWith('@testing-library/')
	)
}

function lines(source) {
	return stripComments(source).split('\n')
}

/** The view rules for one *.view.tsx. */
export function checkView(path, source) {
	const violations = []
	const add = (line, message) => violations.push({ file: path, line, message })

	const sibling = baseName(path).replace(/\.view\.tsx$/, '')
	const ownLogic = new RegExp(`^\\./${sibling}(\\.tsx)?$`)
	for (const { line, specifier, typeOnly } of importsOf(source)) {
		if (isApiImport(specifier) && !typeOnly) {
			add(line, `a view imports no value from api/, found "${specifier}"; fetch in the view model (a type-only import is allowed)`)
		}
		if (ownLogic.test(specifier) && !typeOnly) {
			add(line, `a view imports no runtime value from its own ${sibling}.tsx, found "${specifier}": that is a runtime circular import; put the shared value in a module both import`)
		}
	}

	stripCommentsAndStrings(source)
		.split('\n')
		.forEach((text, index) => {
			const line = index + 1

			for (const match of text.matchAll(HOOK_CALL)) {
				if (match[1] !== 'useLocale') {
					add(line, `a view calls no hook but useLocale, found ${match[1]}(; move it to the view model in the sibling .tsx`)
				}
			}

			for (const [pattern, name] of FORBIDDEN_IN_VIEW) {
				if (pattern.test(text)) add(line, `a view does not use ${name}; move it to the view model`)
			}

			if (MODULE_LEVEL_MUTABLE.test(text)) add(line, 'a view holds no module-level mutable state (let or var)')
		})

	return violations
}

/** A coverage-ignore hint needs a comment giving the reason on the line directly above (ADR-0188). */
export function checkIgnoreHints(path, source) {
	const violations = []
	const raw = source.split('\n')
	raw.forEach((text, index) => {
		if (!IGNORE_HINT.test(text)) return
		const above = (raw[index - 1] ?? '').trim()
		// The comment directly above: one // line, or the whole block ending there.
		let comment = above
		if (above.endsWith('*/')) {
			let start = index - 1
			while (start > 0 && !raw[start].includes('/*')) start -= 1
			comment = raw.slice(start, index).join('\n')
		}
		const isComment = above.startsWith('//') || above.startsWith('*') || above.startsWith('/*') || above.endsWith('*/')
		const reason = comment
			.split('\n')
			.map((text) => text.trim().replace(/^(\/\/+|\/\*+|\*+\/?)/, '').replace(/\*\/$/, '').trim())
			.join(' ')
			.trim()
		if (!isComment || reason === '' || IGNORE_HINT.test(comment)) {
			violations.push({
				file: path,
				line: index + 1,
				message: 'a coverage ignore hint gives its reason in a comment on the line directly above, and only for an unreachable defensive branch',
			})
		}
	})
	return violations
}

/** No shipped file imports test code. */
export function checkReleaseImports(path, source) {
	const violations = []
	lines(source).forEach((text, index) => {
		for (const match of text.matchAll(MODULE_SPECIFIER)) {
			if (isTestOnlyImport(match[2])) {
				violations.push({
					file: path,
					line: index + 1,
					message: `test code is never part of a release, but this imports "${match[2]}"; only a *.test.ts(x) file may`,
				})
			}
		}
	})
	return violations
}

/**
 * Every violation in `files`, a Map of repository path to source. `strict`
 * defaults to STRICT.
 */
export function checkTree(files, { strict = STRICT } = {}) {
	const violations = []
	const paths = new Set(files.keys())

	for (const [path, source] of files) {
		if (!isTest(path)) violations.push(...checkReleaseImports(path, source))
		violations.push(...checkIgnoreHints(path, source))

		if (isView(path)) {
			if (!paths.has(path.replace(/\.view\.tsx$/, '.tsx'))) {
				violations.push({ file: path, line: 1, message: `a view has a sibling ${baseName(path).replace(/\.view\.tsx$/, '.tsx')} holding its logic; none exists` })
			}
			violations.push(...checkView(path, source))
		} else if (strict && path.endsWith('.tsx') && !isTest(path) && !EXEMPT_FROM_STRICT.has(baseName(path))) {
			if (!paths.has(path.replace(/\.tsx$/, '.view.tsx'))) {
				violations.push({ file: path, line: 1, message: `every component has a ${baseName(path).replace(/\.tsx$/, '.view.tsx')} holding its markup; none exists` })
			}
		}
	}

	return violations.sort((a, b) => a.file.localeCompare(b.file) || a.line - b.line)
}

export function collectFiles(dir) {
	const files = new Map()
	for (const entry of readdirSync(dir)) {
		const path = join(dir, entry)
		if (statSync(path).isDirectory()) {
			for (const [file, source] of collectFiles(path)) files.set(file, source)
		} else if (/\.tsx?$/.test(path)) {
			files.set(path.split('\\').join('/'), readFileSync(path, 'utf8'))
		}
	}
	return files
}

export function main(root = ROOT, options = {}) {
	const files = collectFiles(root)
	const violations = checkTree(files, options)
	for (const { file, line, message } of violations) console.error(`${file}:${line}: ${message}`)
	if (violations.length > 0) {
		console.error(`check-component-split: ${violations.length} violation(s)`)
		return 1
	}
	const views = [...files.keys()].filter(isView).length
	console.log(`check-component-split: ${views} view(s) and ${files.size} file(s) in ${root} follow the split`)
	return 0
}

if (process.argv[1] === fileURLToPath(import.meta.url)) process.exit(main())
