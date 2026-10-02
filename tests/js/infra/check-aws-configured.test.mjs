import { describe, it } from 'node:test'
import assert from 'node:assert/strict'

import { decide, main } from '../../../tools/infra/check-aws-configured.mjs'
import { tempFile } from './fake-exec.mjs'

const base = { planRoleArn: 'arn:x', stateBucket: 'bucket', sameRepo: 'true', account: 'staging', accountUpper: 'STAGING' }

describe('decide', () => {
	it('skips a fork pull request', () => {
		const result = decide({ ...base, sameRepo: 'false' })
		assert.equal(result.ready, false)
		assert.match(result.notice, /^::notice::Fork pull request\./)
	})

	it('skips an account whose role is unset, naming the variables', () => {
		const result = decide({ ...base, planRoleArn: '' })
		assert.equal(result.ready, false)
		assert.match(result.notice, /AWS_PLAN_ROLE_ARN_STAGING or TF_STATE_BUCKET_STAGING is not set, so staging has not been bootstrapped yet\./)
	})

	it('skips an account whose state bucket is unset', () => {
		assert.equal(decide({ ...base, stateBucket: '' }).ready, false)
	})

	it('is ready when configured on a same-repo pull request', () => {
		assert.deepEqual(decide(base), { ready: true, notice: null })
	})
})

describe('check-aws-configured', () => {
	it('writes ready=true and prints nothing when configured', () => {
		const out = tempFile('out')
		const logs = []
		const env = { PLAN_ROLE_ARN: 'a', STATE_BUCKET: 'b', SAME_REPO: 'true', ACCOUNT: 'staging', ACCOUNT_UPPER: 'STAGING', GITHUB_OUTPUT: out.file }
		assert.equal(main({ env, log: (m) => logs.push(m) }), 0)
		assert.equal(out.read(), 'ready=true\n')
		assert.deepEqual(logs, [])
	})

	it('writes ready=false with the notice when skipped, still exiting 0', () => {
		const out = tempFile('out')
		const logs = []
		const env = { PLAN_ROLE_ARN: '', STATE_BUCKET: '', SAME_REPO: 'true', ACCOUNT: 'production', ACCOUNT_UPPER: 'PRODUCTION', GITHUB_OUTPUT: out.file }
		assert.equal(main({ env, log: (m) => logs.push(m) }), 0)
		assert.equal(out.read(), 'ready=false\n')
		assert.equal(logs.length, 1)
	})
})
