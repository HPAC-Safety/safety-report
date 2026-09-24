import { describe, it } from 'node:test'
import assert from 'node:assert/strict'

import { SESSION_LINKS, findSessionLinks, main } from '../../tools/agent-session-links.mjs'

/** Runs `main` with console output captured, restoring it afterwards even on failure. */
function runMain(text) {
	const errors = []
	const original = console.error
	console.error = (...args) => errors.push(args.join(' '))
	try {
		return { code: main(text, 'The pull-request body'), errors }
	} finally {
		console.error = original
	}
}

describe('findSessionLinks', () => {
	it('finds a Claude session link anywhere in the text', () => {
		const found = findSessionLinks('## Why\n\nCloses #1\n\nhttps://claude.ai/code/session_01AbCdEfGh\n')

		assert.deepEqual(found, [{ agent: 'Claude', link: 'https://claude.ai/code/session_01AbCdEfGh' }])
	})

	it('finds one in a trailer line, whatever its case', () => {
		const found = findSessionLinks('Subject\n\nClaude-Session: HTTPS://Claude.ai/code/session_01XyZ')

		assert.equal(found.length, 1)
	})

	it('leaves other claude.ai links alone', () => {
		assert.deepEqual(findSessionLinks('See https://claude.ai/code/artifact/abc and https://claude.ai/new'), [])
	})

	it('checks every agent in the collection', () => {
		const links = [...SESSION_LINKS, { agent: 'Other', pattern: /https:\/\/agent\.example\/s\/\w+/g }]

		const found = findSessionLinks('https://agent.example/s/abc and https://claude.ai/code/session_01A', links)

		assert.deepEqual(found.map(({ agent }) => agent).sort(), ['Claude', 'Other'])
	})
})

describe('main', () => {
	it('passes text with no session link', () => {
		assert.equal(runMain('## Why\n\nCloses #1\n').code, 0)
	})

	it('fails and annotates a session link', () => {
		const { code, errors } = runMain('Closes #1\n\nhttps://claude.ai/code/session_01AbC\n')

		assert.equal(code, 1)
		assert.match(errors.join('\n'), /::error::The pull-request body carries a Claude session link/)
	})
})
