import { describe, it } from 'node:test'
import assert from 'node:assert/strict'
import { mkdtempSync, readFileSync, rmSync, writeFileSync } from 'node:fs'
import { tmpdir } from 'node:os'
import { join } from 'node:path'

import { main } from '../../../tools/coverage/read-local-baseline.ts'

const setup = (files: Record<string, string>, jobToken = '') => {
	const dir = mkdtempSync(join(tmpdir(), 'read-baseline-'))
	for (const [name, text] of Object.entries(files)) writeFileSync(join(dir, name), text)
	const output = join(dir, 'GITHUB_OUTPUT')
	writeFileSync(output, '')
	const logs: string[] = []
	main({ env: { GITHUB_OUTPUT: output, JOB_TOKEN: jobToken }, log: (m) => logs.push(m), dir })
	const read = readFileSync(output, 'utf8')
	rmSync(dir, { recursive: true })
	return { logs, read }
}

describe('read-local-baseline', () => {
	it('reads the baseline the wrapper fetched and the run it came from', () => {
		const { logs, read } = setup({ 'Cobertura.xml': '<x/>', 'run-id': '99\n' })
		assert.deepEqual(logs, ['ci-local:github-token=empty', 'Baseline from main run 99.'])
		assert.equal(read, 'path=./.ci-local/baseline/Cobertura.xml\nrun-id=99\n')
	})

	it('reports a job that holds a token', () => {
		assert.equal(setup({}, 'ghs_x').logs[0], 'ci-local:github-token=present')
	})

	it('skips the ratchet with the wrapper\'s notice when there is no baseline', () => {
		const { logs, read } = setup({ notice: 'main has no green run\n' })
		assert.equal(logs[1], '::notice::main has no green run')
		assert.equal(read, 'path=none\nrun-id=none\n')
	})

	it('falls back to a default notice when the wrapper left none', () => {
		const { logs } = setup({})
		assert.equal(logs[1], '::notice::No baseline from tools/dev/ci-local.sh. Ratchet skipped; the floor still applies.')
	})
})
