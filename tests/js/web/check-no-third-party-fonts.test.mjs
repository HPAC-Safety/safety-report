import { describe, it } from 'node:test'
import assert from 'node:assert/strict'
import { mkdirSync, mkdtempSync, rmSync, writeFileSync } from 'node:fs'
import { tmpdir } from 'node:os'
import { join } from 'node:path'
import { spawnSync } from 'node:child_process'
import { fileURLToPath } from 'node:url'

import { main } from '../../../tools/web/check-no-third-party-fonts.mjs'

describe('check-no-third-party-fonts', () => {
	it('searches the dist directory for both Google Fonts hosts', () => {
		const calls = []
		main({ exec: (command, args) => (calls.push([command, ...args]), { stdout: '' }), log: () => {} })
		assert.deepEqual(calls, [['grep', '-rIl', '-e', 'fonts.googleapis.com', '-e', 'fonts.gstatic.com', 'src/web/dist']])
	})

	it('passes when nothing matches', () => {
		assert.equal(main({ exec: () => ({ status: 1, stdout: '' }), log: () => assert.fail('logged') }), 0)
	})

	it('fails naming the files that match', () => {
		const logs = []
		assert.equal(main({ exec: () => ({ status: 0, stdout: 'dist/a.css\ndist/b.css' }), log: (m) => logs.push(m) }), 1)
		assert.deepEqual(logs, ['::error::Fonts must be self-hosted from src/web/assets/fonts. Found a Google Fonts reference in:', 'dist/a.css\ndist/b.css'])
	})

	it('finds a real reference with the real grep', () => {
		const dir = mkdtempSync(join(tmpdir(), 'fonts-'))
		mkdirSync(join(dir, 'assets'))
		writeFileSync(join(dir, 'assets/a.css'), '@import url(https://fonts.gstatic.com/x)')
		writeFileSync(join(dir, 'assets/b.css'), 'body{}')
		const logs = []
		assert.equal(main({ argv: [dir], log: (m) => logs.push(m) }), 1)
		assert.equal(logs[1], join(dir, 'assets/a.css'))
		rmSync(dir, { recursive: true })
	})
})

describe('check-no-third-party-fonts as a command', () => {
	it('passes for a dist with no Google Fonts reference', () => {
		const dir = mkdtempSync(join(tmpdir(), 'fonts-cli-'))
		writeFileSync(join(dir, 'a.css'), 'body{}')
		const script = fileURLToPath(new URL('../../../tools/web/check-no-third-party-fonts.mjs', import.meta.url))
		const result = spawnSync(process.execPath, [script, dir], { encoding: 'utf8' })
		rmSync(dir, { recursive: true, force: true })
		assert.equal(result.status, 0)
	})
})
