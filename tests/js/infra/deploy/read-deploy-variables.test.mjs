import { describe, it } from 'node:test'
import assert from 'node:assert/strict'

import { main, render } from '../../../../tools/infra/deploy/read-deploy-variables.mjs'
import { fakeExec, tempFile } from '../fake-exec.mjs'

describe('read-deploy-variables', () => {
	it('writes one KEY=value per entry, in order', () => {
		const out = tempFile('out')
		const { exec, calls } = fakeExec({
			'terraform -chdir=infra output -json deploy_variables': { stdout: '{"B":"two","A":"one","N":3}' },
		})
		assert.equal(main({ env: { GITHUB_OUTPUT: out.file }, exec }), 0)
		assert.equal(out.read(), 'B=two\nA=one\nN=3\n')
		assert.equal(calls.length, 1)
	})

	it('renders non-strings as JSON', () => {
		assert.equal(render({ a: 1 }), '{"a":1}')
	})
})
