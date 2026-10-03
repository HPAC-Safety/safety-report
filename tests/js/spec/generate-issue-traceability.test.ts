import { describe, it } from 'node:test'
import assert from 'node:assert/strict'

import type { Exec } from '../../../tools/lib/actions.ts'
import { type Api, PAGE, findToken, listedIssues } from '../../../tools/spec/check-issue-traceability.ts'
import { main } from '../../../tools/spec/generate-issue-traceability.ts'

const noLogin: Exec = () => ({ status: 1, stdout: '', stderr: 'not logged in' })
const login: Exec = () => ({ status: 0, stdout: 'from-gh', stderr: '' })

/** A GitHub client serving one open issue and one pull request. */
const api: Api = {
	list: () =>
		Promise.resolve([
			{ number: 5, title: 'An issue' },
			{ number: 6, title: 'A pull request', pull_request: {} },
		]),
	request: () => Promise.reject(new Error('the generator never writes to GitHub')),
}

/** Runs `main` with console output silenced, restoring it afterwards even on failure. */
async function quietly<T>(run: () => Promise<T>): Promise<T> {
	const [log, error] = [console.log, console.error]
	console.log = () => undefined
	console.error = () => undefined
	try {
		return await run()
	} finally {
		console.log = log
		console.error = error
	}
}

describe('findToken', () => {
	it('prefers GITHUB_TOKEN, then GH_TOKEN, then the gh login', () => {
		assert.equal(findToken({ env: { GITHUB_TOKEN: 'a', GH_TOKEN: 'b' }, exec: login }), 'a')
		assert.equal(findToken({ env: { GH_TOKEN: 'b' }, exec: login }), 'b')
		assert.equal(findToken({ env: {}, exec: login }), 'from-gh')
	})

	it('finds none without a variable or a login', () => {
		assert.equal(findToken({ env: {}, exec: noLogin }), undefined)
	})

	it('finds none when gh is not installed', () => {
		const missing: Exec = () => {
			throw new Error('spawnSync gh ENOENT')
		}

		assert.equal(findToken({ env: {}, exec: missing }), undefined)
	})
})

describe('main', () => {
	it('writes the page from the open issues, pull requests left out', async () => {
		const written: { path: string; text: string }[] = []

		const code = await quietly(() =>
			main({
				env: { GITHUB_TOKEN: 't', GITHUB_REPOSITORY: 'o/r' },
				exec: noLogin,
				write: (path, text) => written.push({ path, text }),
				makeApi: () => api,
			}),
		)

		assert.equal(code, 0)
		assert.equal(written.length, 1)
		assert.equal(written[0]?.path, PAGE)
		assert.deepEqual(listedIssues(written[0]?.text ?? ''), [5])
		assert.match(written[0]?.text ?? '', /https:\/\/github\.com\/o\/r\/issues\/5/)
	})

	it('writes nothing and exits 2 without a token', async () => {
		const written: string[] = []

		const code = await quietly(() => main({ env: {}, exec: noLogin, write: (path) => written.push(path), makeApi: () => api }))

		assert.equal(code, 2)
		assert.deepEqual(written, [])
	})
})
