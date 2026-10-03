import { describe, it } from 'node:test'
import assert from 'node:assert/strict'
import { spawnSync } from 'node:child_process'
import { mkdtempSync, rmSync, writeFileSync } from 'node:fs'
import { tmpdir } from 'node:os'
import { join } from 'node:path'

import { main } from '../../../tools/build/check-clean-worktree.ts'
import type { Exec } from '../../../tools/lib/actions.ts'

describe('check-clean-worktree', () => {
	it('passes on a clean tree', () => {
		const calls: string[][] = []
		const exec: Exec = (command, args = []) => {
			calls.push([command, ...args])
			return { status: 0, stdout: '', stderr: '' }
		}
		assert.equal(main({ argv: ['msg:'], exec, log: () => assert.fail('logged') }), 0)
		assert.deepEqual(calls, [['git', 'status', '--porcelain']])
	})

	it('fails with the message, then lists the changes', () => {
		const calls: (string | boolean)[][] = []
		const logs: string[] = []
		const exec: Exec = (command, args = [], options) => {
			calls.push([command, ...args, options?.inherit ?? false])
			return { status: 0, stdout: ' M a', stderr: '' }
		}
		assert.equal(main({ argv: ['Building the app modified tracked files:'], exec, log: (m) => logs.push(m) }), 1)
		assert.deepEqual(logs, ['::error::Building the app modified tracked files:'])
		assert.deepEqual(calls.at(-1), ['git', 'status', '--porcelain', true])
	})

	it('needs a message', () => {
		assert.equal(main({ argv: [], exec: () => assert.fail('ran'), log: () => undefined }), 2)
	})

	it('sees an untracked file in a real repository', () => {
		const dir = mkdtempSync(join(tmpdir(), 'clean-worktree-'))
		const cwd = process.cwd()
		spawnSync('git', ['init', '-q', dir])
		writeFileSync(join(dir, 'new.txt'), 'x')
		process.chdir(dir)
		try {
			assert.equal(main({ argv: ['dirty:'], log: () => undefined }), 1)
		} finally {
			process.chdir(cwd)
			rmSync(dir, { recursive: true })
		}
	})
})
