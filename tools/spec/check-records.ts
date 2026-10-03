#!/usr/bin/env node
// The shape of every decision record, lesson, and convention (ADR-0191).
//
// adr-numbers.ts owns an ADR's number and whether its status agrees with its
// status line. This owns the rest of its shape, and the shape of the other two
// kinds of record the specification chain reads:
//
//   ADR         one **Status:** paragraph directly under the heading, one
//               `## Considered options`, and amendment headings in one form.
//               A record numbered from FIRST_TEMPLATED_ADR on uses the
//               template's sections in its order, takes no amendment, and
//               never the retired `partially-superseded`.
//   lesson      a `kind` of product, process, or incident; only the five
//               sections, in order; what its kind owes — a claim ID under
//               Spec delta, a skill or convention under Skill, or for an
//               incident only Symptom and Root cause.
//   convention  CONV-NNN-kebab-slug.md, its heading agreeing, a unique number,
//               `## Rule` and `## Why`.
//
//   node tools/spec/check-records.ts
//
// Dependency-free, like every other tool in this directory. The exit code is
// the contract.
import { existsSync, readFileSync, readdirSync } from 'node:fs'
import { join } from 'node:path'

import { parseFrontmatter } from '../docs/check-frontmatter.ts'
import { CONVENTIONS, DECISIONS, LESSONS } from './spec-paths.ts'
import { isMain } from '../lib/actions.ts'

const ROOT = process.cwd()

/** The first ADR written under the template; older records keep their sections. */
export const FIRST_TEMPLATED_ADR = 191

/** The template's sections, in order (TEMPLATE.md). */
export const ADR_SECTIONS = ['Context', 'Decision drivers', 'Considered options', 'Decision', 'Consequences', 'Related']
export const REQUIRED_ADR_SECTIONS = ['Context', 'Considered options', 'Decision', 'Consequences']

/** What a status line opens with, by status, on a templated record. */
const STATUS_WORDS: Readonly<Record<string, string>> = {
	proposed: 'Proposed',
	accepted: 'Accepted',
	rejected: 'Rejected',
	deprecated: 'Deprecated',
	superseded: 'Superseded',
}

/** The one form an amendment heading takes. Amendments are history; no new one is written. */
export const AMENDMENT = /^## Amendment \(\d{4}-\d{2}-\d{2}\)( — \S.*)?$/

export const LESSON_KINDS = ['product', 'process', 'incident']
export const LESSON_SECTIONS = ['Symptom', 'Root cause', 'Spec delta', 'Scenario', 'Skill']

export const CONVENTION_FILENAME = /^CONV-(\d{3})-[a-z0-9]+(?:-[a-z0-9]+)*\.md$/
export const CONVENTION_SECTIONS = ['Rule', 'Why', 'Enforced by']

/** The frontmatter of `text` as a plain object, quotes removed. */
export function frontmatter(text: string): Partial<Record<string, string>> {
	const { entries = [] } = parseFrontmatter(text)
	return Object.fromEntries(entries.map(({ key, value }) => [key, value.replace(/^(["'])(.*)\1$/, '$2')]))
}

/** Every `## ` heading outside a fenced code block, without its marks. */
export function headings(text: string): string[] {
	const found: string[] = []
	let fenced = false
	for (const line of text.split('\n')) {
		if (/^\s*(```|~~~)/.test(line)) fenced = !fenced
		else if (!fenced && line.startsWith('## ')) found.push(line.slice(3).trim())
	}
	return found
}

/** The body after the frontmatter block. */
function body(text: string): string {
	const match = text.match(/^---\n[\s\S]*?\n---\n/)
	return match ? text.slice(match[0].length) : text
}

/** The first block after the `# ` heading, or '' when there is none. */
export function firstBlock(text: string): string {
	const lines = body(text).split('\n')
	const heading = lines.findIndex((line) => line.startsWith('# '))
	if (heading === -1) return ''
	let start = heading + 1
	while (start < lines.length && lines[start].trim() === '') start += 1
	let end = start
	while (end < lines.length && lines[end].trim() !== '') end += 1
	return lines.slice(start, end).join('\n')
}

/** `sections` appear in the order `order` gives them. */
function inOrder(sections: readonly string[], order: readonly string[]): boolean {
	const positions = sections.map((section) => order.indexOf(section))
	return positions.every((position, index) => index === 0 || position > positions[index - 1])
}

/** The section's body: from its `## ` heading to the next one. */
function section(text: string, heading: string): string {
	const match = text.match(new RegExp(`^## ${heading}\\s*$([\\s\\S]*?)(?=^## |(?![\\s\\S]))`, 'm'))
	return match ? match[1] : ''
}

/** Every problem with one ADR's shape. `name` is its file name. */
export function checkAdr(name: string, text: string): string[] {
	const path = `${DECISIONS}/${name}`
	const problems: string[] = []
	const number = Number(name.match(/^ADR-(\d{4})-/)?.[1] ?? 0)
	const sections = headings(text)
	const status = frontmatter(text).status ?? ''
	const statusLine = firstBlock(text)

	if (!statusLine.startsWith('**Status:**')) {
		problems.push(`${path}: the first block under the heading is not its **Status:** line — a record states its status once, directly under its title (ADR-0191)`)
	}
	if ((body(text).match(/^\*\*Status:\*\*/gm) ?? []).length > 1) problems.push(`${path}: more than one **Status:** line`)
	if (sections.includes('Status')) problems.push(`${path}: a "## Status" section — the status is the **Status:** line under the heading (ADR-0191)`)

	const options = sections.filter((heading) => heading === 'Considered options').length
	if (options !== 1) {
		problems.push(`${path}: ${options === 0 ? 'no' : options} "## Considered options" section${options === 0 ? '' : 's'} — every record names the options it weighed, or says none were recorded (ADR-0191)`)
	}

	for (const heading of sections.filter((heading) => /amendment/i.test(heading))) {
		if (!AMENDMENT.test(`## ${heading}`)) problems.push(`${path}: "## ${heading}" — an amendment heading reads "## Amendment (YYYY-MM-DD)", optionally " — subject" (ADR-0191)`)
	}

	if (number >= FIRST_TEMPLATED_ADR) {
		const unknown = sections.filter((heading) => !ADR_SECTIONS.includes(heading))
		if (unknown.length > 0) {
			problems.push(`${path}: ${unknown.map((heading) => `"## ${heading}"`).join(', ')} — a record follows TEMPLATE.md: ${ADR_SECTIONS.join(', ')}; anything else is a "### " under one of them. An accepted record takes no amendment; a change is a new ADR (ADR-0191)`)
		}
		for (const required of REQUIRED_ADR_SECTIONS) {
			if (!sections.includes(required)) problems.push(`${path}: no "## ${required}" section (TEMPLATE.md, ADR-0191)`)
		}
		const known = sections.filter((heading) => ADR_SECTIONS.includes(heading))
		if (!inOrder(known, ADR_SECTIONS)) problems.push(`${path}: sections out of order — TEMPLATE.md orders them ${ADR_SECTIONS.join(', ')}`)
		if (status === 'partially-superseded') {
			problems.push(`${path}: "status: partially-superseded" is retired — a record a later one changes is superseded by it (ADR-0191)`)
		}
		const word = STATUS_WORDS[status]
		if (word && !new RegExp(`^\\*\\*Status:\\*\\*\\s+${word}\\b`).test(statusLine)) {
			problems.push(`${path}: its status line does not open with "${word}", matching "status: ${status}"`)
		}
	}

	return problems
}

/** Every problem with one lesson's shape. `skills` are the skill names that exist. */
export function checkLesson(name: string, text: string, skills: ReadonlySet<string>, conventions: ReadonlySet<string>): string[] {
	const path = `${LESSONS}/${name}`
	const problems: string[] = []
	const kind = frontmatter(text).kind ?? ''
	const sections = headings(text)

	if (!LESSON_KINDS.includes(kind)) {
		return [`${path}: "kind: ${kind}" is not one of ${LESSON_KINDS.join(', ')} — say whether a claim, a skill, or nothing upstream is its remedy (ADR-0191)`]
	}

	const unknown = sections.filter((heading) => !LESSON_SECTIONS.includes(heading))
	if (unknown.length > 0) {
		problems.push(`${path}: ${unknown.map((heading) => `"## ${heading}"`).join(', ')} — a lesson has only ${LESSON_SECTIONS.join(', ')}; anything else is a "### " under one of them`)
	}
	for (const heading of new Set(sections)) {
		if (sections.filter((other) => other === heading).length > 1) problems.push(`${path}: "## ${heading}" appears twice`)
	}
	const known = sections.filter((heading) => LESSON_SECTIONS.includes(heading))
	if (!inOrder(known, LESSON_SECTIONS)) problems.push(`${path}: sections out of order — ${LESSON_SECTIONS.join(', ')}`)

	const required = kind === 'incident' ? ['Symptom', 'Root cause'] : LESSON_SECTIONS
	for (const heading of required) {
		if (!sections.includes(heading)) problems.push(`${path}: ${kind === 'incident' ? 'an' : 'a'} ${kind} lesson needs "## ${heading}"`)
	}

	if (kind === 'product' && !/\b(REQ|CON)-[A-Z]+-\d{3}\b/.test(section(text, 'Spec delta'))) {
		problems.push(`${path}: a product lesson's "## Spec delta" names the REQ- or CON- claim that is its remedy (ADR-0191)`)
	}

	if (kind === 'process') {
		const skill = section(text, 'Skill')
		const named = [...skill.matchAll(/`([a-z0-9]+(?:-[a-z0-9]+)+)`/g)].map((match) => match[1])
		const conventionIds = skill.match(/\bCONV-\d{3}\b/g) ?? []
		const real = named.filter((candidate) => skills.has(candidate)).length + conventionIds.filter((id) => conventions.has(id)).length
		if (real === 0) {
			problems.push(`${path}: a process lesson's "## Skill" names the skill or convention it changed, as \`skill-name\` or CONV-NNN, and that skill or convention exists (ADR-0191)`)
		}
	}

	return problems
}

/** Every problem with one convention's shape, apart from a duplicate number. */
export function checkConvention(name: string, text: string): string[] {
	const path = `${CONVENTIONS}/${name}`
	const number = name.match(CONVENTION_FILENAME)?.[1]
	if (number === undefined) return [`${path}: not named CONV-NNN-kebab-slug.md`]

	const problems: string[] = []
	const meta = frontmatter(text)
	if (meta.type !== 'convention') problems.push(`${path}: "type: ${meta.type ?? ''}" — a convention is "type: convention"`)
	if (!['accepted', 'superseded'].includes(meta.status ?? '')) problems.push(`${path}: "status: ${meta.status ?? ''}" is not accepted or superseded`)
	if (!/^\d{4}-\d{2}-\d{2}$/.test(meta.date ?? '')) problems.push(`${path}: "date: ${meta.date ?? ''}" is not YYYY-MM-DD`)

	const heading = body(text).match(/^# CONV-(\d{3})\s+—\s+\S/m)
	if (!heading) problems.push(`${path}: no "# CONV-${number} — <rule>" heading`)
	else if (heading[1] !== number) problems.push(`${path}: the file says CONV-${number} and the heading says CONV-${heading[1]}`)

	const sections = headings(text)
	const unknown = sections.filter((section) => !CONVENTION_SECTIONS.includes(section))
	if (unknown.length > 0) problems.push(`${path}: ${unknown.map((section) => `"## ${section}"`).join(', ')} — a convention has ${CONVENTION_SECTIONS.join(', ')}`)
	for (const required of ['Rule', 'Why']) {
		if (!sections.includes(required)) problems.push(`${path}: no "## ${required}" section`)
	}
	return problems
}

const list = (root: string, directory: string, pattern: RegExp): string[] =>
	existsSync(join(root, directory)) ? readdirSync(join(root, directory)).filter((name) => pattern.test(name)).sort() : []

/** Every problem across the tree. */
export function checkRecords(root = ROOT): { problems: string[]; counts: { adrs: number; lessons: number; conventions: number } } {
	const read = (directory: string, name: string): string => readFileSync(join(root, directory, name), 'utf8')
	const adrs = list(root, DECISIONS, /^ADR-\d{4}-.+\.md$/)
	const lessons = list(root, LESSONS, /^\d{4}-.+\.md$/)
	const conventions = list(root, CONVENTIONS, /\.md$/).filter((name) => name !== 'README.md')

	const skills = new Set(list(root, 'skills', /^[a-z0-9-]+$/).filter((name) => existsSync(join(root, 'skills', name, 'SKILL.md'))))
	const conventionIds = new Set(conventions.flatMap((name) => (CONVENTION_FILENAME.test(name) ? [`CONV-${name.slice(5, 8)}`] : [])))

	const problems = [
		...adrs.flatMap((name) => checkAdr(name, read(DECISIONS, name))),
		...lessons.flatMap((name) => checkLesson(name, read(LESSONS, name), skills, conventionIds)),
		...conventions.flatMap((name) => checkConvention(name, read(CONVENTIONS, name))),
	]

	const seen = new Map<string, string>()
	for (const name of conventions) {
		const number = name.match(CONVENTION_FILENAME)?.[1]
		if (number === undefined) continue
		const earlier = seen.get(number)
		if (earlier) problems.push(`${CONVENTIONS}/${name}: CONV-${number} is already taken by ${earlier}`)
		else seen.set(number, name)
	}

	return { problems, counts: { adrs: adrs.length, lessons: lessons.length, conventions: conventions.length } }
}

/**
 * Reports the way the command line does, without exiting, so a test can
 * exercise every outcome in-process. Returns the exit code.
 */
export function main(root = ROOT): number {
	const { problems, counts } = checkRecords(root)
	if (problems.length > 0) {
		for (const problem of problems) {
			const [file, ...rest] = problem.split(': ')
			console.error(`::error file=${file}::${rest.join(': ')}`)
		}
		return 1
	}
	console.log(`${counts.adrs} decision record(s), ${counts.lessons} lesson(s), and ${counts.conventions} convention(s) keep their shape (ADR-0191).`)
	return 0
}

const runAsCommand = isMain(import.meta.url)
if (runAsCommand) process.exit(main())
