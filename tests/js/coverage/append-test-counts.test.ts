import { describe, it } from 'node:test'
import assert from 'node:assert/strict'
import { mkdtempSync, readFileSync, rmSync, writeFileSync } from 'node:fs'
import { tmpdir } from 'node:os'
import { join } from 'node:path'

import type { Exec } from '../../../tools/lib/actions.ts'
import { main } from '../../../tools/coverage/append-test-counts.ts'

const run = (fragmentText?: string) => {
	const dir = mkdtempSync(join(tmpdir(), 'test-counts-'))
	const comment = join(dir, 'comment.md')
	const fragment = join(dir, 'e2e.md')
	writeFileSync(comment, 'gate\n')
	if (fragmentText !== undefined) writeFileSync(fragment, fragmentText)
	const calls: string[][] = []
	const exec: Exec = (command, args = []) => {
		calls.push([command, ...args])
		return { status: 0, stdout: '<details>x</details>', stderr: '' }
	}
	main({ exec, comment, fragment })
	const text = readFileSync(comment, 'utf8')
	rmSync(dir, { recursive: true })
	return { text, calls }
}

describe('append-test-counts', () => {
	it('appends the C# tables then the Playwright fragment to comment.md', () => {
		const { text, calls } = run('<details>e2e</details>\n')
		assert.equal(text, 'gate\n\n## Test counts\n\n<details>x</details>\n\n<details>e2e</details>\n')
		assert.deepEqual(calls, [['node', 'tools/coverage/report-test-counts.ts', '--section', 'csharp', '--trx-dir', './artifacts/coverage']])
	})

	it('says the Playwright suite did not run when there is no fragment', () => {
		assert.match(run().text, /<\/details>\n\n_Playwright suite not run in this job\._\n$/)
	})
})
