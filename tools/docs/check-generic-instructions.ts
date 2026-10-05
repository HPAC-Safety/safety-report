#!/usr/bin/env node
// No skill references a record (CONV-009).
//
// Every text file under each `skills/<name>/` — its SKILL.md and any supporting
// file — may not reference a record: a specification path, an ADR, a convention,
// a claim, or a lesson (RECORD_REFERENCES). A record's number or path rots when
// the record moves, so a skill names a topic, never the record that holds it.
// AGENTS.md is not scanned; it is the topic-to-record map.
//
// The generic skills and role agents live in ChaseFlorell/agent-team, whose CI
// guards that they name nothing specific to this repository. Only the `hpac-*`
// project skills remain here.
//
// Every `skills/<name>/` here is project-specific, so its name says hpac; a
// generic skill belongs in agent-team.
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

/** The name of every `skills/<name>/` directory, sorted. */
export function skillNames(root: string = ROOT): string[] {
	const skillsDir = join(root, 'skills')
	return existsSync(skillsDir) ? readdirSync(skillsDir).sort().filter((name) => statSync(join(skillsDir, name)).isDirectory()) : []
}

/** A skill here is project-specific, so its name says hpac (CONV-009). One that does not is generic and belongs in agent-team. */
export function checkSkillName(name: string): string[] {
	return /(^|-)hpac(-|$)/.test(name) ? [] : [`skills/${name}:1: skills/${name}: a skill here is project-specific, so its name says hpac; a generic skill belongs in agent-team`]
}

/** Every file checked for record references: each text file of every skill. */
export function instructionFiles(root: string = ROOT): string[] {
	return skillNames(root).flatMap((name) => walk(root, `skills/${name}`).filter((path) => TEXT.test(path)))
}

// A pattern carries its own flags; most are case-insensitive. `why` is what
// the author sees.
export interface Forbidden {
	pattern: RegExp
	why: string
}

// Checked in every skill: an instruction file
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

const squash = (text: string): string => text.replace(/\s+/g, ' ')

/** Every record reference one file's text holds, as `path:line: why (match)`. */
export function checkText(path: string, text: string): string[] {
	const problems: string[] = []
	const lines = text.split('\n')
	lines.forEach((line, index) => {
		// The line joined with the next, so a reference wrapped across a line break is
		// caught; it counts only where it starts, so the next line does not report it again.
		const joined = index + 1 < lines.length ? `${line}\n${lines[index + 1]}` : line
		for (const { pattern, why } of RECORD_REFERENCES) {
			const match = pattern.exec(joined)
			if (!match || match.index >= line.length) continue
			problems.push(`${path}:${index + 1}: ${why} ("${squash(match[0])}")`)
		}
	})
	return problems
}

export function main(root: string = ROOT, files: readonly string[] = instructionFiles(root)): number {
	const problems: string[] = skillNames(root).flatMap(checkSkillName)
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

	console.log(`${files.length} skill file(s) checked: none references a record, and every skill name says hpac.`)
	return 0
}

const runAsCommand = isMain(import.meta.url)
if (runAsCommand) process.exit(main())
