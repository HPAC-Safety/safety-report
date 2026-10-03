#!/usr/bin/env node
// Runs the coverage gate once and keeps its output as comment.md (ci.yml
// `coverage`, "Gate").
//
// Output is captured to comment.md here, once, rather than re-run by the
// comment step - tools/coverage/check-coverage.ts writes to
// $GITHUB_STEP_SUMMARY as a side effect, and running it twice duplicated the
// table in the job summary. comment.md starts with the marker the pull
// request comment is recognised by, and holds the gate's stdout and stderr
// interleaved as it wrote them, like `2>&1 | tee comment.md`. The output is
// echoed to the log afterwards, and the gate's exit code is this one's.
//
//   node tools/coverage/write-coverage-comment.ts [check-coverage options]
//
// The options pass through to check-coverage.ts after --report, --baseline,
// and --baseline-run-id, which come from the environment: BASELINE_PATH and
// BASELINE_RUN_ID (each `none` when there is no baseline).
import { closeSync, openSync, readFileSync, writeSync } from 'node:fs'
import { spawnSync } from 'node:child_process'
import { type Env, isMain } from '../lib/actions.ts'

export const MARKER = '<!-- coverage-gate -->'
export const REPORT = './artifacts/report/Cobertura.xml'

/** The check-coverage arguments for this run. */
export function gateArgs(checker: string, env: Env, extra: readonly string[]): string[] {
	return [checker, '--report', REPORT, '--baseline', env.BASELINE_PATH || 'none', '--baseline-run-id', env.BASELINE_RUN_ID || 'none', ...extra]
}

export type Spawn = (command: string, args: string[], options: { stdio: ['ignore', number, number] }) => { status: number | null }

export interface MainOptions {
	argv?: readonly string[]
	env?: Env
	write?: (text: string) => unknown
	comment?: string
	checker?: string
	spawn?: Spawn
}

export function main({
	argv = process.argv.slice(2),
	env = process.env,
	write = (text: string) => process.stdout.write(text),
	comment = 'comment.md',
	checker = 'tools/coverage/check-coverage.ts',
	spawn = spawnSync,
}: MainOptions = {}): number {
	const fd = openSync(comment, 'w')
	let status: number
	try {
		writeSync(fd, `${MARKER}\n`)
		status = spawn('node', gateArgs(checker, env, argv), { stdio: ['ignore', fd, fd] }).status ?? 1
	} finally {
		closeSync(fd)
	}
	write(readFileSync(comment, 'utf8'))
	return status
}

if (isMain(import.meta.url)) process.exit(main())
