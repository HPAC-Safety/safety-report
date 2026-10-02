#!/usr/bin/env node
// Runs the repository's JavaScript suites with coverage (ci.yml `test`).
//
// The suite is passed to `node --test` as an explicit file list rather than as
// a directory: `node --test tests/js` resolves the path as a module and fails
// with MODULE_NOT_FOUND on the pinned Node. The lcov lands at
// ./artifacts/coverage/js/lcov.info, where the ReportGenerator glob in
// `coverage` finds it with the .NET and web reports.
import { mkdirSync, readdirSync } from 'node:fs'
import path from 'node:path'
import { annotation, exec as realExec, isMain } from '../lib/actions.mjs'

export const LCOV = './artifacts/coverage/js/lcov.info'

/** Every *.test.mjs and *.test.js file under `dir`, sorted, as repository-relative paths. */
export function findSuites(dir, readDir = (d) => readdirSync(d, { withFileTypes: true })) {
	let entries
	try {
		entries = readDir(dir)
	} catch {
		return []
	}
	const found = []
	for (const entry of entries) {
		const full = path.posix.join(dir, entry.name)
		if (entry.isDirectory()) found.push(...findSuites(full, readDir))
		else if (/\.test\.(mjs|js)$/.test(entry.name)) found.push(full)
	}
	return found.sort()
}

/** The `node` arguments that run `suites` with spec output on stdout and lcov in a file. */
export function testArgs(suites, lcov = LCOV) {
	return [
		'--test',
		'--experimental-test-coverage',
		'--test-reporter=spec',
		'--test-reporter-destination=stdout',
		'--test-reporter=lcov',
		`--test-reporter-destination=${lcov}`,
		...suites,
	]
}

export function main({ exec = realExec, log = console.log, root = 'tests/js', mkdir = mkdirSync } = {}) {
	const suites = findSuites(root)
	if (suites.length === 0) {
		log(annotation('notice', 'No JavaScript test suite yet — added by #8. Skipping.'))
		return 0
	}
	mkdir(path.dirname(LCOV), { recursive: true })
	return exec('node', testArgs(suites), { inherit: true }).status
}

if (isMain(import.meta.url)) process.exit(main())
