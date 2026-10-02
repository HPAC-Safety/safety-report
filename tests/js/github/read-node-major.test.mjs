import { describe, it } from 'node:test'
import assert from 'node:assert/strict'
import { mkdtempSync, readFileSync, writeFileSync } from 'node:fs'
import { tmpdir } from 'node:os'
import path from 'node:path'

import { main, readNodeMajor } from '../../../tools/github/read-node-major.mjs'

describe('pinned node major', () => {
	it('reads the first node-version', () => {
		assert.equal(readNodeMajor('steps:\n  with:\n    node-version: 24\n    node-version: 22\n'), '24')
	})

	it('finds none when the file pins nothing', () => {
		assert.equal(readNodeMajor('node-version-file: x\n'), null)
	})

	it('outputs the major, or fails naming the file', () => {
		const dir = mkdtempSync(path.join(tmpdir(), 'rnm-'))
		const ci = path.join(dir, 'ci.yml')
		const out = path.join(dir, 'out')
		writeFileSync(ci, 'node-version: 24\n')
		assert.equal(main({ argv: [ci], env: { GITHUB_OUTPUT: out }, log: () => {} }), 0)
		assert.equal(readFileSync(out, 'utf8'), 'major=24\n')

		writeFileSync(ci, 'nothing\n')
		const logged = []
		assert.equal(main({ argv: [ci], env: { GITHUB_OUTPUT: out }, log: (l) => logged.push(l) }), 1)
		assert.equal(logged[0], `::error::Could not read the pinned Node major out of ${ci}.`)
	})

	it('fails without a file argument', () => {
		assert.equal(main({ argv: [], env: {}, log: () => {} }), 1)
	})
})
