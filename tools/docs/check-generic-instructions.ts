#!/usr/bin/env node
// A generic skill or role agent names nothing specific to this repository
// (CONV-009, CONV-008).
//
// The generic files are meant to be copied into another project unchanged.
// Anything that names this product, its domain, its paths, or one of its
// ADR, lesson, convention, or claim numbers belongs in the project skill that
// extends the generic one.
//
// The rule selects the files: every `agents/*.md`, and every
// `skills/*/SKILL.md` whose directory name has no "hpac". A skill whose name
// says hpac is project-specific, so it is not checked (CONV-009).
//
// The exit code is the contract.
import { existsSync, readdirSync, readFileSync } from 'node:fs'
import { join } from 'node:path'
import { isMain } from '../lib/actions.ts'

const ROOT = process.cwd()

/** Every generic file under `root`: each agent, and each skill whose directory has no "hpac". */
export function genericFiles(root: string = ROOT): string[] {
	const names = (directory: string): string[] => (existsSync(join(root, directory)) ? readdirSync(join(root, directory)).sort() : [])
	const agents = names('agents').filter((name) => name.endsWith('.md')).map((name) => `agents/${name}`)
	const skills = names('skills')
		.filter((name) => !/hpac/i.test(name) && existsSync(join(root, 'skills', name, 'SKILL.md')))
		.map((name) => `skills/${name}/SKILL.md`)
	return [...agents, ...skills]
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
	{ pattern: /aviation|occurrence|\bpilot|pilote/i, why: 'names the product domain' },
	{ pattern: /safety ?officer|\breporter/i, why: 'names a product role' },
	{ pattern: /typeform|gemini|graphify|reqnroll/i, why: 'names a tool or provider this repository chose' },
	{ pattern: /\bADR-\d/i, why: 'cites a decision record by number' },
	{ pattern: /\blesson \d/i, why: 'cites a lesson by number' },
	{ pattern: /\b(REQ|CON)-[A-Z]+-\d/, why: 'cites a claim by ID' },
	{ pattern: /CONV-\d/, why: 'cites a convention by number' },
	// Case-sensitive: "the Worker" is this product's background service, and "the worker" is a plain noun.
	{ pattern: /\bthe Worker\b/, why: 'names this product\'s background service' },
	{ pattern: /\.spec\/|docs\/(decisions|lessons)\/|\btools\/[\w/-]+\.|\bsrc\/|features\//i, why: 'names a path in this repository' },
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
	const problems: string[] = []
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
