import { describe, it } from 'node:test'
import assert from 'node:assert/strict'
import { mkdirSync, mkdtempSync, rmSync, writeFileSync } from 'node:fs'
import { tmpdir } from 'node:os'
import { join } from 'node:path'

import type { ExecOptions } from '../../../tools/lib/actions.ts'
import { findSuites, LCOV, main, testArgs } from '../../../tools/coverage/run-js-tests.ts'

const tree = (files: string[]): string => {
	const root = mkdtempSync(join(tmpdir(), 'run-js-tests-'))
	for (const file of files) {
		mkdirSync(join(root, file, '..'), { recursive: true })
		writeFileSync(join(root, file), '')
	}
	return root
}

describe('run-js-tests', () => {
	it('finds only .test.ts files, at any depth, sorted, and nothing else', () => {
		const root = tree(['b/x.test.ts', 'a/deep/y.test.ts', 'a/helper.ts', 'z.test.js', 'w.test.mjs'])
		try {
			assert.deepEqual(findSuites(root).map((f) => f.slice(root.length + 1)), ['a/deep/y.test.ts', 'b/x.test.ts'])
		} finally {
			rmSync(root, { recursive: true })
		}
	})

	it('finds nothing in a directory that does not exist', () => {
		assert.deepEqual(findSuites('/no/such/dir'), [])
	})

	it('builds the node --test arguments with the lcov reporter writing the coverage file', () => {
		assert.deepEqual(testArgs(['a.test.ts'], 'out.info'), [
			'--test',
			'--experimental-test-coverage',
			'--test-reporter=spec',
			'--test-reporter-destination=stdout',
			'--test-reporter=lcov',
			'--test-reporter-destination=out.info',
			'a.test.ts',
		])
		assert.equal(LCOV, './artifacts/coverage/js/lcov.info')
	})

	it('runs the suites and returns node\'s status', () => {
		const root = tree(['a.test.ts'])
		const calls: [string, readonly string[] | undefined, ExecOptions | undefined][] = []
		const dirs: [string, unknown][] = []
		try {
			const status = main({
				root,
				exec: (command, args, options) => {
					calls.push([command, args, options])
					return { status: 3, stdout: '', stderr: '' }
				},
				mkdir: (dir, options) => dirs.push([dir, options]),
				log: () => undefined,
			})
			assert.equal(status, 3)
			assert.equal(calls[0][0], 'node')
			assert.equal(calls[0][1]?.at(-1), `${root}/a.test.ts`)
			assert.deepEqual(dirs, [['./artifacts/coverage/js', { recursive: true }]])
		} finally {
			rmSync(root, { recursive: true })
		}
	})

	it('skips with a notice when there is no suite', () => {
		const logs: string[] = []
		const status = main({ root: '/no/such/dir', exec: () => assert.fail('ran'), log: (m) => logs.push(m) })
		assert.equal(status, 0)
		assert.deepEqual(logs, ['::notice::No JavaScript test suite yet — added by #8. Skipping.'])
	})
})
