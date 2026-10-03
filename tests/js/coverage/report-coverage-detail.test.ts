import { describe, it } from 'node:test'
import assert from 'node:assert/strict'
import { mkdtempSync, readFileSync, rmSync, writeFileSync } from 'node:fs'
import { tmpdir } from 'node:os'
import { join } from 'node:path'

import { detailBlock, main } from '../../../tools/coverage/report-coverage-detail.ts'

describe('report-coverage-detail', () => {
	it('wraps the markdown in a details block with blank lines around it', () => {
		assert.equal(detailBlock('| a |\n'), '\n<details><summary>Per-assembly detail</summary>\n\n| a |\n\n</details>\n')
	})

	it('appends the block to the job summary', () => {
		const dir = mkdtempSync(join(tmpdir(), 'coverage-detail-'))
		const summary = join(dir, 'SummaryGithub.md')
		const out = join(dir, 'summary')
		writeFileSync(summary, '| a |\n')
		writeFileSync(out, 'before\n')
		assert.equal(main({ env: { GITHUB_STEP_SUMMARY: out }, summary }), 0)
		assert.equal(readFileSync(out, 'utf8'), `before\n${detailBlock('| a |\n')}`)
		rmSync(dir, { recursive: true })
	})

	it('writes nothing when there is no summary', () => {
		const dir = mkdtempSync(join(tmpdir(), 'coverage-detail-'))
		const out = join(dir, 'summary')
		writeFileSync(out, '')
		main({ env: { GITHUB_STEP_SUMMARY: out }, summary: join(dir, 'missing.md') })
		assert.equal(readFileSync(out, 'utf8'), '')
		rmSync(dir, { recursive: true })
	})
})
