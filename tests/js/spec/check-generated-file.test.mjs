import { describe, it } from 'node:test'
import assert from 'node:assert/strict'

import { main, parseArgs } from '../../../tools/spec/check-generated-file.mjs'

const argv = ['--generator', 'tools/spec/gen.mjs', '--file', '.spec/x.md', '--message', 'Out of date.']

const fakeExec = (diffStatus) => {
	const calls = []
	const exec = (command, args) => {
		calls.push([command, ...args])
		return { status: command === 'git' && args[0] === 'diff' && args.includes('--exit-code') ? diffStatus : 0, stdout: '', stderr: '' }
	}
	return { calls, exec }
}

describe('check-generated-file', () => {
	it('parses name/value pairs', () => {
		assert.deepEqual(parseArgs(argv), { generator: 'tools/spec/gen.mjs', file: '.spec/x.md', message: 'Out of date.' })
	})

	it('passes when regenerating changes nothing', () => {
		const { calls, exec } = fakeExec(0)
		const logs = []
		assert.equal(main({ argv, exec, log: (m) => logs.push(m) }), 0)
		assert.deepEqual(calls, [['node', 'tools/spec/gen.mjs'], ['git', 'diff', '--exit-code', '--stat', '--', '.spec/x.md']])
		assert.deepEqual(logs, [])
	})

	it('fails with the annotation on the file, then shows the diff, when it changed', () => {
		const { calls, exec } = fakeExec(1)
		const logs = []
		assert.equal(main({ argv, exec, log: (m) => logs.push(m) }), 1)
		assert.deepEqual(logs, ['::error file=.spec/x.md::Out of date.'])
		assert.deepEqual(calls.at(-1), ['git', 'diff', '--', '.spec/x.md'])
	})

	it('requires all three arguments', () => {
		const { calls, exec } = fakeExec(0)
		assert.equal(main({ argv: ['--file', 'a'], exec, log: () => {} }), 2)
		assert.deepEqual(calls, [])
	})
})
