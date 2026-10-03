#!/usr/bin/env node
// Regenerates the traceability matrix, the step bindings, and the specification
// index in the pull request's checkout, with the BASE branch's generators
// (traceability.yml; ADR-0101, ADR-0183, ADR-0184).
//
// This is the trust boundary of the workflow. It runs from the base checkout
// and points the base generators at the head checkout's files as plain data:
// the generators read the feature files, the constraint pages, and step files
// (read as text, their patterns compiled, nothing in them run), and write
// three markdown files. No code the pull request wrote runs with the token.
//
// A pull request that changes a generator itself is skipped: the base
// generator would render the old format. Its author regenerates locally (the
// post-merge/post-rewrite hooks do it after every merge or rebase), and ci.yml's
// "docs" job still checks the result with the head's code.
//
// The matrix and the specification index are both generated from .spec/, so one
// run regenerates both and one commit carries them. `--no-fail` on the bindings:
// ci.yml's docs job is the gate (ADR-0184).
//
//   node base/tools/spec/regenerate-spec-docs.ts <base-dir> <head-dir>
//
// Output: `changed` (true when a generated file differs).
import { existsSync, readFileSync } from 'node:fs'
import path from 'node:path'

import { type Env, type Exec, exec, isMain, setOutput } from '../lib/actions.ts'

export const GENERATORS = ['generate-traceability.ts', 'generate-spec-index.ts', 'spec-paths.ts', 'generate-bindings.ts']
export const GENERATED_FILES = ['.spec/traceability.md', '.spec/README.md', '.spec/bindings.md']

/** The generators, in the order they run, with the arguments each takes. */
export const RUNS: readonly (readonly [string, ...string[]])[] = [['generate-traceability.ts'], ['generate-spec-index.ts'], ['generate-bindings.ts', '--no-fail']]

/** The file system calls the generator comparison makes, replaceable by a test. */
export interface FileAccess {
	exists?: (path: string) => boolean
	read?: (path: string) => Buffer
}

interface MainOptions extends FileAccess {
	argv?: readonly string[]
	env?: Env
	exec?: Exec
	log?: (line: string) => void
}

/** The first generator whose base and head copies differ (or are missing), or null. */
export function changedGenerator(base: string, head: string, { exists = existsSync, read = readFileSync }: FileAccess = {}): string | null {
	return (
		GENERATORS.find((tool) => {
			const [a, b] = [path.join(base, 'tools/spec', tool), path.join(head, 'tools/spec', tool)]
			return !(exists(a) && exists(b) && read(a).equals(read(b)))
		}) ?? null
	)
}

export function main({ argv = process.argv.slice(2), env = process.env, exec: run = exec, log = console.log, exists, read }: MainOptions = {}): number {
	const [base, head] = argv
	if (!base || !head) {
		log('::error::Usage: node tools/spec/regenerate-spec-docs.ts <base-dir> <head-dir>')
		return 1
	}

	const changed = changedGenerator(base, head, { exists, read })
	if (changed) {
		log(
			`::notice::This pull request changes tools/spec/${changed}, so the base generators can't render its output. Regenerate locally with 'node tools/spec/generate-traceability.ts && node tools/spec/generate-bindings.ts && node tools/spec/generate-spec-index.ts'; ci.yml checks the result.`,
		)
		setOutput('changed', 'false', env)
		return 0
	}

	for (const [tool, ...flags] of RUNS) {
		const result = run('node', [path.resolve(base, 'tools/spec', tool), ...flags], { cwd: head, inherit: true })
		if (result.status !== 0) return result.status
	}

	const diff = run('git', ['diff', '--quiet', '--', ...GENERATED_FILES], { cwd: head })
	setOutput('changed', diff.status === 0 ? 'false' : 'true', env)
	return 0
}

if (isMain(import.meta.url)) process.exit(main())
