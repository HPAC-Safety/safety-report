import { describe, it } from 'node:test'
import assert from 'node:assert/strict'
import { mkdirSync, mkdtempSync, readFileSync, rmSync, writeFileSync } from 'node:fs'
import { tmpdir } from 'node:os'
import { join } from 'node:path'
import { spawnSync } from 'node:child_process'
import { fileURLToPath } from 'node:url'

const SCRIPT = fileURLToPath(new URL('../../../tools/coverage/report-test-counts.mjs', import.meta.url))

describe('report-test-counts --section e2e', () => {
	const fixture = () => {
		const dir = mkdtempSync(join(tmpdir(), 'test-counts-cli-'))
		mkdirSync(join(dir, 'gen/home'), { recursive: true })
		writeFileSync(join(dir, 'gen/home/home.spec.js'), "test('a', () => {})\ntest('b', () => {})\n")
		return dir
	}
	const run = (dir, ...extra) =>
		spawnSync('node', [SCRIPT, '--section', 'e2e', '--e2e-gen', join(dir, 'gen'), '--smoke-spec', join(dir, 'none.ts'), ...extra], { encoding: 'utf8' })

	it('prints the table by default', () => {
		const dir = fixture()
		const result = run(dir)
		rmSync(dir, { recursive: true })
		assert.equal(result.status, 0)
		assert.match(result.stdout, /Playwright \(E2E\) \(2 tests\)/)
		assert.match(result.stdout, /\| home\.feature \| 2 \|/)
	})

	it('writes the same text to --out, creating its directory', () => {
		const dir = fixture()
		const printed = run(dir).stdout
		const out = join(dir, 'nested/dir/e2e.md')
		const result = run(dir, '--out', out)
		const written = readFileSync(out, 'utf8')
		rmSync(dir, { recursive: true })
		assert.equal(result.stdout, '')
		assert.equal(written, printed)
	})
})
