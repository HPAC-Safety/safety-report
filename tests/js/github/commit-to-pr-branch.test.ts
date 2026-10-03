import { describe, it } from 'node:test'
import assert from 'node:assert/strict'
import { readFileSync } from 'node:fs'
import { dirname, join } from 'node:path'
import { fileURLToPath } from 'node:url'

import type { Exec } from '../../../tools/lib/actions.ts'
import { main, warning } from '../../../tools/github/commit-to-pr-branch.ts'

const REPO = join(dirname(fileURLToPath(import.meta.url)), '..', '..', '..')

const ENV = { GH_TOKEN: 'sekret', GITHUB_REPOSITORY: 'o/r', HEAD_REF: 'feature', HEAD_SHA: 'abc', SUMMARY: '3 keys' }
const ARGS = ['--adr', 'ADR-0021/ADR-0057', '--add', 'a.json,b.json', '--add-if-exists', 'p.pending', '--subject', 'Translate', '--body-env', 'SUMMARY', '--paths', 'x/**,y']

function harness({ failOn }: { failOn?: string } = {}) {
	const calls: (readonly string[])[] = []
	const logged: string[] = []
	const pushes: (readonly string[])[] = []
	const exec: Exec = (command, args = []) => {
		calls.push([command, ...args])
		return { status: failOn === args[0] ? 1 : 0, stdout: '', stderr: failOn === args[0] ? 'boom' : '' }
	}
	return { calls, logged, pushes, exec, log: (line: string) => logged.push(line), push: (argv: readonly string[]) => (pushes.push(argv), 0) }
}

describe('commit to pr branch', () => {
	it('configures, stages, commits with the summary, and pushes through push-to-pr-branch', () => {
		const h = harness()
		const code = main({ argv: ARGS, env: ENV, exec: h.exec, log: h.log, push: h.push, cwd: '/w', exists: (file) => file === '/w/p.pending' })
		assert.equal(code, 0)
		assert.deepEqual(h.calls, [
			['git', 'config', 'user.name', 'github-actions[bot]'],
			['git', 'config', 'user.email', '41898282+github-actions[bot]@users.noreply.github.com'],
			['git', 'remote', 'set-url', 'origin', 'https://x-access-token:sekret@github.com/o/r.git'],
			['git', 'add', 'a.json', 'b.json'],
			['git', 'add', 'p.pending'],
			['git', 'commit', '-m', 'Translate', '-m', '3 keys.'],
		])
		assert.deepEqual(h.pushes, [['--branch', 'feature', '--event-sha', 'abc', '--paths', 'x/**,y']])
		assert.deepEqual(h.logged, [])
	})

	it('skips an optional file that is absent and omits the body without --body-env', () => {
		const h = harness()
		const argv = ['--adr', 'ADR-0101', '--add', 'a', '--add-if-exists', 'p', '--subject', 'S', '--paths', 'x']
		assert.equal(main({ argv, env: ENV, exec: h.exec, log: h.log, push: h.push, exists: () => false }), 0)
		assert.deepEqual(h.calls.slice(3), [['git', 'add', 'a'], ['git', 'commit', '-m', 'S']])
	})

	it('warns when the built-in token is in use', () => {
		const h = harness()
		main({ argv: ARGS, env: { ...ENV, USING_DEFAULT_TOKEN: 'true' }, exec: h.exec, log: h.log, push: h.push, exists: () => false })
		assert.deepEqual(h.logged, [warning('ADR-0021/ADR-0057')])
		assert.match(h.logged[0], /^::warning::No TRANSLATION_PR_TOKEN is set.* See ADR-0021\/ADR-0057\.$/)
	})

	it('stops at a failed git command without printing the token, and never pushes', () => {
		const h = harness({ failOn: 'remote' })
		const code = main({ argv: ARGS, env: ENV, exec: h.exec, log: h.log, push: h.push })
		assert.equal(code, 1)
		assert.deepEqual(h.logged, ['::error::git remote failed: boom'])
		assert.equal(h.pushes.length, 0)
		assert.equal(h.calls.length, 3)
	})

	it('fails on missing arguments', () => {
		const h = harness()
		assert.equal(main({ argv: [], env: ENV, exec: h.exec, log: h.log, push: h.push }), 1)
	})

	it('pushes through the shared push tool', () => {
		assert.match(readFileSync(join(REPO, 'tools/github/commit-to-pr-branch.ts'), 'utf8'), /from '\.\/push-to-pr-branch\.ts'/)
	})
})
