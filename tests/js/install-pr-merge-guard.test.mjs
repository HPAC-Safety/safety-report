import { describe, it } from 'node:test'
import assert from 'node:assert/strict'
import { mkdtempSync, readFileSync, rmSync, writeFileSync } from 'node:fs'
import { tmpdir } from 'node:os'
import { join } from 'node:path'

import { install, withGuardHook } from '../../tools/install-pr-merge-guard.mjs'

/** A fresh scratch directory, removed after the callback runs. */
function withTempDir(fn) {
	const dir = mkdtempSync(join(tmpdir(), 'guard-settings-'))
	try {
		return fn(dir)
	} finally {
		rmSync(dir, { recursive: true, force: true })
	}
}

describe('withGuardHook', () => {
	it('adds a PreToolUse/Bash hook to an empty settings object', () => {
		const result = withGuardHook({})

		assert.equal(result.hooks.PreToolUse.length, 1)
		assert.equal(result.hooks.PreToolUse[0].matcher, 'Bash')
		assert.match(result.hooks.PreToolUse[0].hooks[0].command, /guard-pr-merge\.mjs/)
	})

	it('appends to an existing Bash matcher instead of replacing it', () => {
		const existing = { hooks: { PreToolUse: [{ matcher: 'Bash', hooks: [{ type: 'command', command: 'echo other-hook' }] }] } }

		const result = withGuardHook(existing)

		assert.equal(result.hooks.PreToolUse.length, 1)
		assert.equal(result.hooks.PreToolUse[0].hooks.length, 2)
		assert.equal(result.hooks.PreToolUse[0].hooks[0].command, 'echo other-hook')
	})

	it('leaves an unrelated matcher and graphify-owned keys untouched', () => {
		const existing = {
			$schema: 'https://example.com/schema.json',
			hooks: { PreToolUse: [{ matcher: 'Edit', hooks: [{ type: 'command', command: 'graphify-thing' }] }] },
		}

		const result = withGuardHook(existing)

		assert.equal(result.$schema, 'https://example.com/schema.json')
		assert.equal(result.hooks.PreToolUse.length, 2)
		assert.ok(result.hooks.PreToolUse.some((entry) => entry.matcher === 'Edit'))
	})

	it('is idempotent: running twice changes nothing further', () => {
		const once = withGuardHook({})
		const twice = withGuardHook(once)

		assert.deepEqual(once, twice)
	})
})

describe('install', () => {
	it('creates .claude/settings.json when none exists', () => {
		withTempDir((dir) => {
			const path = join(dir, '.claude', 'settings.json')

			const changed = install(path)

			assert.equal(changed, true)
			const written = JSON.parse(readFileSync(path, 'utf8'))
			assert.match(written.hooks.PreToolUse[0].hooks[0].command, /guard-pr-merge\.mjs/)
		})
	})

	it('merges into an existing file without disturbing other settings', () => {
		withTempDir((dir) => {
			const path = join(dir, 'settings.json')
			writeFileSync(path, JSON.stringify({ graphifyBinary: '/abs/path/to/graphify' }))

			install(path)

			const written = JSON.parse(readFileSync(path, 'utf8'))
			assert.equal(written.graphifyBinary, '/abs/path/to/graphify')
			assert.match(written.hooks.PreToolUse[0].hooks[0].command, /guard-pr-merge\.mjs/)
		})
	})

	it('does nothing on a second run', () => {
		withTempDir((dir) => {
			const path = join(dir, 'settings.json')

			install(path)
			const first = readFileSync(path, 'utf8')
			const changed = install(path)
			const second = readFileSync(path, 'utf8')

			assert.equal(changed, false)
			assert.equal(first, second)
		})
	})

	it('throws a clear error on malformed JSON rather than clobbering it', () => {
		withTempDir((dir) => {
			const path = join(dir, 'settings.json')
			writeFileSync(path, '{ not json')

			assert.throws(() => install(path), /not valid JSON/)
		})
	})
})
