#!/usr/bin/env node
// Runs the repository's JavaScript suites with coverage (ci.yml `test`).
//
// The suite is passed to `node --test` as an explicit file list rather than as
// a directory: `node --test tests/js` resolves the path as a module and fails
// with MODULE_NOT_FOUND on the pinned Node. The lcov lands at
// ./artifacts/coverage/js/lcov.info, where the ReportGenerator glob in
// `coverage` finds it with the .NET and web reports.
import { type Dirent, mkdirSync, readdirSync } from 'node:fs'
import path from 'node:path'
import { type Exec, annotation, exec as realExec, isMain } from '../lib/actions.ts'

export const LCOV = './artifacts/coverage/js/lcov.info'

/** Every *.test.ts file under `dir`, sorted, as repository-relative paths. */
export function findSuites(dir: string, readDir: (dir: string) => readonly Dirent[] = (d) => readdirSync(d, { withFileTypes: true })): string[] {
	let entries: readonly Dirent[]
	try {
		entries = readDir(dir)
	} catch {
		return []
	}
	const found: string[] = []
	for (const entry of entries) {
		const full = path.posix.join(dir, entry.name)
		if (entry.isDirectory()) found.push(...findSuites(full, readDir))
		else if (/\.test\.ts$/.test(entry.name)) found.push(full)
	}
	return found.sort()
}

/** The `node` arguments that run `suites` with spec output on stdout and lcov in a file. */
export function testArgs(suites: readonly string[], lcov = LCOV): string[] {
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

export interface MainOptions {
	exec?: Exec
	log?: (line: string) => void
	root?: string
	mkdir?: (dir: string, options: { recursive: true }) => unknown
}

export function main({ exec = realExec, log = console.log, root = 'tests/js', mkdir = mkdirSync }: MainOptions = {}): number {
	const suites = findSuites(root)
	if (suites.length === 0) {
		log(annotation('notice', 'No JavaScript test suite yet — added by #8. Skipping.'))
		return 0
	}
	mkdir(path.dirname(LCOV), { recursive: true })
	return exec('node', testArgs(suites), { inherit: true }).status
}

if (isMain(import.meta.url)) process.exit(main())
