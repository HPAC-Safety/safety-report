#!/usr/bin/env node
// A workflow step runs one command; its logic lives in a tested script under
// tools/ (ADR-0189).
//
// Every `run:` in .github/workflows/*.yml and .github/actions/*/action.yml must
// be one command line. A long command may continue across lines with a
// trailing backslash, and comments and blank lines are ignored. Refused:
//
//   - a second command line;
//   - shell control flow: if, then, for, while, until, case, function;
//   - command lists: &&, ||, and ;.
//
// A pipe, a redirect, and a `$(...)` stay allowed inside the one command —
// `echo "$TOKEN" | docker login ...` is one command, not logic.
//
// Dependency-free, like the other checks: it reads `run:` values line by line
// rather than parsing YAML, which is enough for the block (`|`, `>`) and plain
// scalar forms the workflows use.
import { readdirSync, readFileSync, existsSync } from 'node:fs'
import path from 'node:path'
import { isMain } from '../lib/actions.ts'

const KEYWORDS = /(^|[\s;(])(if|then|elif|else|fi|for|while|until|do|done|case|esac|function)(?=\s|$)/
const LISTS = /&&|\|\||(^|[^\\]);/

/** Extracts each `run:` value from a workflow's text, with the line it starts on. */
export interface RunStep {
	line: number
	script: string
}

export function runSteps(text: string): RunStep[] {
	const lines = text.split('\n')
	const steps: RunStep[] = []
	for (let i = 0; i < lines.length; i++) {
		const match = lines[i].match(/^(\s*)(?:- )?run:\s*(.*)$/)
		if (!match) continue
		const indent = match[1].length
		const rest = match[2].replace(/\s+#.*$/, '')
		if (/^[|>][-+]?\d*$/.test(rest)) {
			const body: string[] = []
			let j = i + 1
			for (; j < lines.length; j++) {
				const line = lines[j]
				if (line.trim() === '') { body.push(''); continue }
				if (leadingWidth(line) <= indent) break
				body.push(line)
			}
			steps.push({ line: i + 1, script: dedent(body).join('\n').trimEnd() })
			i = j - 1
		} else {
			steps.push({ line: i + 1, script: unquote(rest) })
		}
	}
	return steps
}

function leadingWidth(line: string): number {
	return line.length - line.trimStart().length
}

function dedent(lines: string[]): string[] {
	const widths = lines.filter((l) => l.trim()).map(leadingWidth)
	const min = widths.length ? Math.min(...widths) : 0
	return lines.map((l) => l.slice(min))
}

function unquote(value: string): string {
	if (/^'.*'$/.test(value)) return value.slice(1, -1).replace(/''/g, "'")
	if (/^".*"$/.test(value)) return value.slice(1, -1)
	return value
}

/** The command lines of a script: comments and blanks dropped, continuations joined. */
export function commandLines(script: string): string[] {
	const out: string[] = []
	let pending = ''
	for (const raw of script.split('\n')) {
		const line = raw.trim()
		if (!pending && (line === '' || line.startsWith('#'))) continue
		const joined = pending ? `${pending} ${line}` : line
		if (line.endsWith('\\')) { pending = joined.slice(0, -1).trimEnd(); continue }
		pending = ''
		out.push(joined)
	}
	if (pending) out.push(pending)
	return out
}

/** Strips quoted strings so a keyword or `;` inside one is not mistaken for shell. */
function withoutQuotes(line: string): string {
	return line.replace(/'[^']*'/g, "''").replace(/"(?:[^"\\]|\\.)*"/g, '""')
}

/** Why a step's script is refused, or null when it is one plain command. */
export function problem(script: string): string | null {
	const commands = commandLines(script)
	if (commands.length > 1) return `holds ${commands.length} command lines; move them into a script under tools/`
	const [command = ''] = commands
	const bare = withoutQuotes(command)
	if (KEYWORDS.test(bare)) return 'holds shell control flow; move it into a script under tools/'
	if (LISTS.test(bare)) return 'chains commands with &&, ||, or ;; use one command per step or a script under tools/'
	return null
}

/** The workflow and composite-action files under a repository root. */
export function workflowFiles(root: string): string[] {
	const files: string[] = []
	const workflows = path.join(root, '.github/workflows')
	if (existsSync(workflows)) {
		for (const name of readdirSync(workflows)) if (/\.ya?ml$/.test(name)) files.push(`.github/workflows/${name}`)
	}
	const actions = path.join(root, '.github/actions')
	if (existsSync(actions)) {
		for (const name of readdirSync(actions)) {
			for (const file of ['action.yml', 'action.yaml']) {
				if (existsSync(path.join(actions, name, file))) files.push(`.github/actions/${name}/${file}`)
			}
		}
	}
	return files.sort()
}

/** Checks every workflow under root; prints each refusal as an annotation and returns the exit code. */
export function main(root: string = process.cwd(), log: (line: string) => void = console.log): number {
	let failures = 0
	let checked = 0
	for (const file of workflowFiles(root)) {
		for (const step of runSteps(readFileSync(path.join(root, file), 'utf8'))) {
			checked++
			const reason = problem(step.script)
			if (!reason) continue
			failures++
			log(`::error file=${file},line=${step.line}::${file}:${step.line}: a run step ${reason} (ADR-0189).`)
		}
	}
	if (failures) return 1
	log(`check-workflow-steps: ${checked} run step(s), each one command.`)
	return 0
}

if (isMain(import.meta.url)) process.exit(main())
