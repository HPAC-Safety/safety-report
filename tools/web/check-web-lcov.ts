#!/usr/bin/env node
// The web logic's coverage reaches the ratchet (ADR-0188, #769).
//
// Run after `npm --prefix src/web run test:coverage`. The `coverage` job only
// runs on GitHub and under `tools/dev/ci-local.sh --full`, so on its own the path
// from Vitest to the merged report would first be exercised on GitHub. This
// proves the hand-off from the `web` job, which the fast local gate runs:
//
//   - the lcov exists where vite.config.ts writes it;
//   - that path is one the ReportGenerator `-reports:` globs in ci.yml read,
//     taken from ci.yml itself so the two cannot drift;
//   - every file it names is relative to the repository root and exists, so
//     the merged report can show its source.
//
// Usage: node tools/web/check-web-lcov.ts [lcov-path]
import { existsSync, readFileSync } from 'node:fs'
import { isAbsolute, join } from 'node:path'
import { fileURLToPath } from 'node:url'

export const REPO_ROOT = fileURLToPath(new URL('../..', import.meta.url))
export const DEFAULT_LCOV = 'artifacts/coverage/web/lcov.info'
export const CI_WORKFLOW = '.github/workflows/ci.yml'

/** The ReportGenerator `-reports:` globs a workflow merges, without a leading `./`. */
export function reportGlobs(workflow: string): string[] {
	const match = workflow.match(/-reports:([^"\s]+)/)
	const list = match?.[1]
	if (list === undefined) return []
	return list.split(';').map((glob) => glob.trim().replace(/^\.\//, '')).filter(Boolean)
}

/** Whether a repository-relative path matches a glob of `*` (one segment) and `**` (any segments). */
export function globMatches(glob: string, path: string): boolean {
	let pattern = ''
	for (let i = 0; i < glob.length; i++) {
		if (glob.startsWith('**/', i)) {
			pattern += '(?:[^/]+/)*'
			i += 2
		} else if (glob[i] === '*') pattern += '[^/]*'
		else pattern += (glob[i] ?? '').replace(/[.+?^${}()|[\]\\]/, '\\$&')
	}
	return new RegExp(`^${pattern}$`).test(path)
}

/** The `SF:` paths an lcov file names. */
export function sourceFiles(lcov: string): string[] {
	return lcov
		.split('\n')
		.filter((line) => line.startsWith('SF:'))
		.map((line) => line.slice(3).trim())
}

/** Every problem with an lcov at `lcovPath`, given the workflow text and a repository root. */
export interface LcovInput {
	lcovPath: string
	lcov: string
	workflow: string
	root: string
}

export function problems({ lcovPath, lcov, workflow, root }: LcovInput): string[] {
	const found: string[] = []
	const globs = reportGlobs(workflow)
	if (globs.length === 0) found.push(`${CI_WORKFLOW} has no ReportGenerator -reports: globs`)
	else if (!globs.some((glob) => globMatches(glob, lcovPath))) {
		found.push(`${lcovPath} is not read by the coverage merge (${globs.join(';')})`)
	}
	const files = sourceFiles(lcov)
	if (files.length === 0) found.push(`${lcovPath} names no source file`)
	for (const file of files) {
		if (isAbsolute(file)) found.push(`${file} is absolute; name it from the repository root`)
		else if (!existsSync(join(root, file))) found.push(`${file} does not exist from the repository root`)
	}
	return found
}

export function main(argv: readonly string[], root: string = REPO_ROOT): number {
	const lcovPath = argv[0] ?? DEFAULT_LCOV
	const absolute = join(root, lcovPath)
	if (!existsSync(absolute)) {
		console.error(`check-web-lcov: ${lcovPath} does not exist; run npm --prefix src/web run test:coverage first`)
		return 1
	}
	const found = problems({
		lcovPath,
		lcov: readFileSync(absolute, 'utf8'),
		workflow: readFileSync(join(root, CI_WORKFLOW), 'utf8'),
		root,
	})
	for (const problem of found) console.error(`check-web-lcov: ${problem}`)
	if (found.length > 0) return 1
	const count = sourceFiles(readFileSync(absolute, 'utf8')).length
	console.log(`check-web-lcov: ${lcovPath} names ${count} file(s) from the repository root, where the coverage merge reads it`)
	return 0
}

if (process.argv[1] === fileURLToPath(import.meta.url)) process.exit(main(process.argv.slice(2)))
