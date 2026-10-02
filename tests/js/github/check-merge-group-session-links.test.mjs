import { describe, it } from 'node:test'
import assert from 'node:assert/strict'

import { main } from '../../../tools/github/check-merge-group-session-links.mjs'

function fakeExec({ commits, subjects = {}, messages = {} }) {
	const calls = []
	const exec = (command, args) => {
		calls.push([command, ...args])
		if (args[0] === 'rev-list') return { status: 0, stdout: commits, stderr: '' }
		const commit = args.at(-1)
		return { status: 0, stdout: (args[2] === '--format=%s' ? subjects : messages)[commit] ?? '', stderr: '' }
	}
	return { exec, calls }
}

function capture(fn) {
	const original = console.error
	const errors = []
	console.error = (...args) => errors.push(args.join(' '))
	try {
		return { result: fn(), errors }
	} finally {
		console.error = original
	}
}

describe('check merge group session links', () => {
	it('fails an empty range', () => {
		const { exec } = fakeExec({ commits: '' })
		const { result, errors } = capture(() => main({ env: { BASE_SHA: 'abc' }, exec, log: () => {} }))
		assert.equal(result, 1)
		assert.deepEqual(errors, ['::error::The merge group holds no commit between abc and HEAD, so nothing was checked.'])
	})

	it('checks every commit and fails when any carries a link', () => {
		const link = 'https://claude.ai/code/session_abc123'
		const { exec, calls } = fakeExec({
			commits: 'c1\nc2\nc3',
			subjects: { c1: 'one', c2: 'two', c3: 'three' },
			messages: { c1: 'clean', c2: `body\n${link}`, c3: 'clean' },
		})
		const logged = []
		const { result, errors } = capture(() => main({ env: { BASE_SHA: 'abc' }, exec, log: (line) => logged.push(line) }))
		assert.equal(result, 1)
		assert.deepEqual(logged, ['::group::one', '::endgroup::', '::group::two', '::endgroup::', '::group::three', '::endgroup::'])
		assert.match(errors[0], /The commit message carries a Claude session link/)
		assert.deepEqual(calls[0], ['git', 'rev-list', '--reverse', 'abc..HEAD'])
	})

	it('passes when no commit carries a link', () => {
		const { exec } = fakeExec({ commits: 'c1', subjects: { c1: 'one' }, messages: { c1: 'clean' } })
		assert.equal(main({ env: { BASE_SHA: 'abc' }, exec, log: () => {} }), 0)
	})
})
