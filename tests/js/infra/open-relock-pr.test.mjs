import { describe, it } from 'node:test'
import assert from 'node:assert/strict'

import { BODY, DEFAULT_TOKEN_WARNING, main } from '../../../tools/infra/open-relock-pr.mjs'
import { fakeExec } from './fake-exec.mjs'

const env = { GH_TOKEN: 'tok', RELOCK_BRANCH: 'chore/relock', GITHUB_REPOSITORY: 'o/r' }
const view = 'gh pr view chore/relock --json number --jq .number'

describe('open-relock-pr', () => {
	it('pushes the branch and creates a pull request when none is open', () => {
		const { exec, calls } = fakeExec({ [view]: { status: 1 } })
		const logs = []
		assert.equal(main({ env: { ...env, USING_DEFAULT_TOKEN: 'false' }, exec, log: (m) => logs.push(m) }), 0)
		assert.deepEqual(calls, [
			'git config user.name github-actions[bot]',
			'git config user.email 41898282+github-actions[bot]@users.noreply.github.com',
			'git remote set-url origin https://x-access-token:tok@github.com/o/r.git',
			'git switch -C chore/relock',
			'git add infra/.terraform.lock.hcl',
			'git commit -m Relock Terraform providers',
			'git push --force origin chore/relock',
			view,
			`gh pr create --base main --head chore/relock --title Relock Terraform providers --body ${BODY}`,
		])
		assert.deepEqual(logs, [])
	})

	it('edits the open pull request and says so', () => {
		const { exec, calls } = fakeExec({ [view]: { status: 0, stdout: '7' } })
		const logs = []
		main({ env: { ...env, USING_DEFAULT_TOKEN: 'false' }, exec, log: (m) => logs.push(m) })
		assert.equal(calls.at(-1), `gh pr edit chore/relock --body ${BODY}`)
		assert.deepEqual(logs, ['Updated the open relock pull request.'])
	})

	it('warns first when the built-in token is in use', () => {
		const { exec } = fakeExec({ [view]: { status: 1 } })
		const logs = []
		main({ env: { ...env, USING_DEFAULT_TOKEN: 'true' }, exec, log: (m) => logs.push(m) })
		assert.equal(logs[0], DEFAULT_TOKEN_WARNING)
	})

	it('never puts the token in a failure message', () => {
		const { exec } = fakeExec({ 'git remote set-url origin https://x-access-token:tok@github.com/o/r.git': { status: 1 } })
		assert.throws(
			() => main({ env, exec, log() {} }),
			(error) => !error.message.includes('tok'),
		)
	})

	it('stops when a git step fails', () => {
		const { exec, calls } = fakeExec({ 'git commit -m Relock Terraform providers': { status: 1 } })
		assert.throws(() => main({ env, exec, log() {} }))
		assert.equal(calls.some((c) => c.startsWith('git push')), false)
	})
})
