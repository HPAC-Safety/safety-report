import { describe, it } from 'node:test'
import assert from 'node:assert/strict'

import { main } from '../../../tools/infra/read-lockfile-change.ts'
import { fakeExec, tempFile } from './fake-exec.ts'

const diff = 'git diff --quiet -- infra/.terraform.lock.hcl'

describe('read-lockfile-change', () => {
	it('reports changed=false when git diff is quiet', () => {
		const out = tempFile('out')
		main({ env: { GITHUB_OUTPUT: out.file }, exec: fakeExec({ [diff]: { status: 0 } }).exec })
		assert.equal(out.read(), 'changed=false\n')
	})

	it('reports changed=true when git diff exits 1', () => {
		const out = tempFile('out')
		main({ env: { GITHUB_OUTPUT: out.file }, exec: fakeExec({ [diff]: { status: 1 } }).exec })
		assert.equal(out.read(), 'changed=true\n')
	})
})
