#!/usr/bin/env node
// A generic skill or role agent names nothing specific to this repository
// (CONV-009, CONV-008).
//
// The generic files are meant to be copied into another project unchanged.
// Anything that names this product, its domain, its paths, or one of its
// ADR, lesson, convention, or claim numbers belongs in the project skill that
// extends the generic one.
//
// The rule selects the files: every `.md` under `agents/`, at any depth and in
// any case, and every text file under each `skills/<name>/` whose name has no
// "hpac" — its SKILL.md and any supporting file. A skill whose name says hpac
// is project-specific, so it is checked for record references only (CONV-009). A generic skill
// directory without an exact `SKILL.md` fails, rather than being skipped, so a
// misnamed file cannot hide a skill on a case-insensitive disk.
//
// A skill whose name says hpac is project-specific, so it may name this
// product; but it, like every agent and every generic skill, may not reference
// a record: a specification path, an ADR, a convention, a claim, or a lesson
// (RECORD_REFERENCES). A record's number or path rots when the record moves, so
// the instruction files name no record. AGENTS.md is not scanned; it is the
// topic-to-record map. When a generic file trips both lists,
// the same match is reported once, by the generic list.
//
// Accepted false positives: a generic file may not use "reporter" except as
// "test reporter", nor "occurrence report"; reword it.
//
// The exit code is the contract.
import { existsSync, readdirSync, readFileSync, statSync } from 'node:fs'
import { join, relative } from 'node:path'
import { isMain } from '../lib/actions.ts'

const ROOT = process.cwd()

const TEXT = /\.(md|ya?ml|json|txt)$/i

/** Every file under `directory`, recursively, relative to `root`, sorted. */
function walk(root: string, directory: string): string[] {
	const full = join(root, directory)
	if (!existsSync(full)) return []
	return readdirSync(full)
		.sort()
		.flatMap((name) => {
			const path = join(full, name)
			return statSync(path).isDirectory() ? walk(root, join(directory, name)) : [relative(root, path).split('\\').join('/')]
		})
}

/** The generic skill directories under `root`: every one whose name has no "hpac", in any case. */
function genericSkills(root: string): string[] {
	const skills = join(root, 'skills')
	if (!existsSync(skills)) return []
	return readdirSync(skills)
		.sort()
		.filter((name) => !/hpac/i.test(name) && statSync(join(skills, name)).isDirectory())
}

/** Every generic file under `root`: each agent, and each text file of each skill whose directory has no "hpac". */
export function genericFiles(root: string = ROOT): string[] {
	const agents = walk(root, 'agents').filter((path) => /\.md$/i.test(path))
	const skills = genericSkills(root).flatMap((name) => walk(root, `skills/${name}`).filter((path) => TEXT.test(path)))
	return [...agents, ...skills]
}

/** Every file checked for record references: each agent, and each text file of every skill, hpac or not. */
export function instructionFiles(root: string = ROOT): string[] {
	const agents = walk(root, 'agents').filter((path) => /\.md$/i.test(path))
	const skillsDir = join(root, 'skills')
	const names = existsSync(skillsDir) ? readdirSync(skillsDir).sort().filter((name) => statSync(join(skillsDir, name)).isDirectory()) : []
	return [...agents, ...names.flatMap((name) => walk(root, `skills/${name}`).filter((path) => TEXT.test(path)))]
}

/** Each generic skill directory with no file named exactly `SKILL.md`. */
export function missingSkillFiles(root: string = ROOT): string[] {
	return genericSkills(root)
		.filter((name) => !readdirSync(join(root, 'skills', name)).includes('SKILL.md'))
		.map((name) => `skills/${name}/SKILL.md`)
}

// A pattern carries its own flags; most are case-insensitive. `why` is what
// the author sees.
export interface Forbidden {
	pattern: RegExp
	why: string
}

export const FORBIDDEN: readonly Forbidden[] = [
	{ pattern: /hpac/i, why: 'names the product' },
	{ pattern: /acvl/i, why: 'names the product' },
	{ pattern: /safety-report/i, why: 'names the repository' },
	{ pattern: /aviation|occurrence report|\bpilot|pilote/i, why: 'names the product domain' },
	{ pattern: /safety ?officer|(?<!test )\breporter/i, why: 'names a product role' },
	{ pattern: /typeform|gemini|graphify|reqnroll/i, why: 'names a tool or provider this repository chose' },
	{ pattern: /\bADR-\d/i, why: 'cites a decision record by number' },
	{ pattern: /\blesson \d/i, why: 'cites a lesson by number' },
	{ pattern: /\b(REQ|CON)-[A-Z]+-\d/, why: 'cites a claim by ID' },
	{ pattern: /CONV-\d/, why: 'cites a convention by number' },
	// Case-sensitive: "the Worker" is this product's background service, and "the worker" is a plain noun.
	{ pattern: /\b[Tt]he Worker\b/, why: 'names this product\'s background service' },
	{ pattern: /\.spec\/|docs\/(decisions|lessons)\/|\btools\/[\w/-]+\.|\bsrc\/(web|HpacSafety)/i, why: 'names a path in this repository' },
]

// Checked in every agent and every skill, hpac or not: an instruction file
// names a topic, never the record that holds it. A separator is any run of
// whitespace, ASCII or Unicode hyphens, or "#", so "ADR 0147", "ADR0147", and
// "ADR\u20110147" all count; a reference may wrap onto the next line.
// A lesson or convention needs a 3-4 digit number, so "lesson 2 days old" and
// "lessons 2 and 3" stay prose.
const DASH = '\\-\\u2010-\\u2015'
const SEP = `[\\s${DASH}#]*`
const HYPHEN = `[${DASH}]\\s*`
export const RECORD_REFERENCES: readonly Forbidden[] = [
	// ".spec" with or without a slash, but not a test file: foo.spec.ts, .tsx, .js, .mjs, .cjs, .jsx.
	{ pattern: /\.spec\b(?!\.[cm]?[jt]sx?\b)|(?<![\w/-])(traceability\.md|claims(\.schema)?\.json|area-paths\.json)\b/i, why: 'references a specification path' },
	{ pattern: new RegExp(`\\bADR${SEP}\\d{3,4}`, 'i'), why: 'references a decision record by number' },
	{ pattern: new RegExp(`\\bCONV${SEP}\\d{3}`, 'i'), why: 'references a convention by number' },
	{ pattern: new RegExp(`\\b(REQ|CON)${HYPHEN}[A-Z]+${HYPHEN}\\d{2,}`, 'i'), why: 'references a claim by ID' },
	{ pattern: new RegExp(`\\blessons?(${SEP}\\d{3,4}\\b|\\s*[#${DASH}]\\s*\\d+|/\\d)`, 'i'), why: 'references a lesson by number' },
	{ pattern: /docs\/(decisions|lessons)\//i, why: 'references a record directory' },
]

/** Whether a path is a generic instruction file: an agent, or a file of a skill whose directory has no "hpac". */
export function isGeneric(path: string): boolean {
	const skill = /^skills\/([^/]+)\//.exec(path)
	return !skill || !/hpac/i.test(skill[1])
}

const squash = (text: string): string => text.replace(/\s+/g, ' ')

/** Every term one file's text may not hold, as `path:line: why (match)`; a generic file also fails FORBIDDEN. */
export function checkText(path: string, text: string, generic: boolean = isGeneric(path)): string[] {
	const problems: string[] = []
	const lines = text.split('\n')
	lines.forEach((line, index) => {
		const reported = new Set<string>()
		for (const { pattern, why } of generic ? FORBIDDEN : []) {
			const match = pattern.exec(line)
			if (match) {
				reported.add(squash(match[0]).toLowerCase())
				problems.push(`${path}:${index + 1}: ${why} ("${match[0]}")`)
			}
		}
		// The line joined with the next, so a reference wrapped across a line break is
		// caught; it counts only where it starts, so the next line does not report it again.
		const joined = index + 1 < lines.length ? `${line}\n${lines[index + 1]}` : line
		for (const { pattern, why } of RECORD_REFERENCES) {
			const match = pattern.exec(joined)
			if (!match || match.index >= line.length) continue
			const text = squash(match[0])
			const lower = text.toLowerCase()
			// FORBIDDEN matches a shorter prefix ("ADR-0"), so overlap counts as the same match.
			if (![...reported].some((seen) => lower.includes(seen) || seen.includes(lower))) problems.push(`${path}:${index + 1}: ${why} ("${text}")`)
		}
	})
	return problems
}

export function main(root: string = ROOT, files: readonly string[] = instructionFiles(root)): number {
	const problems: string[] = missingSkillFiles(root).map((path) => `${path}:1: a generic skill directory needs a file named exactly SKILL.md`)
	for (const path of files) {
		const full = join(root, path)
		if (!existsSync(full)) {
			problems.push(`${path}:1: listed but does not exist`)
			continue
		}
		problems.push(...checkText(path, readFileSync(full, 'utf8')))
	}

	if (problems.length > 0) {
		for (const problem of problems) {
			const [file, line, ...rest] = problem.split(':')
			console.error(`::error file=${file},line=${line}::${rest.join(':').trim()}`)
		}
		return 1
	}

	console.log(`${files.length} instruction file(s) checked: generic ones name nothing in this repository, and none references a record.`)
	return 0
}

const runAsCommand = isMain(import.meta.url)
if (runAsCommand) process.exit(main())
