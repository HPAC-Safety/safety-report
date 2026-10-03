import { describe, it } from 'node:test'
import assert from 'node:assert/strict'

import type { Exec } from '../../../tools/lib/actions.ts'
import { main } from '../../../tools/github/check-pr-screenshots-diff.ts'

describe('check pr screenshots diff', () => {
	it('passes a change that touches no rendered web file', () => {
		const calls: (readonly string[])[] = []
		const exec: Exec = (command, args = []) => {
			calls.push([command, ...args])
			return { status: 0, stdout: 'docs/a.md\nsrc/HpacSafety.Api/Program.cs', stderr: '' }
		}
		const original = console.log
		console.log = () => {}
		try {
			assert.equal(main({ env: { BASE_SHA: 'abc', PR_BODY: 'Closes #1' }, exec }), 0)
		} finally {
			console.log = original
		}
		assert.deepEqual(calls, [['git', 'diff', '--name-only', 'abc...HEAD']])
	})

	it('fails without a body to check', () => {
		const exec: Exec = () => ({ status: 0, stdout: '', stderr: '' })
		const original = console.error
		console.error = () => {}
		try {
			assert.equal(main({ env: { BASE_SHA: 'abc' }, exec }), 1)
		} finally {
			console.error = original
		}
	})
})
