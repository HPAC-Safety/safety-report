#!/usr/bin/env node
// The markup/logic split and the test boundary in src/web/src (ADR-0188).
//
// A component's logic lives in Foo.tsx and its markup in Foo.view.tsx. This
// guard holds the view to being markup, and keeps test code out of anything
// that ships:
//
//   view       every *.view.tsx has a sibling Foo.tsx; calls no hook but
//              useLocale; imports nothing from api/; and touches no
//              localStorage, sessionStorage, fetch, timer or XMLHttpRequest,
//              and holds no module-level let or var;
//   release    no file that is not a test imports a *.test.* file, vitest,
//              @vitest/*, or @testing-library/*;
//   strict     (STRICT below) every component .tsx has its .view.tsx, except
//              main.tsx, routes.tsx and tests.
//
// STRICT is a constant here, not a flag: a rule that holds only where a CI
// command line says so is not a rule (ADR-0073). It stays false while the
// components are split area by area (#756); the last pull request sets it to
// true.
//
// Plain node:fs and regular expressions, no dependency, like
// tools/check-hardcoded-strings.mjs: a pragmatic line-based scan, comments
// blanked first so a line number stays true.
import { readdirSync, readFileSync, statSync } from 'node:fs'
import { join } from 'node:path'
import { fileURLToPath } from 'node:url'

export const ROOT = 'src/web/src'
export const STRICT = false

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
const MODULE_LEVEL_MUTABLE = /^(let|var)\s+\w+/
const MODULE_SPECIFIER = /(?:\bfrom\s*|\bimport\s*\(?\s*|\brequire\s*\(\s*)(["'])([^"']+)\1/g

const isTest = (path) => /\.test\.tsx?$/.test(path)
const isView = (path) => path.endsWith('.view.tsx')
const baseName = (path) => path.slice(path.lastIndexOf('/') + 1)

/** Blanks comments, keeping every newline so line numbers stay true. */
export function stripComments(source) {
	return source
		.replace(/\/\*[\s\S]*?\*\//g, (block) => block.replace(/[^\n]/g, ' '))
		.replace(/(^|[^:])\/\/.*$/gm, (_, lead) => lead)
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

	lines(source).forEach((text, index) => {
		const line = index + 1

		for (const match of text.matchAll(HOOK_CALL)) {
			if (match[1] !== 'useLocale') {
				add(line, `a view calls no hook but useLocale, found ${match[1]}(; move it to the view model in the sibling .tsx`)
			}
		}

		for (const match of text.matchAll(MODULE_SPECIFIER)) {
			if (isApiImport(match[2])) add(line, `a view imports nothing from api/, found "${match[2]}"; fetch in the view model`)
		}

		for (const [pattern, name] of FORBIDDEN_IN_VIEW) {
			if (pattern.test(text)) add(line, `a view does not use ${name}; move it to the view model`)
		}

		if (MODULE_LEVEL_MUTABLE.test(text)) add(line, 'a view holds no module-level mutable state (let or var)')
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
