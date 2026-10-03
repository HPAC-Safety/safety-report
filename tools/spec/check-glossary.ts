#!/usr/bin/env node
// The scenarios and area READMEs use the glossary's words (CONV-003, #815).
//
// .spec/glossary.md is the one source: every table whose header has a
// "Banned in scenarios" column lists, per canonical term, the synonyms that
// may not appear, and the areas exempt from them. This reads those tables and
// fails any banned synonym in:
//
//   .spec/features/<area>/<area>.feature  every line but tags, comments, doc
//                                         strings, and table rows: step text,
//                                         names, and descriptions;
//   .spec/features/<area>/README.md       the prose, without frontmatter,
//                                         fenced code, or link targets.
//
// Quoted "literals", `code spans`, and <placeholders> are skipped: they are
// interface copy, data, or names, not vocabulary.
//
//   node tools/spec/check-glossary.ts
//
// Dependency-free, like every other tool in this directory. The exit code is
// the contract.
import { existsSync, readFileSync, readdirSync, statSync } from 'node:fs'
import { join } from 'node:path'

import { FEATURES, GLOSSARY } from './spec-paths.ts'
import { isMain } from '../lib/actions.ts'

const ROOT = process.cwd()

/** One canonical term and what it bans. */
export interface Entry {
	term: string
	banned: RegExp[]
	exempt: string[]
}

/** One banned synonym found in a file. */
export interface Violation {
	file: string
	line: number
	found: string
	term: string
}

const BANNED_COLUMN = 'Banned in scenarios'

/** The cells of one Markdown table row; `\|` is a literal bar, not a separator. */
export function cells(row: string): string[] {
	const inner = row.trim().replace(/^\|/, '').replace(/(?<!\\)\|$/, '')
	return inner.split(/(?<!\\)\|/).map((cell) => cell.trim().replaceAll('\\|', '|'))
}

/** Every `backticked` item in a cell. */
function items(cell: string): string[] {
	return [...cell.matchAll(/`([^`]+)`/g)].map((match) => match[1])
}

/**
 * The matcher one banned item compiles to: `/pattern/flags` is a regular
 * expression, case as written; anything else is a phrase matched as whole
 * words, ignoring case.
 */
export function matcher(item: string): RegExp {
	const literal = item.match(/^\/(.+)\/([a-z]*)$/)
	if (literal) {
		const [, source, flags] = literal
		return new RegExp(source, flags.includes('g') ? flags : `${flags}g`)
	}
	const phrase = item.replace(/[.*+?^${}()|[\]\\]/g, '\\$&').replace(/\s+/g, '\\s+')
	return new RegExp(`\\b${phrase}\\b`, 'gi')
}

/** Every entry of every glossary table that has a banned column. */
export function readGlossary(source: string): { entries: Entry[]; problems: string[] } {
	const entries: Entry[] = []
	const problems: string[] = []
	let columns: { term: number; banned: number; exempt: number } | null = null

	for (const [index, raw] of source.split('\n').entries()) {
		const line = raw.trim()
		if (!line.startsWith('|')) {
			columns = null
			continue
		}
		const row = cells(line)
		if (columns === null) {
			const banned = row.indexOf(BANNED_COLUMN)
			columns = banned === -1 ? { term: -1, banned: -1, exempt: -1 } : { term: 0, banned, exempt: row.indexOf('Exempt areas') }
			continue
		}
		if (columns.banned === -1 || row.every((cell) => /^:?-+:?$/.test(cell))) continue

		const term = row[columns.term].replace(/\*\*/g, '').trim()
		const banned: RegExp[] = []
		for (const item of items(row[columns.banned] ?? '')) {
			try {
				banned.push(matcher(item))
			} catch (error) {
				problems.push(`${GLOSSARY}:${index + 1}: ${term}: \`${item}\` is not a valid pattern (${(error as Error).message})`)
			}
		}
		const exempt = columns.exempt === -1 ? [] : items(row[columns.exempt] ?? '')
		entries.push({ term, banned, exempt })
	}
	if (!entries.some((entry) => entry.banned.length > 0)) problems.push(`${GLOSSARY}: no table with a "${BANNED_COLUMN}" column bans anything`)
	return { entries, problems }
}

/** Blank out what the lint never reads in one line: quotes, code, placeholders. */
export function stripLiterals(line: string): string {
	return line
		.replace(/`[^`]*`/g, ' ')
		.replace(/"[^"]*"/g, ' ')
		.replace(/“[^”]*”/g, ' ')
		.replace(/<[^>\s][^>]*>/g, ' ')
}

/** The lines of a feature file the lint reads, numbered from 1. */
export function featureLines(source: string): Array<[number, string]> {
	const read: Array<[number, string]> = []
	let docString: string | null = null
	for (const [index, raw] of source.split('\n').entries()) {
		const line = raw.trim()
		if (docString) {
			if (line.startsWith(docString)) docString = null
			continue
		}
		if (line.startsWith('"""') || line.startsWith('```')) {
			docString = line.slice(0, 3)
			continue
		}
		if (line === '' || line.startsWith('#') || line.startsWith('@') || line.startsWith('|')) continue
		read.push([index + 1, stripLiterals(line)])
	}
	return read
}

/** The prose lines of a README the lint reads, numbered from 1. */
export function readmeLines(source: string): Array<[number, string]> {
	const read: Array<[number, string]> = []
	const lines = source.split('\n')
	let start = 0
	if (lines[0]?.trim() === '---') {
		const end = lines.findIndex((line, index) => index > 0 && line.trim() === '---')
		start = end === -1 ? 0 : end + 1
	}
	let fence: string | null = null
	let comment = false
	for (let index = start; index < lines.length; index++) {
		const line = lines[index]
		const trimmed = line.trim()
		if (fence) {
			if (trimmed.startsWith(fence)) fence = null
			continue
		}
		const opening = trimmed.match(/^(```|~~~)/)
		if (opening) {
			fence = opening[1]
			continue
		}
		let text = line
		if (comment) {
			const close = text.indexOf('-->')
			if (close === -1) continue
			text = text.slice(close + 3)
			comment = false
		}
		text = text.replace(/<!--.*?-->/g, ' ')
		const open = text.indexOf('<!--')
		if (open !== -1) {
			text = text.slice(0, open)
			comment = true
		}
		text = text.replace(/\]\([^)]*\)/g, '] ').replace(/https?:\/\/\S+/g, ' ')
		read.push([index + 1, stripLiterals(text)])
	}
	return read
}

/** Every banned synonym in `lines`, for a file in `area`. */
export function scan(file: string, area: string, lines: Array<[number, string]>, entries: readonly Entry[]): Violation[] {
	const found: Violation[] = []
	for (const [line, text] of lines) {
		for (const entry of entries) {
			if (entry.exempt.includes(area)) continue
			for (const pattern of entry.banned) {
				pattern.lastIndex = 0
				for (const match of text.matchAll(pattern)) found.push({ file, line, found: match[0], term: entry.term })
			}
		}
	}
	return found.sort((a, b) => a.line - b.line)
}

/** Every banned synonym in every area's feature file and README. */
export function checkGlossary(root = ROOT): { violations: Violation[]; problems: string[] } {
	const glossary = join(root, GLOSSARY)
	if (!existsSync(glossary)) return { violations: [], problems: [`${GLOSSARY} is missing`] }
	const { entries, problems } = readGlossary(readFileSync(glossary, 'utf8'))

	const violations: Violation[] = []
	const features = join(root, FEATURES)
	for (const area of readdirSync(features).sort()) {
		if (!statSync(join(features, area)).isDirectory()) continue
		const feature = `${FEATURES}/${area}/${area}.feature`
		const readme = `${FEATURES}/${area}/README.md`
		if (existsSync(join(root, feature))) violations.push(...scan(feature, area, featureLines(readFileSync(join(root, feature), 'utf8')), entries))
		if (existsSync(join(root, readme))) violations.push(...scan(readme, area, readmeLines(readFileSync(join(root, readme), 'utf8')), entries))
	}
	return { violations, problems }
}

export function main(root = ROOT, log: (line: string) => void = console.log): number {
	const { violations, problems } = checkGlossary(root)
	for (const problem of problems) log(`::error::${problem}`)
	for (const { file, line, found, term } of violations) log(`::error file=${file},line=${line}::"${found}" is a banned synonym; the glossary's word is "${term}" (${GLOSSARY})`)
	if (problems.length > 0 || violations.length > 0) {
		log(`${violations.length} banned synonym(s) in the scenarios and area READMEs, ${problems.length} glossary problem(s).`)
		return 1
	}
	log('The scenarios and area READMEs use the glossary\'s words.')
	return 0
}

if (isMain(import.meta.url)) process.exit(main())
