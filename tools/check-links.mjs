#!/usr/bin/env node
// Every relative link in tracked markdown resolves (ADR-0183).
//
// A link is checked the way GitHub renders it: relative to the file that holds
// it, or to the repository root when it starts with `/`. It resolves when it
// names a tracked file, or a directory that holds one. A `#anchor` on a link to
// a markdown file must name a heading (or an explicit `id`) in that file; a
// bare `#anchor` must name one in the file itself. External URLs are not
// fetched.
//
// Symlinks are skipped — `CLAUDE.md` is `AGENTS.md` — and so is everything in
// a code fence, an inline code span, or an HTML comment, which render as text.
// A C# file is read too, for the `<see href="…">` that links a decision.
//
//   node tools/check-links.mjs              every tracked markdown file
//   node tools/check-links.mjs a.md b.md    only these (the pre-commit hook)
//
// The exit code is the contract.
import { execFileSync } from 'node:child_process'
import { existsSync, lstatSync, readFileSync, readdirSync } from 'node:fs'
import { join, posix, relative, sep } from 'node:path'

import { anchors, links } from './markdown-links.mjs'

const ROOT = process.cwd()

const CHECKED = /\.(md|mdc|cs)$/

/**
 * Every tracked file, from `git ls-files` so ignored and generated trees never
 * count; a walk when there is no repository, which is how the tests run.
 */
export function trackedFiles(root = ROOT) {
	try {
		return execFileSync('git', ['-C', root, 'ls-files', '-z'], { encoding: 'utf8' }).split('\0').filter(Boolean)
	} catch {
		return walk(root, root)
	}
}

function walk(directory, root) {
	const skip = new Set(['.git', 'node_modules', 'obj', 'bin', 'graphify-out', '.claude'])
	return readdirSync(directory, { withFileTypes: true }).flatMap((entry) => {
		if (skip.has(entry.name)) return []
		const full = join(directory, entry.name)
		if (entry.isDirectory()) return walk(full, root)
		return [relative(root, full).split(sep).join('/')]
	})
}

/** The set of tracked files and every directory that holds one. */
export function index(files) {
	const fileSet = new Set(files)
	const directories = new Set()
	for (const file of files) {
		let directory = posix.dirname(file)
		while (directory !== '.' && !directories.has(directory)) {
			directories.add(directory)
			directory = posix.dirname(directory)
		}
	}
	return { files: fileSet, directories }
}

/** The repository path a link in `file` points at, or null when it climbs out. */
export function resolve(file, path) {
	let decoded = path
	try {
		decoded = decodeURIComponent(path)
	} catch {
		// A malformed escape is checked as written.
	}
	const joined = decoded.startsWith('/') ? decoded.slice(1) : posix.join(posix.dirname(file), decoded)
	const normalized = posix.normalize(joined).replace(/\/$/, '')
	if (normalized === '..' || normalized.startsWith('../')) return null
	return normalized === '' ? '.' : normalized
}

/** Every unresolvable link in one file, as `{ line, message }`. */
export function checkText(file, text, { files, directories }, read) {
	const problems = []
	for (const link of links(text, { html: file.endsWith('.cs') })) {
		if (link.external) continue

		if (link.path === '') {
			if (link.anchor && !anchorsOf(file, text, read).has(link.anchor.toLowerCase())) {
				problems.push({ line: link.line, message: `#${link.anchor} names no heading in this file` })
			}
			continue
		}

		const target = resolve(file, link.path)
		if (target === null) {
			problems.push({ line: link.line, message: `${link.raw} climbs out of the repository` })
			continue
		}
		if (target === '.' || directories.has(target)) continue
		if (!files.has(target)) {
			problems.push({ line: link.line, message: `${link.raw} does not resolve — ${target} is not a tracked file` })
			continue
		}
		if (link.anchor && /\.mdc?$/.test(target) && !anchorsOf(target, null, read).has(link.anchor.toLowerCase())) {
			problems.push({ line: link.line, message: `${link.raw} — #${link.anchor} names no heading in ${target}` })
		}
	}
	return problems
}

const anchorCache = new Map()
function anchorsOf(path, text, read) {
	if (!anchorCache.has(path)) {
		const offered = anchors(text ?? read(path))
		anchorCache.set(path, new Set([...offered].map((anchor) => anchor.toLowerCase())))
	}
	return anchorCache.get(path)
}

/**
 * Checks `paths` (or every tracked markdown file) under `root` and reports the
 * way the command line does, without exiting. Returns the exit code.
 */
export function main(root = ROOT, paths = []) {
	anchorCache.clear()
	const tracked = trackedFiles(root)
	const lookup = index(tracked)
	const read = (path) => readFileSync(join(root, path), 'utf8')

	const candidates = paths.length > 0 ? paths : tracked
	const checked = candidates
		.filter((path) => CHECKED.test(path))
		.filter((path) => existsSync(join(root, path)) && !lstatSync(join(root, path)).isSymbolicLink())
		.filter((path) => !path.endsWith('.cs') || read(path).includes('href='))
		.sort()

	let failures = 0
	for (const file of checked) {
		const text = read(file)
		for (const problem of checkText(file, text, lookup, read)) {
			failures += 1
			console.error(`::error file=${file},line=${problem.line}::${problem.message}`)
		}
	}

	if (failures > 0) {
		console.error(`\n${failures} broken link(s). Fix the path, or the heading the anchor names.`)
		return 1
	}
	console.log(`${checked.length} file(s) checked. Every relative link resolves.`)
	return 0
}

const runAsCommand = String(process.argv[1]).endsWith('check-links.mjs')
if (runAsCommand) process.exit(main(ROOT, process.argv.slice(2)))
