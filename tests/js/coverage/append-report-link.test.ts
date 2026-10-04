import { describe, it } from 'node:test'
import assert from 'node:assert/strict'
import { mkdtempSync, readFileSync, rmSync, writeFileSync } from 'node:fs'
import { tmpdir } from 'node:os'
import { join } from 'node:path'

import { main, NO_REPORT } from '../../../tools/coverage/append-report-link.ts'

const run = (reportUrl?: string) => {
	const dir = mkdtempSync(join(tmpdir(), 'report-link-'))
	const comment = join(dir, 'comment.md')
	writeFileSync(comment, 'gate\n')
	main({ env: reportUrl === undefined ? {} : { REPORT_URL: reportUrl }, comment })
	const text = readFileSync(comment, 'utf8')
	rmSync(dir, { recursive: true })
	return text
}

describe('append-report-link', () => {
	it('appends a link to the uploaded coverage report', () => {
		const text = run('https://github.com/o/r/actions/runs/1/artifacts/2')
		assert.match(text, /^gate\n\n## Coverage report\n\n\[Download the coverage report\]\(https:\/\/github\.com\/o\/r\/actions\/runs\/1\/artifacts\/2\) — /)
		assert.match(text, /`index\.html`/)
	})

	it('says no report was uploaded when there is no URL', () => {
		assert.equal(run(), `gate\n\n## Coverage report\n\n${NO_REPORT}`)
		assert.equal(run('  '), `gate\n\n## Coverage report\n\n${NO_REPORT}`)
	})
})
