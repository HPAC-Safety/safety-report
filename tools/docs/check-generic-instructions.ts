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
// is project-specific, so it is not checked (CONV-009). A generic skill
// directory without an exact `SKILL.md` fails, rather than being skipped, so a
// misnamed file cannot hide a skill on a case-insensitive disk.
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

/** Every forbidden term in one file's text, as `path:line: why (match)`. */
export function checkText(path: string, text: string): string[] {
	const problems: string[] = []
	text.split('\n').forEach((line, index) => {
		for (const { pattern, why } of FORBIDDEN) {
			const match = pattern.exec(line)
			if (match) problems.push(`${path}:${index + 1}: ${why} ("${match[0]}")`)
		}
	})
	return problems
}

export function main(root: string = ROOT, files: readonly string[] = genericFiles(root)): number {
	const problems: string[] = missingSkillFiles(root).map((path) => `${path}:1: a generic skill directory needs a file named exactly SKILL.md`)
	for (const path of files) {
		const full = join(root, path)
		if (!existsSync(full)) {
			problems.push(`${path}:1: listed as generic but does not exist`)
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

	console.log(`${files.length} generic instruction file(s) checked. None names this repository.`)
	return 0
}

const runAsCommand = isMain(import.meta.url)
if (runAsCommand) process.exit(main())
