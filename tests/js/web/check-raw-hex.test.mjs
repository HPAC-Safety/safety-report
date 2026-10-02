import { describe, it } from 'node:test'
import assert from 'node:assert/strict'
import { mkdtempSync, rmSync, writeFileSync } from 'node:fs'
import { tmpdir } from 'node:os'
import { join } from 'node:path'

import { INCLUDES, main, PATHS } from '../../../tools/web/check-raw-hex.mjs'

describe('check-raw-hex', () => {
	it('scans the markup paths for .tsx, .ts, and .html only', () => {
		const calls = []
		main({ exec: (command, args) => (calls.push([command, args]), { stdout: '' }), log: () => {} })
		assert.equal(calls[0][0], 'grep')
		assert.deepEqual(calls[0][1].slice(2, 2 + PATHS.length), PATHS)
		assert.deepEqual(calls[0][1].slice(-3), INCLUDES.map((glob) => `--include=${glob}`))
	})

	it('passes when nothing matches', () => {
		assert.equal(main({ exec: () => ({ status: 1, stdout: '' }), log: () => assert.fail('logged') }), 0)
	})

	it('fails with the annotation and the matches', () => {
		const logs = []
		assert.equal(main({ exec: () => ({ stdout: 'a.tsx:1:#fff' }), log: (m) => logs.push(m) }), 1)
		assert.deepEqual(logs, ['::error::Raw hex values belong in src/web/src/index.css. Use the @theme tokens in markup:', 'a.tsx:1:#fff'])
	})

	it('matches 3, 4, 6, and 8 digit literals in the real grep, and ignores other files and 5-digit runs', () => {
		const dir = mkdtempSync(join(tmpdir(), 'raw-hex-'))
		writeFileSync(join(dir, 'a.tsx'), 'x="bg-[#f22312ff]"\ny="#abc"\nz="#12345"\n')
		writeFileSync(join(dir, 'b.css'), 'a{color:#fff}')
		const logs = []
		assert.equal(main({ argv: [dir], log: (m) => logs.push(m) }), 1)
		assert.match(logs[1], /a\.tsx:1:.*#f22312ff/)
		assert.match(logs[1], /a\.tsx:2:/)
		assert.doesNotMatch(logs[1], /a\.tsx:3:|b\.css/)
		rmSync(dir, { recursive: true })
	})
})
