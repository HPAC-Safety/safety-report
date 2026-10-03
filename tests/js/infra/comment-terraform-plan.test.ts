import { describe, it } from 'node:test'
import assert from 'node:assert/strict'

import { commentBody, main, TAIL_BYTES } from '../../../tools/infra/comment-terraform-plan.ts'
import { fakeExec } from './fake-exec.ts'

describe('commentBody', () => {
	it('wraps the plan in the marker, heading, and details block', () => {
		const body = commentBody({ account: 'staging', outcome: 'success', planBytes: Buffer.from('Plan: 1 to add\n') }).toString()
		assert.equal(
			body,
			[
				'<!-- terraform-plan-staging -->',
				'### Terraform plan — staging — `success`',
				'',
				'<details><summary>Plan output</summary>',
				'',
				'```terraform',
				'Plan: 1 to add',
				'```',
				'',
				'</details>',
				'',
			].join('\n'),
		)
	})

	it('keeps only the last 60000 bytes of a long plan', () => {
		const plan = Buffer.from(`${'a'.repeat(TAIL_BYTES)}END`)
		const body = commentBody({ account: 'x', outcome: 'failure', planBytes: plan }).toString()
		assert.ok(body.includes('```terraform\naaa'))
		assert.ok(body.includes('aEND```'))
		assert.equal(body.split('a').length - 1 >= TAIL_BYTES - 3, true)
		assert.equal(body.includes('a'.repeat(TAIL_BYTES)), false)
	})
})

describe('comment-terraform-plan', () => {
	it('writes comment.md then posts it, editing the last comment', () => {
		const { exec, calls } = fakeExec()
		let written: Buffer | undefined
		const env = { ACCOUNT: 'production', PLAN_OUTCOME: 'failure', PR_NUMBER: '12' }
		assert.equal(main({ env, exec, readPlan: () => Buffer.from('boom\n'), writeComment: (b) => {
				written = b
			},
		}), 0)
		assert.match(written?.toString() ?? '', /^<!-- terraform-plan-production -->/)
		assert.deepEqual(calls, ['gh pr comment 12 --body-file comment.md --edit-last --create-if-none'])
	})
})
