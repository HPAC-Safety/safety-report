// A recording stand-in for tools/lib/actions.ts's exec. `replies` maps a
// command line ("terraform -chdir=infra show -json") to a `{ status, stdout }`
// or a function returning one; an unlisted command succeeds with no output.
import { mkdtempSync, readFileSync, writeFileSync } from 'node:fs'
import { tmpdir } from 'node:os'
import path from 'node:path'
import type { Exec, ExecOptions, ExecResult } from '../../../tools/lib/actions.ts'

/** A canned reply, or a function of the call's options returning one. */
export type Reply = Partial<ExecResult> | ((options: ExecOptions) => Partial<ExecResult>)

export function fakeExec(replies: Partial<Record<string, Reply>> = {}): { exec: Exec; calls: string[] } {
	const calls: string[] = []
	const exec: Exec = (command, args = [], options = {}) => {
		const line = [command, ...args].join(' ')
		calls.push(line)
		const reply = replies[line]
		const outcome = typeof reply === 'function' ? reply(options) : (reply ?? {})
		const result = { status: 0, stdout: '', stderr: '', ...outcome }
		if (options.check && result.status !== 0) throw new Error(`${line} exited ${result.status}`)
		return result
	}
	return { exec, calls }
}

/** A temp file standing in for $GITHUB_OUTPUT or $GITHUB_STEP_SUMMARY, with a reader. */
export function tempFile(name: string): { file: string; read: () => string } {
	const file = path.join(mkdtempSync(path.join(tmpdir(), 'infra-test-')), name)
	writeFileSync(file, '')
	return { file, read: () => readFileSync(file, 'utf8') }
}
