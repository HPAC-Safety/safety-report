import { describe, it } from 'node:test'
import assert from 'node:assert/strict'
import { mkdtempSync, readFileSync, rmSync, writeFileSync } from 'node:fs'
import { tmpdir } from 'node:os'
import { join } from 'node:path'

import { gateArgs, main, MARKER } from '../../../tools/coverage/write-coverage-comment.mjs'

describe('write-coverage-comment', () => {
	it('passes the report, the baseline from the environment, and the extra options to the gate', () => {
		assert.deepEqual(gateArgs('c.mjs', { BASELINE_PATH: 'b.xml', BASELINE_RUN_ID: '7' }, ['--min-line', '80']), [
			'c.mjs', '--report', './artifacts/report/Cobertura.xml', '--baseline', 'b.xml', '--baseline-run-id', '7', '--min-line', '80',
		])
		assert.deepEqual(gateArgs('c.mjs', {}, []).slice(3), ['--baseline', 'none', '--baseline-run-id', 'none'])
	})

	const run = (script, env = {}) => {
		const dir = mkdtempSync(join(tmpdir(), 'coverage-comment-'))
		const checker = join(dir, 'checker.mjs')
		writeFileSync(checker, script)
		const comment = join(dir, 'comment.md')
		const out = []
		const status = main({ argv: ['--min-line', '80'], env, write: (t) => out.push(t), comment, checker })
		const file = readFileSync(comment, 'utf8')
		rmSync(dir, { recursive: true })
		return { status, file, out: out.join('') }
	}

	it('writes the marker and the gate output, in order, to comment.md and the log', () => {
		const { status, file, out } = run('console.log("table"); console.error("note"); console.log("passed")')
		assert.equal(status, 0)
		assert.equal(file, `${MARKER}\ntable\nnote\npassed\n`)
		assert.equal(out, file)
	})

	it('returns the gate\'s exit code when it fails, keeping what it printed', () => {
		const { status, file } = run('console.error("::error::too low"); process.exit(1)')
		assert.equal(status, 1)
		assert.match(file, /::error::too low/)
	})

	it('hands the gate its arguments', () => {
		const { file } = run('console.log(process.argv.slice(2).join(" "))', { BASELINE_PATH: 'b.xml', BASELINE_RUN_ID: '7' })
		assert.match(file, /--baseline b\.xml --baseline-run-id 7 --min-line 80/)
	})
})
