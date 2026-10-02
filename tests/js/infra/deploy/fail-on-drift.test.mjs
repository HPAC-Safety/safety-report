import { describe, it } from 'node:test'
import assert from 'node:assert/strict'

import { DRIFT_ERROR, judge, main } from '../../../../tools/infra/deploy/fail-on-drift.mjs'
import { fakeExec } from '../fake-exec.mjs'

describe('judge', () => {
	it('0 converged', () => assert.deepEqual(judge(0), { exit: 0, message: 'Converged: a plan right after apply is empty.' }))
	it('2 is drift, failing the job', () => assert.deepEqual(judge(2), { exit: 1, message: DRIFT_ERROR }))
	it('anything else passes its code through silently', () => assert.deepEqual(judge(1), { exit: 1, message: null }))
})

describe('fail-on-drift', () => {
	const env = { GITHUB_WORKSPACE: '/w', TFVARS_FILE: 'infra/staging.tfvars' }
	const plan = 'terraform -chdir=infra plan -no-color -detailed-exitcode -var-file=/w/infra/staging.tfvars'

	for (const [status, exit, logged] of [[0, 0, 1], [2, 1, 1], [1, 1, 0]]) {
		it(`exits ${exit} for plan status ${status}`, () => {
			const { exec, calls } = fakeExec({ [plan]: { status } })
			const logs = []
			assert.equal(main({ env, exec, log: (m) => logs.push(m) }), exit)
			assert.deepEqual(calls, [plan])
			assert.equal(logs.length, logged)
		})
	}
})
