import { describe, it } from 'node:test'
import assert from 'node:assert/strict'
import { mkdtempSync, readFileSync, rmSync, writeFileSync } from 'node:fs'
import { tmpdir } from 'node:os'
import { join } from 'node:path'

import type { Exec, ExecResult } from '../../../tools/lib/actions.ts'
import { main } from '../../../tools/coverage/fetch-coverage-baseline.ts'

const setup = ({ finder, download = { status: 0 }, present = true }: { finder: Partial<ExecResult>; download?: Partial<ExecResult>; present?: boolean }) => {
	const dir = mkdtempSync(join(tmpdir(), 'fetch-baseline-'))
	const output = join(dir, 'output')
	writeFileSync(output, '')
	const calls: string[][] = []
	const logs: string[] = []
	const exec: Exec = (command, args = []) => {
		calls.push([command, ...args])
		const result = command === 'node' ? finder : download
		return { status: 0, stdout: '', stderr: '', ...result }
	}
	const code = () => main({ env: { GITHUB_OUTPUT: output, GITHUB_REPOSITORY: 'o/r' }, exec, log: (m) => logs.push(m), exists: () => present })
	return { dir, output, calls, logs, code, read: () => readFileSync(output, 'utf8') }
}

describe('fetch-coverage-baseline', () => {
	it('outputs the downloaded baseline and its run', () => {
		const t = setup({ finder: { stdout: '42', stderr: '::notice::Baseline from main run 42.' } })
		try {
			assert.equal(t.code(), 0)
			assert.equal(t.read(), 'path=./artifacts/baseline/Cobertura.xml\nrun-id=42\n')
			assert.deepEqual(t.calls, [
				['node', 'tools/coverage/find-coverage-baseline.ts', '--repo', 'o/r'],
				['gh', 'run', 'download', '42', '--name', 'coverage-report', '--dir', './artifacts/baseline'],
			])
			assert.deepEqual(t.logs, ['::notice::Baseline from main run 42.'])
		} finally {
			rmSync(t.dir, { recursive: true })
		}
	})

	it('outputs none when the finder finds nothing', () => {
		const t = setup({ finder: { stdout: 'none' } })
		try {
			assert.equal(t.code(), 0)
			assert.equal(t.read(), 'path=none\nrun-id=none\n')
			assert.equal(t.calls.length, 1)
		} finally {
			rmSync(t.dir, { recursive: true })
		}
	})

	it('treats a failed finder as no baseline', () => {
		const t = setup({ finder: { status: 1, stdout: '' } })
		try {
			assert.equal(t.code(), 0)
			assert.equal(t.read(), 'path=none\nrun-id=none\n')
		} finally {
			rmSync(t.dir, { recursive: true })
		}
	})

	it('skips the ratchet with a notice when the download fails', () => {
		const t = setup({ finder: { stdout: '42' }, download: { status: 1 } })
		try {
			assert.equal(t.code(), 0)
			assert.equal(t.read(), 'path=none\nrun-id=none\n')
			assert.deepEqual(t.logs, ['::notice::main run 42 reported a coverage-report artifact, but the download failed. Ratchet skipped; the floor still applies.'])
		} finally {
			rmSync(t.dir, { recursive: true })
		}
	})

	it('skips the ratchet when the download leaves no Cobertura.xml', () => {
		const t = setup({ finder: { stdout: '42' }, present: false })
		try {
			t.code()
			assert.equal(t.read(), 'path=none\nrun-id=none\n')
		} finally {
			rmSync(t.dir, { recursive: true })
		}
	})
})
