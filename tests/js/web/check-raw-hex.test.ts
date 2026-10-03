import { describe, it } from 'node:test'
import assert from 'node:assert/strict'
import { mkdtempSync, rmSync, writeFileSync } from 'node:fs'
import { tmpdir } from 'node:os'
import { join } from 'node:path'
import { spawnSync } from 'node:child_process'
import { fileURLToPath } from 'node:url'

import type { ExecResult } from '../../../tools/lib/actions.ts'
import { INCLUDES, main, PATHS } from '../../../tools/web/check-raw-hex.ts'

/** An exec stand-in's answer: only the fields a case cares about differ. */
const result = (fields: Partial<ExecResult>): ExecResult => ({ status: 0, stdout: '', stderr: '', ...fields })

describe('check-raw-hex', () => {
	it('scans the markup paths for .tsx, .ts, and .html only', () => {
		const calls: (readonly [string, readonly string[]])[] = []
		main({
			exec: (command, args = []) => {
				calls.push([command, args])
				return result({})
			},
			log: () => {},
		})
		const [command, args] = calls[0] ?? ['', []]
		assert.equal(command, 'grep')
		assert.deepEqual(args.slice(2, 2 + PATHS.length), PATHS)
		assert.deepEqual(args.slice(-3), INCLUDES.map((glob) => `--include=${glob}`))
	})

	it('passes when nothing matches', () => {
		assert.equal(main({ exec: () => result({ status: 1 }), log: () => assert.fail('logged') }), 0)
	})

	it('fails with the annotation and the matches', () => {
		const logs: string[] = []
		assert.equal(main({ exec: () => result({ stdout: 'a.tsx:1:#fff' }), log: (m) => logs.push(m) }), 1)
		assert.deepEqual(logs, ['::error::Raw hex values belong in src/web/src/index.css. Use the @theme tokens in markup:', 'a.tsx:1:#fff'])
	})

	it('matches 3, 4, 6, and 8 digit literals in the real grep, and ignores other files and 5-digit runs', () => {
		const dir = mkdtempSync(join(tmpdir(), 'raw-hex-'))
		writeFileSync(join(dir, 'a.tsx'), 'x="bg-[#f22312ff]"\ny="#abc"\nz="#12345"\n')
		writeFileSync(join(dir, 'b.css'), 'a{color:#fff}')
		const logs: string[] = []
		assert.equal(main({ argv: [dir], log: (m) => logs.push(m) }), 1)
		assert.match(logs[1], /a\.tsx:1:.*#f22312ff/)
		assert.match(logs[1], /a\.tsx:2:/)
		assert.doesNotMatch(logs[1], /a\.tsx:3:|b\.css/)
		rmSync(dir, { recursive: true })
	})
})

describe('check-raw-hex as a command', () => {
	it('passes for a path with no raw hex', () => {
		const dir = mkdtempSync(join(tmpdir(), 'raw-hex-cli-'))
		writeFileSync(join(dir, 'a.tsx'), 'export const a = 1\n')
		const script = fileURLToPath(new URL('../../../tools/web/check-raw-hex.ts', import.meta.url))
		const result = spawnSync(process.execPath, [script, dir], { encoding: 'utf8' })
		rmSync(dir, { recursive: true, force: true })
		assert.equal(result.status, 0)
	})
})
