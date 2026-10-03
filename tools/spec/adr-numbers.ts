#!/usr/bin/env node
// An ADR's number is its identity, and it is claimed by naming a file — so two
// branches can claim the same one and nothing notices until a rebase (ADR-0091,
// lesson 0003).
//
// Three jobs, because they are the same job at three moments:
//   --check              a duplicate, a filename disagreeing with its heading,
//                        or a status disagreeing with its status line fails,
//                        in the pre-commit hook and in CI
//   --next               the next free number, counting every fetched remote
//                        branch, not only origin/main
//   --renumber old new   move the file and rewrite every reference to it
//
// The exit code is the contract.
import { execFileSync } from 'node:child_process'
import { lstatSync, readFileSync, readdirSync, writeFileSync } from 'node:fs'
import { join } from 'node:path'

import { DECISIONS, LEGACY_DECISIONS } from './spec-paths.ts'
import { isMain, errorMessage } from '../lib/actions.ts'

const ROOT = process.cwd()
const FILENAME = /^ADR-(\d{4})-[a-z0-9-]+\.md$/
// Both separators are in use: older records write "# ADR-0016: title" and
// newer ones "# ADR-0083 — title". The separator is not the point — the number
// agreeing with the filename is — so both are accepted rather than churning
// thirteen historical files to satisfy a cosmetic rule.
const HEADING = /^#\s+ADR-(\d{4})\s*[—:-]/m

const pad = (number: number): string => String(number).padStart(4, '0')

/** Every ADR file in the working tree, with the number its name claims. */
/** One ADR file, with the number its name claims, or null when the name has none. */
export interface Adr {
	name: string
	number: string | null
}

/** Reads an ADR file by name. */
export type ReadAdr = (name: string) => string

export function localAdrs(root = ROOT): Adr[] {
	return readdirSync(join(root, DECISIONS))
		.filter((name) => name.startsWith('ADR-'))
		.sort()
		.map((name) => ({ name, number: name.match(FILENAME)?.[1] ?? null }))
}

/**
 * Every problem with the numbering, as messages naming the fix. A duplicate is
 * the one this exists for; the heading check catches a rename that moved the
 * file and left the document calling itself something else.
 */
export function checkNumbering(adrs: readonly Adr[], read: ReadAdr): string[] {
	const problems: string[] = []
	const seen = new Map<string, string>()

	for (const { name, number } of adrs) {
		if (number === null) {
			problems.push(`${DECISIONS}/${name}: not named ADR-NNNN-kebab-slug.md`)
			continue
		}

		if (seen.has(number)) {
			problems.push(
				`${DECISIONS}/${name}: ADR-${number} is already taken by ${seen.get(number)}. ` +
					`Renumber one of them: node tools/spec/adr-numbers.ts --renumber ${number} $(node tools/spec/adr-numbers.ts --next)`,
			)
		} else {
			seen.set(number, name)
		}

		const heading = read(name).match(HEADING)
		if (!heading) {
			problems.push(`${DECISIONS}/${name}: no "# ADR-${number} — <title>" heading, so the file and the document disagree about what this is`)
		} else if (heading[1] !== number) {
			problems.push(`${DECISIONS}/${name}: the file says ADR-${number} and the heading says ADR-${heading[1]}`)
		}
	}

	return problems
}

// The lifecycle (ADR-0192). `partially-superseded` is retired: the records that
// already carry it keep it, and check-records.ts and check-adr-immutability.ts
// refuse it anywhere new.
export const STATUSES: readonly string[] = ['proposed', 'accepted', 'rejected', 'deprecated', 'superseded', 'partially-superseded']

/** The statuses whose status line must link what replaced or narrowed the record. */
const SUPERSEDED: readonly string[] = ['superseded', 'partially-superseded']

/**
 * An ADR's own status paragraph: the `**Status:**` paragraph (ADR-0192), or
 * the first paragraph of the `## Status` section older branches may still
 * carry. Only this is read, because the rest of a record discusses other
 * records' supersession freely.
 */
export function statusLine(text: string): string {
	const bold = text.match(/^\*\*Status:\*\*[\s\S]*?(?=\n\s*\n|(?![\s\S]))/m)
	if (bold) return bold[0]
	const section = text.match(/^## Status\s*\n+([\s\S]*?)(?=\n\s*\n|^## |(?![\s\S]))/m)
	return section ? section[1] : ''
}

/**
 * Every disagreement between what an ADR declares and what it says (ADR-0183):
 * a status outside the closed set; a status line saying it is superseded by
 * another record while it declares `accepted`; a superseded record whose status
 * line links nothing that replaced it; a successor that does not exist.
 */
export function checkStatus(adrs: readonly Adr[], read: ReadAdr): string[] {
	const problems: string[] = []
	const numbers = new Set(adrs.flatMap((adr) => (adr.number ? [adr.number] : [])))

	for (const { name, number } of adrs) {
		if (number === null) continue
		const text = read(name)
		const status = text.match(/^status:\s*(.*?)\s*$/m)?.[1]
		const line = statusLine(text)

		if (status === undefined || !STATUSES.includes(status)) {
			problems.push(`${DECISIONS}/${name}: "status: ${status ?? ''}" is not one of ${STATUSES.join(', ')}`)
			continue
		}

		const successors = [...line.matchAll(/superseded\s+by\s+\**\[?`?ADR-(\d{4})/gi)].map((match) => match[1])
		if (!SUPERSEDED.includes(status) && successors.length > 0) {
			problems.push(`${DECISIONS}/${name}: its status line says it is superseded by ADR-${successors[0]}, but it declares "status: ${status}" — make it superseded`)
		}
		if (SUPERSEDED.includes(status) && !/\]\(/.test(line)) {
			problems.push(`${DECISIONS}/${name}: "status: ${status}" needs a **Status:** line linking what replaced or narrowed it`)
		}
		for (const successor of successors) {
			if (!numbers.has(successor)) problems.push(`${DECISIONS}/${name}: its status line names ADR-${successor}, which does not exist`)
		}
	}

	return problems
}

/**
 * Numbers claimed anywhere git knows about: the working tree, and every fetched
 * remote branch. A number another open pull request has already used is taken,
 * even though it has not merged — which is exactly the race that keeps costing
 * a rebase.
 */
export function claimedNumbers(root = ROOT, { remote = true, report }: { remote?: boolean; report?: (line: string) => void } = {}): Set<string> {
	const claimed = new Set(localAdrs(root).flatMap((adr) => (adr.number ? [adr.number] : [])))
	if (!remote) return claimed

	let refs: string[] = []
	try {
		refs = execFileSync('git', ['-C', root, 'for-each-ref', '--format=%(refname)', 'refs/remotes/'], { encoding: 'utf8' })
			.split('\n')
			.filter(Boolean)
	} catch {
		// No repository. The local set still stands.
	}

	// Each ref and directory on its own: a branch not yet rebased past the
	// move to .spec/ (ADR-0183) has only the old directory, and one missing
	// directory must not stop the rest of the scan.
	let legacy = 0
	for (const ref of refs) {
		for (const directory of [DECISIONS, LEGACY_DECISIONS]) {
			let listed: string
			try {
				listed = execFileSync('git', ['-C', root, 'ls-tree', '--name-only', `${ref}:${directory}`], {
					encoding: 'utf8',
					stdio: ['ignore', 'pipe', 'ignore'],
				})
			} catch {
				continue
			}
			if (directory === LEGACY_DECISIONS) legacy += 1
			for (const name of listed.split('\n')) {
				const number = name.match(FILENAME)?.[1]
				if (number) claimed.add(number)
			}
		}
	}

	report?.(`consulted ${refs.length} remote ref(s); ${legacy} still on ${LEGACY_DECISIONS}`)
	return claimed
}

export function nextNumber(claimed: Iterable<string>): string {
	const highest = [...claimed].map(Number).reduce((a, b) => Math.max(a, b), 0)
	return pad(highest + 1)
}

/**
 * Every tracked file that could mention an ADR number. Symlinks are skipped:
 * `CLAUDE.md` and `.cursor/rules/agents.mdc` resolve to `AGENTS.md`, so
 * following them would rewrite the same bytes three times and report the
 * change under a path nobody edits.
 */
function referencingFiles(root: string): string[] {
	return execFileSync('git', ['-C', root, 'ls-files', '*.md', '*.mdc', '*.ts', '*.mjs', '*.yml', '*.yaml', '*.cs', '*.json'], { encoding: 'utf8' })
		.split('\n')
		.filter(Boolean)
		.filter((file) => {
			try {
				return !lstatSync(join(root, file)).isSymbolicLink()
			} catch {
				return false
			}
		})
}

/**
 * Moves ADR-<old> to ADR-<new> and rewrites every reference: the filename in a
 * link, the heading, and bare prose like "(ADR-0089)". Missing one leaves a
 * link that resolves to somebody else's decision, which reads as correct.
 *
 * It rewrites every *textual* occurrence, which includes a test fixture that
 * names the number on purpose. That is the honest trade for catching prose
 * references, so it prints every file it touched — read the diff before
 * committing, the same as any other mechanical edit.
 */
export function renumber(
	root: string,
	oldNumber: string,
	newNumber: string,
	{ file }: { file?: string | undefined } = {},
): { from: string; to: string; touched: string[]; unresolved: string[] } {
	const adrs = localAdrs(root)
	const matching = adrs.filter((entry) => (file ? entry.name === file : entry.number === oldNumber))

	if (matching.length === 0) throw new Error(file ? `No ${file} in ${DECISIONS}` : `No ADR-${oldNumber} in ${DECISIONS}`)

	// The case this tool exists for is two records sharing a number, so
	// picking one of them by sort order would be the wrong kind of helpful.
	if (matching.length > 1) {
		throw new Error(
			`ADR-${oldNumber} names ${matching.length} files (${matching.map((entry) => entry.name).join(', ')}). ` +
				'Say which one: --renumber <old> <new> --file <name>',
		)
	}

	const [adr] = matching
	if (adrs.some((entry) => entry.number === newNumber)) throw new Error(`ADR-${newNumber} already exists`)

	const renamed = adr.name.replace(`ADR-${oldNumber}-`, `ADR-${newNumber}-`)
	execFileSync('git', ['-C', root, 'mv', join(DECISIONS, adr.name), join(DECISIONS, renamed)])

	const slug = adr.name.replace(/\.md$/, '')
	const renamedSlug = renamed.replace(/\.md$/, '')
	const movedFile = `${DECISIONS}/${renamed}`

	// A reference by slug names one record and is always safe to rewrite. A
	// bare "ADR-0090" is only safe while that number names one record — which
	// is exactly what is not true when the reason you are renumbering is a
	// collision. So the bare form is rewritten only when the old number was
	// unique, and reported for a person to resolve when it was not. Rewriting
	// it regardless is how a citation of somebody else's decision gets
	// silently repointed at yours.
	const ambiguous = adrs.filter((entry) => entry.number === oldNumber).length > 1
	const bare = (): RegExp => new RegExp(`ADR-${oldNumber}(?!-)`, 'g')

	const touched: string[] = []
	const unresolved: string[] = []

	for (const file of referencingFiles(root)) {
		// The moved record names itself; its own heading is handled below
		// rather than reported as something a person has to disambiguate.
		if (file === movedFile) continue

		const path = join(root, file)
		let text: string
		try {
			text = readFileSync(path, 'utf8')
		} catch {
			continue
		}

		// The slug contains the number, so it is rewritten first; doing the
		// bare form first would corrupt the filename it is part of.
		let rewritten = text.replaceAll(slug, renamedSlug)

		if (ambiguous) {
			for (const [index, line] of rewritten.split('\n').entries()) {
				if (bare().test(line)) unresolved.push(`${file}:${index + 1}: ${line.trim()}`)
			}
		} else {
			rewritten = rewritten.replace(bare(), `ADR-${newNumber}`)
		}

		if (rewritten !== text) {
			writeFileSync(path, rewritten)
			touched.push(file)
		}
	}

	const movedPath = join(root, movedFile)
	const before = readFileSync(movedPath, 'utf8')
	const after = before.replaceAll(slug, renamedSlug).replace(bare(), `ADR-${newNumber}`)
	if (after !== before) {
		writeFileSync(movedPath, after)
		touched.push(movedFile)
	}

	return { from: adr.name, to: renamed, touched, unresolved }
}

/**
 * Reports the way the command line does, without exiting, so a test can
 * exercise every outcome in-process. Returns the exit code.
 */
export function main(argv: readonly string[] = [], root = ROOT): number {
	const read: ReadAdr = (name) => readFileSync(join(root, DECISIONS, name), 'utf8')

	if (argv[0] === '--next') {
		console.log(nextNumber(claimedNumbers(root, { report: (line) => console.error(line) })))
		return 0
	}

	if (argv[0] === '--renumber') {
		const oldNumber = argv.at(1)
		const newNumber = argv.at(2)
		if (oldNumber === undefined || newNumber === undefined || !/^\d{4}$/.test(oldNumber) || !/^\d{4}$/.test(newNumber)) {
			console.error('Usage: node tools/spec/adr-numbers.ts --renumber <old> <new> [--file <name>], each number four digits')
			return 1
		}

		const flag = argv.indexOf('--file')
		let result: ReturnType<typeof renumber>
		try {
			result = renumber(root, oldNumber, newNumber, { file: flag === -1 ? undefined : argv[flag + 1] })
		} catch (error) {
			console.error(`::error::${errorMessage(error)}`)
			return 1
		}

		console.log(`${result.from} -> ${result.to}`)
		for (const file of result.touched) console.log(`  rewrote ${file}`)

		if (result.unresolved.length > 0) {
			console.log('')
			console.log(`${result.unresolved.length} bare reference(s) left alone. While two records shared`)
			console.log(`ADR-${oldNumber}, a bare number does not say which one is meant — resolve these by hand:`)
			for (const line of result.unresolved) console.log(`  ${line}`)
		}

		return 0
	}

	const adrs = localAdrs(root)
	const problems = [...checkNumbering(adrs, read), ...checkStatus(adrs, read)]
	if (problems.length > 0) {
		for (const problem of problems) {
			const [file, ...rest] = problem.split(': ')
			console.error(`::error file=${file}::${rest.join(': ')}`)
		}
		return 1
	}

	console.log(`${adrs.length} decision record(s) numbered without collision, each status agreeing with its status line.`)
	return 0
}

const runAsCommand = isMain(import.meta.url)
if (runAsCommand) process.exit(main(process.argv.slice(2)))
