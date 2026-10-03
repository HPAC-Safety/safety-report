import { describe, it } from 'node:test'
import assert from 'node:assert/strict'

import { healthUrl, main } from '../../../../tools/infra/deploy/smoke-test-health.ts'
import { fakeExec } from '../fake-exec.ts'

const curl = (url: string) => `curl -fsS -o /dev/null -w %{http_code} ${url}`

describe('healthUrl', () => {
	it('strips trailing slashes before appending the path', () => {
		assert.equal(healthUrl('https://x.example/'), 'https://x.example/api/health')
		assert.equal(healthUrl('https://x.example///'), 'https://x.example/api/health')
		assert.equal(healthUrl('https://x.example'), 'https://x.example/api/health')
	})
})

describe('smoke-test-health', () => {
	const env = { PUBLIC_URL: 'https://x.example/' }
	const url = 'https://x.example/api/health'

	it('passes on the first 200', () => {
		const { exec } = fakeExec({ [curl(url)]: { stdout: '200' } })
		const logs: string[] = []
		assert.equal(main({ env, exec, log: (m) => logs.push(m), sleep() {} }), 0)
		assert.deepEqual(logs, [`Healthy: ${url}`])
	})

	it('retries after a curl failure, waiting 6 seconds, and then passes', () => {
		let attempt = 0
		const { exec } = fakeExec({ [curl(url)]: () => (++attempt < 3 ? { status: 22, stdout: '503' } : { stdout: '200' }) })
		const sleeps: number[] = []
		assert.equal(main({ env, exec, log() {}, sleep: (s) => sleeps.push(s) }), 0)
		assert.deepEqual(sleeps, [6, 6])
	})

	it('keeps trying on a non-200 success status', () => {
		let attempt = 0
		const { exec } = fakeExec({ [curl(url)]: () => ({ stdout: ++attempt < 2 ? '204' : '200' }) })
		assert.equal(main({ env, exec, log() {}, sleep() {} }), 0)
	})

	it('gives up after ten attempts with an error annotation', () => {
		const { exec, calls } = fakeExec({ [curl(url)]: { status: 22 } })
		const logs: string[] = []
		assert.equal(main({ env, exec, log: (m) => logs.push(m), sleep() {} }), 1)
		assert.equal(calls.length, 10)
		assert.deepEqual(logs, [`::error::/api/health did not answer 200 at ${url}.`])
	})
})
