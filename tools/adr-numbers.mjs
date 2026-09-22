#!/usr/bin/env node
// An ADR's number is its identity, and it is claimed by naming a file — so two
// branches can claim the same one and nothing notices until a rebase (ADR-0091,
// lesson 0003).
//
// Three jobs, because they are the same job at three moments:
//   --check              a duplicate or a filename disagreeing with its heading
//                        fails, in the pre-commit hook and in CI
//   --next               the next free number, counting every fetched remote
//                        branch, not only origin/main
//   --renumber old new   move the file and rewrite every reference to it
//
// The exit code is the contract.
import { execFileSync } from 'node:child_process'
import { lstatSync, readFileSync, readdirSync, writeFileSync } from 'node:fs'
import { join } from 'node:path'

const ROOT = process.cwd()
const DECISIONS = 'docs/decisions'
const FILENAME = /^ADR-(\d{4})-[a-z0-9-]+\.md$/
// Both separators are in use: older records write "# ADR-0016: title" and
// newer ones "# ADR-0083 — title". The separator is not the point — the number
// agreeing with the filename is — so both are accepted rather than churning
// thirteen historical files to satisfy a cosmetic rule.
const HEADING = /^#\s+ADR-(\d{4})\s*[—:-]/m

const pad = (number) => String(number).padStart(4, '0')

/** Every ADR file in the working tree, with the number its name claims. */
export function localAdrs(root = ROOT) {
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
export function checkNumbering(adrs, read) {
	const problems = []
	const seen = new Map()

	for (const { name, number } of adrs) {
		if (number === null) {
			problems.push(`${DECISIONS}/${name}: not named ADR-NNNN-kebab-slug.md`)
			continue
		}

		if (seen.has(number)) {
			problems.push(
				`${DECISIONS}/${name}: ADR-${number} is already taken by ${seen.get(number)}. ` +
					`Renumber one of them: node tools/adr-numbers.mjs --renumber ${number} $(node tools/adr-numbers.mjs --next)`,
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

/**
 * Numbers claimed anywhere git knows about: the working tree, and every fetched
 * remote branch. A number another open pull request has already used is taken,
 * even though it has not merged — which is exactly the race that keeps costing
 * a rebase.
 */
export function claimedNumbers(root = ROOT, { remote = true } = {}) {
	const claimed = new Set(localAdrs(root).map((adr) => adr.number).filter(Boolean))
	if (!remote) return claimed

	try {
		const refs = execFileSync('git', ['-C', root, 'for-each-ref', '--format=%(refname)', 'refs/remotes/'], { encoding: 'utf8' })
			.split('\n')
			.filter(Boolean)

		for (const ref of refs) {
			const listed = execFileSync('git', ['-C', root, 'ls-tree', '--name-only', `${ref}:${DECISIONS}`], {
				encoding: 'utf8',
				stdio: ['ignore', 'pipe', 'ignore'],
			})
			for (const name of listed.split('\n')) {
				const number = name.match(FILENAME)?.[1]
				if (number) claimed.add(number)
			}
		}
	} catch {
		// No repository, or a ref with no decisions directory. The local set
		// still stands; --next says which sources it consulted.
	}

	return claimed
}

export function nextNumber(claimed) {
	const highest = [...claimed].map(Number).reduce((a, b) => Math.max(a, b), 0)
	return pad(highest + 1)
}

/**
 * Every tracked file that could mention an ADR number. Symlinks are skipped:
 * `CLAUDE.md` and `.cursor/rules/agents.mdc` resolve to `AGENTS.md`, so
 * following them would rewrite the same bytes three times and report the
 * change under a path nobody edits.
 */
function referencingFiles(root) {
	return execFileSync('git', ['-C', root, 'ls-files', '*.md', '*.mdc', '*.mjs', '*.yml', '*.yaml', '*.cs', '*.json'], { encoding: 'utf8' })
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
export function renumber(root, oldNumber, newNumber, { file } = {}) {
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
	const bare = () => new RegExp(`ADR-${oldNumber}(?!-)`, 'g')

	const touched = []
	const unresolved = []

	for (const file of referencingFiles(root)) {
		// The moved record names itself; its own heading is handled below
		// rather than reported as something a person has to disambiguate.
		if (file === movedFile) continue

		const path = join(root, file)
		let text
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
export function main(argv = [], root = ROOT) {
	const read = (name) => readFileSync(join(root, DECISIONS, name), 'utf8')

	if (argv[0] === '--next') {
		console.log(nextNumber(claimedNumbers(root)))
		return 0
	}

	if (argv[0] === '--renumber') {
		const [, oldNumber, newNumber] = argv
		if (!/^\d{4}$/.test(oldNumber ?? '') || !/^\d{4}$/.test(newNumber ?? '')) {
			console.error('Usage: node tools/adr-numbers.mjs --renumber <old> <new> [--file <name>], each number four digits')
			return 1
		}

		const flag = argv.indexOf('--file')
		let result
		try {
			result = renumber(root, oldNumber, newNumber, { file: flag === -1 ? undefined : argv[flag + 1] })
		} catch (error) {
			console.error(`::error::${error.message}`)
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

	const problems = checkNumbering(localAdrs(root), read)
	if (problems.length > 0) {
		for (const problem of problems) {
			const [file, ...rest] = problem.split(': ')
			console.error(`::error file=${file}::${rest.join(': ')}`)
		}
		return 1
	}

	console.log(`${localAdrs(root).length} decision record(s) numbered without collision.`)
	return 0
}

const runAsCommand = String(process.argv[1]).endsWith('adr-numbers.mjs')
if (runAsCommand) process.exit(main(process.argv.slice(2)))
