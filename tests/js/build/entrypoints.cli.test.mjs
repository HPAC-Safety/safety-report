import { describe, it } from 'node:test'
import assert from 'node:assert/strict'
import { mkdtempSync, rmSync } from 'node:fs'
import { tmpdir } from 'node:os'
import { join } from 'node:path'
import { spawnSync } from 'node:child_process'
import { fileURLToPath } from 'node:url'

const script = (name) => fileURLToPath(new URL(`../../../tools/build/${name}.mjs`, import.meta.url))

describe('the build scripts run as commands', () => {
	const run = (name, args = []) => {
		const dir = mkdtempSync(join(tmpdir(), 'build-entry-'))
		const result = spawnSync(process.execPath, [script(name), ...args], { cwd: dir, encoding: 'utf8', env: { PATH: process.env.PATH } })
		rmSync(dir, { recursive: true, force: true })
		return result
	}

	it('check-clean-worktree requires a message', () => {
		const result = run('check-clean-worktree')
		assert.equal(result.status, 2)
		assert.match(result.stdout, /A message is required/)
	})

	it('smoke-test-worker-image requires an image tag', () => {
		const result = run('smoke-test-worker-image')
		assert.equal(result.status, 2)
		assert.match(result.stdout, /An image tag is required/)
	})
})
