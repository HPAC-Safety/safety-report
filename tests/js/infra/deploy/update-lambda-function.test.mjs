import { describe, it } from 'node:test'
import assert from 'node:assert/strict'

import { main } from '../../../../tools/infra/deploy/update-lambda-function.mjs'
import { fakeExec } from '../fake-exec.mjs'

describe('update-lambda-function', () => {
	const env = { FUNCTION_NAME: 'fn', IMAGE_URI: 'r/api@sha256:a' }

	it('updates the code then waits for it', () => {
		const { exec, calls } = fakeExec()
		assert.equal(main({ env, exec }), 0)
		assert.deepEqual(calls, ['aws lambda update-function-code --function-name fn --image-uri r/api@sha256:a', 'aws lambda wait function-updated --function-name fn'])
	})

	it('does not wait when the update fails', () => {
		const { exec, calls } = fakeExec({ 'aws lambda update-function-code --function-name fn --image-uri r/api@sha256:a': { status: 1 } })
		assert.throws(() => main({ env, exec }))
		assert.equal(calls.length, 1)
	})
})
