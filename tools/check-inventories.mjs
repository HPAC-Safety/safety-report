#!/usr/bin/env node
// The source inventory maps every directory under src/ (ADR-0143).
//
// docs/source-inventory.md is kept at directory level so it stays true: a new
// file in an existing directory needs no entry, but a new directory does. This
// fails when a directory holding tracked files has no entry, or an entry names
// a directory that no longer exists.
//
// Directories come from `git ls-files`, so build output (bin/, obj/,
// node_modules/, dist/) never counts, on a developer's machine or in CI. A
// directory holding only other directories, such as a `Features/` grouping,
// needs no entry of its own.
//
//   node tools/check-inventories.mjs
//
// The exit code is the contract.
import { execFileSync } from 'node:child_process'
import { readFileSync } from 'node:fs'
import { posix } from 'node:path'

export const INVENTORY = 'docs/source-inventory.md'

// Never source, even if one is ever committed by mistake.
const BUILD_OUTPUT = new Set(['bin', 'obj', 'node_modules'])

/** Every directory under src/ that directly holds a tracked file. */
export function sourceDirectories(files) {
	const directories = new Set()
	for (const file of files) {
		const directory = posix.dirname(file)
		if (directory === 'src' || !directory.startsWith('src/')) continue
		if (directory.split('/').some((segment) => BUILD_OUTPUT.has(segment))) continue
		directories.add(directory)
	}
	return directories
}

/**
 * The directory each inventory entry names: the link in the first cell of a
 * table row, resolved from docs/ to a path under src/.
 */
export function inventoryEntries(markdown) {
	return [...markdown.matchAll(/^\|\s*\[[^\]]*\]\(([^)\s]+)\)/gm)]
		.map(([, link]) => posix.normalize(posix.join('docs', link)).replace(/\/$/, ''))
		.filter((path) => path.startsWith('src/'))
}

/** What is wrong with the inventory, or an empty list. */
export function inventoryProblems({ files, markdown }) {
	const directories = sourceDirectories(files)
	const entries = inventoryEntries(markdown)
	const listed = new Set(entries)
	const tracked = new Set(files.map((file) => posix.dirname(file)).flatMap(ancestors))

	const problems = []
	for (const directory of [...directories].sort()) {
		if (!listed.has(directory)) problems.push(`${directory}/ has no entry in ${INVENTORY}.`)
	}
	for (const entry of entries) {
		if (!tracked.has(entry)) problems.push(`${INVENTORY} lists ${entry}/, which no longer exists.`)
	}
	return problems
}

/** A directory and every directory above it. */
function ancestors(directory) {
	const parts = directory.split('/')
	return parts.map((_, i) => parts.slice(0, i + 1).join('/'))
}

/**
 * Reports the way the command line does, without exiting, so a test can
 * exercise every outcome in-process. Returns the exit code.
 */
export function main({ files, markdown }) {
	const problems = inventoryProblems({ files, markdown })
	if (problems.length === 0) {
		console.log(`::notice::${INVENTORY} lists every directory under src/.`)
		return 0
	}

	for (const problem of problems) console.error(`::error file=${INVENTORY}::${problem}`)
	console.error('')
	console.error(`Add a row for each new directory to ${INVENTORY}, in the table for its project,`)
	console.error('saying what it holds and the decisions that govern it. Remove the row of a')
	console.error('directory that is gone.')
	return 1
}

const runAsCommand = String(process.argv[1]).endsWith('check-inventories.mjs')
if (runAsCommand) {
	const files = execFileSync('git', ['ls-files', '--', 'src'], { encoding: 'utf8' }).split('\n').filter(Boolean)
	process.exit(main({ files, markdown: readFileSync(INVENTORY, 'utf8') }))
}
