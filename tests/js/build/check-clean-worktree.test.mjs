import { describe, it } from 'node:test'
import assert from 'node:assert/strict'
import { spawnSync } from 'node:child_process'
import { mkdtempSync, rmSync, writeFileSync } from 'node:fs'
import { tmpdir } from 'node:os'
import { join } from 'node:path'

import { main } from '../../../tools/build/check-clean-worktree.mjs'

describe('check-clean-worktree', () => {
	it('passes on a clean tree', () => {
		const calls = []
		const exec = (command, args) => (calls.push([command, ...args]), { stdout: '' })
		assert.equal(main({ argv: ['msg:'], exec, log: () => assert.fail('logged') }), 0)
		assert.deepEqual(calls, [['git', 'status', '--porcelain']])
	})

	it('fails with the message, then lists the changes', () => {
		const calls = []
		const logs = []
		const exec = (command, args, options) => (calls.push([command, ...args, options?.inherit ?? false]), { stdout: ' M a' })
		assert.equal(main({ argv: ['Building the app modified tracked files:'], exec, log: (m) => logs.push(m) }), 1)
		assert.deepEqual(logs, ['::error::Building the app modified tracked files:'])
		assert.deepEqual(calls.at(-1), ['git', 'status', '--porcelain', true])
	})

	it('needs a message', () => {
		assert.equal(main({ argv: [], exec: () => assert.fail('ran'), log: () => {} }), 2)
	})

	it('sees an untracked file in a real repository', () => {
		const dir = mkdtempSync(join(tmpdir(), 'clean-worktree-'))
		const cwd = process.cwd()
		spawnSync('git', ['init', '-q', dir])
		writeFileSync(join(dir, 'new.txt'), 'x')
		process.chdir(dir)
		try {
			assert.equal(main({ argv: ['dirty:'], log: () => {} }), 1)
		} finally {
			process.chdir(cwd)
			rmSync(dir, { recursive: true })
		}
	})
})
