#!/usr/bin/env node
// Every tracked markdown file declares what it is (ADR-0087).
//
// A document's kind used to be inferred from its path: you knew
// `.spec/decisions/ADR-0047-*.md` was a decision record because you knew the
// repository. This asserts the frontmatter that says so in the file itself,
// with the keys each kind needs.
//
// Dependency-free on purpose, like `check-locales.ts` beside it. The
// frontmatter this repository writes is a flat block of scalars and short
// lists, so a full YAML parser would be a dependency bought to read four
// lines. Anything nested is reported rather than silently accepted.
//
// The exit code is the contract.
import { execFileSync } from 'node:child_process'
import { lstatSync, readFileSync, readdirSync } from 'node:fs'
import { join, relative, sep } from 'node:path'

import { CONVENTIONS, DECISIONS, LESSONS } from '../spec/spec-paths.ts'
import { isMain } from '../lib/actions.ts'

const ROOT = process.cwd()

// Runtime model payload. A prompt version is immutable once it has been used
// for a summary or a translation, and frontmatter would either change what the
// model receives or put a stripping parser on the privacy-sensitive path.
// ADR-0087; the translation prompt beside the term list, ADR-0179.
export const EXEMPT_PREFIXES = ['src/HpacSafety.Worker/Prompts/', 'locales/translation-prompt.']

export const CORE_KEYS = ['title', 'description', 'type']

export const TYPES = ['adr', 'spec', 'guide', 'readme', 'lesson', 'convention', 'instructions', 'template']

// Keys a `type` adds on top of the core three.
const EXTRA_KEYS: Readonly<Partial<Record<string, readonly string[]>>> = {
	adr: ['status', 'date', 'decision-makers', 'keywords'],
	spec: ['area'],
	// `kind` decides what a lesson owes upstream (ADR-0192).
	lesson: ['date', 'issue', 'status', 'kind'],
	convention: ['status', 'date'],
}

// A file a third-party loader parses keeps that loader's standard and nothing
// else, so its type comes from its path rather than from a key we invented.
export interface VendorShape {
	match: (path: string) => boolean
	kind: string
	keys: readonly string[]
}

const VENDOR_SHAPES: readonly VendorShape[] = [
	{ match: (path) => path.startsWith('skills/') && path.endsWith('/SKILL.md'), kind: 'skill', keys: ['name', 'description'] },
]

// Where the path, not the author, decides the `type`.
const TYPE_BY_PATH: readonly { match: (path: string) => boolean; type: string }[] = [
	{ match: (path) => path.startsWith(`${DECISIONS}/`) && /\/ADR-\d{4}-.+\.md$/.test(path), type: 'adr' },
	{ match: (path) => path.startsWith(`${LESSONS}/`) && /\/\d{4}-.+\.md$/.test(path), type: 'lesson' },
	{ match: (path) => path.startsWith(`${CONVENTIONS}/`) && /\/CONV-\d{3}-.+\.md$/.test(path), type: 'convention' },
]

// The values a key may hold, by type. An ADR's status is checked against its
// own status line by adr-numbers.ts; a lesson has no status line, so here.
const VALUES: Readonly<Partial<Record<string, Readonly<Record<string, readonly string[]>>>>> = {
	lesson: { status: ['accepted', 'superseded'], kind: ['product', 'process', 'incident'] },
	convention: { status: ['accepted', 'superseded'] },
}

// Where the specification lived before it moved to .spec/ (ADR-0183). A file
// here comes from a branch not yet rebased past the move.
const MOVED = /^(docs\/decisions|docs\/lessons|features)\//

/**
 * Reads the leading `---` block as a set of top-level keys. Returns the keys
 * in order with their inline values, or an error describing why the block
 * could not be read. Values are only inspected far enough to tell "present and
 * non-empty" from "declared and left blank".
 */
export interface FrontmatterEntry {
	key: string
	value: string
	nested: boolean
}

export type Frontmatter = { entries: FrontmatterEntry[]; error?: undefined } | { entries?: undefined; error: string }

export function parseFrontmatter(text: string): Frontmatter {
	const lines = text.split('\n')
	if (lines[0] !== '---') return { error: 'no YAML frontmatter block — the file must open with a --- line' }

	const end = lines.indexOf('---', 1)
	if (end === -1) return { error: 'the frontmatter block opens with --- but never closes' }

	const entries: FrontmatterEntry[] = []
	for (let index = 1; index < end; index += 1) {
		const line = lines[index]
		if (line.trim() === '' || line.trimStart().startsWith('#')) continue
		if (line.includes('\t')) return { error: `line ${index + 1} contains a tab; YAML forbids tabs in indentation` }

		// An indented line continues the entry above it: a block list item, or
		// a nested map. Either way the key is already recorded.
		if (/^\s/.test(line)) {
			if (entries.length === 0) return { error: `line ${index + 1} is indented but follows no key` }
			const previous = entries[entries.length - 1]
			if (/^\s*-\s+\S/.test(line)) previous.value = `${previous.value} ${line.trim()}`.trim()
			else previous.nested = true
			continue
		}

		const separator = line.indexOf(':')
		if (separator === -1) return { error: `line ${index + 1} is not a "key: value" pair: ${line}` }

		entries.push({ key: line.slice(0, separator).trim(), value: line.slice(separator + 1).trim(), nested: false })
	}

	return { entries }
}

/** The vendor shape or path-assigned type that applies to `path`, if any. */
export function expectationFor(path: string): { vendor?: VendorShape; type?: string } {
	const vendor = VENDOR_SHAPES.find((shape) => shape.match(path))
	if (vendor) return { vendor }

	const assigned = TYPE_BY_PATH.find((rule) => rule.match(path))
	return assigned ? { type: assigned.type } : {}
}

export function isExempt(path: string): boolean {
	return EXEMPT_PREFIXES.some((prefix) => path.startsWith(prefix))
}

/** Every problem with one file's frontmatter, as messages naming the fix. */
export function checkFile(path: string, text: string): string[] {
	if (MOVED.test(path)) {
		return [`${path}: the specification moved to .spec/ (ADR-0183) — rebase onto main and move this file under .spec/`]
	}

	const parsed = parseFrontmatter(text)
	if (parsed.error !== undefined) return [`${path}: ${parsed.error}`]
	const { entries } = parsed

	const problems: string[] = []
	const seen = new Set<string>()
	for (const entry of entries) {
		if (seen.has(entry.key)) problems.push(`${path}: "${entry.key}" is declared twice`)
		seen.add(entry.key)
	}

	const value = (key: string): FrontmatterEntry | undefined => entries.find((entry) => entry.key === key)
	const require = (keys: readonly string[], because: string): void => {
		for (const key of keys) {
			const found = value(key)
			if (!found) problems.push(`${path}: missing "${key}" — ${because}`)
			else if (found.value === '' && !found.nested) problems.push(`${path}: "${key}" is declared but empty`)
		}
	}

	const { vendor, type: assignedType } = expectationFor(path)

	if (vendor) {
		require(vendor.keys, `a ${vendor.kind} carries ${vendor.keys.map((key) => `"${key}"`).join(', ')}, the shape its loader expects (ADR-0087)`)
		const declaredType = value('type')
		if (declaredType) {
			problems.push(`${path}: a ${vendor.kind} does not carry "type" — its kind comes from its path, and an unrecognized key would be a private extension to somebody else's format (ADR-0087)`)
		}
		return problems
	}

	require(CORE_KEYS, 'every markdown file states its title, description, and type (ADR-0087)')

	const declaredType = value('type')?.value
	if (declaredType && !TYPES.includes(declaredType)) {
		problems.push(`${path}: "type: ${declaredType}" is not one of ${TYPES.join(', ')}`)
		return problems
	}

	if (assignedType && declaredType && declaredType !== assignedType) {
		problems.push(`${path}: "type: ${declaredType}" contradicts its location — a file here is "${assignedType}"`)
	}

	const effectiveType = assignedType ?? declaredType
	const extras = effectiveType === undefined ? undefined : EXTRA_KEYS[effectiveType]
	if (extras) require(extras, `a "${effectiveType}" adds ${extras.map((key) => `"${key}"`).join(', ')}`)

	for (const [key, allowed] of Object.entries((effectiveType === undefined ? undefined : VALUES[effectiveType]) ?? {})) {
		const found = value(key)?.value
		if (found && !allowed.includes(found)) problems.push(`${path}: "${key}: ${found}" is not one of ${allowed.join(', ')}`)
	}

	return problems
}

/**
 * Tracked markdown, newest checkout or not. Uses `git ls-files` so the set is
 * exactly what is committed — generated and ignored trees never appear — and
 * falls back to a walk when there is no repository, which is how the tests run
 * against a throwaway directory.
 */
export function collectMarkdownFiles(root: string = ROOT): string[] {
	try {
		const listed = execFileSync('git', ['-C', root, 'ls-files', '-z', '*.md', '*.mdc'], { encoding: 'utf8' })
		return listed.split('\0').filter(Boolean).sort()
	} catch {
		return walk(root, root).sort()
	}
}

function walk(directory: string, root: string): string[] {
	const skip = new Set(['.git', 'node_modules', 'obj', 'bin', 'graphify-out', '.skillfile', '.claude'])
	return readdirSync(directory, { withFileTypes: true }).flatMap((entry) => {
		if (skip.has(entry.name)) return []
		const full = join(directory, entry.name)
		if (entry.isDirectory()) return walk(full, root)
		if (!entry.name.endsWith('.md') && !entry.name.endsWith('.mdc')) return []
		return [relative(root, full).split(sep).join('/')]
	})
}

/**
 * Checks `root` and reports the way the command line does, without exiting, so
 * a test can exercise both outcomes in-process. Returns the exit code.
 */
export function main(root: string = ROOT, paths: readonly string[] = []): number {
	const files = paths.length > 0 ? [...paths].sort() : collectMarkdownFiles(root)
	const checked: string[] = []
	const problems: string[] = []

	for (const path of files) {
		if (isExempt(path)) continue
		// A symlink resolves to a file already checked on its own path;
		// `CLAUDE.md` and `AGENTS.md` are one document with one frontmatter block.
		if (lstatSync(join(root, path)).isSymbolicLink()) continue

		checked.push(path)
		problems.push(...checkFile(path, readFileSync(join(root, path), 'utf8')))
	}

	if (problems.length > 0) {
		for (const problem of problems) {
			const [file, ...rest] = problem.split(': ')
			console.error(`::error file=${file}::${rest.join(': ')}`)
		}
		return 1
	}

	console.log(`${checked.length} markdown file(s) checked. Every one declares what it is.`)
	return 0
}

// Given paths, checks only those — the pre-commit hook passes the staged
// markdown so a commit pays for its own files and nothing else.
const runAsCommand = isMain(import.meta.url)
if (runAsCommand) process.exit(main(ROOT, process.argv.slice(2)))
