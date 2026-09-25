#!/usr/bin/env node
// A generic skill or role agent names nothing specific to this repository
// (ADR-0131).
//
// The generic files are meant to be copied into another project unchanged.
// Anything that names this product, its domain, its paths, or one of its
// ADR, lesson, or claim numbers belongs in the project skill that extends the
// generic one. This is the list of generic files and the terms they may not
// contain; a split's project skill is deliberately not on the list.
//
// The exit code is the contract.
import { existsSync, readFileSync } from 'node:fs'
import { join } from 'node:path'

const ROOT = process.cwd()

export const GENERIC_FILES = [
	'agents/ai-author.md',
	'agents/database-administrator.md',
	'agents/implementer.md',
	'agents/spec-author.md',
	'agents/spec-reviewer.md',
	'agents/test-writer.md',
	'skills/clarify-requirements/SKILL.md',
	'skills/coding-conventions/SKILL.md',
	'skills/deliver-change/SKILL.md',
	'skills/design-ef-core-model/SKILL.md',
	'skills/manage-ef-core-migrations/SKILL.md',
	'skills/postgres-dba/SKILL.md',
	'skills/test-from-scenarios/SKILL.md',
]

// Each term is matched case-insensitively. `why` is what the author sees.
export const FORBIDDEN = [
	{ pattern: /hpac/i, why: 'names the product' },
	{ pattern: /acvl/i, why: 'names the product' },
	{ pattern: /safety-report/i, why: 'names the repository' },
	{ pattern: /aviation|occurrence|\bpilot|pilote/i, why: 'names the product domain' },
	{ pattern: /safety ?officer|\breporter/i, why: 'names a product role' },
	{ pattern: /typeform|deepl|gemini|graphify|reqnroll/i, why: 'names a tool or provider this repository chose' },
	{ pattern: /\bADR-\d/i, why: 'cites a decision record by number' },
	{ pattern: /\blesson \d/i, why: 'cites a lesson by number' },
	{ pattern: /\b(REQ|CON)-[A-Z]+-\d/, why: 'cites a claim by ID' },
	{ pattern: /docs\/(decisions|lessons)\/|\btools\/[\w-]+\.|\bsrc\/|features\//i, why: 'names a path in this repository' },
]

/** Every forbidden term in one file's text, as `path:line: why (match)`. */
export function checkText(path, text) {
	const problems = []
	text.split('\n').forEach((line, index) => {
		for (const { pattern, why } of FORBIDDEN) {
			const match = line.match(pattern)
			if (match) problems.push(`${path}:${index + 1}: ${why} ("${match[0]}")`)
		}
	})
	return problems
}

export function main(root = ROOT, files = GENERIC_FILES) {
	const problems = []
	for (const path of files) {
		const full = join(root, path)
		if (!existsSync(full)) {
			problems.push(`${path}:1: listed as generic but does not exist — update GENERIC_FILES`)
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

const runAsCommand = String(process.argv[1]).endsWith('check-generic-instructions.mjs')
if (runAsCommand) process.exit(main())
