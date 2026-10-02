import { describe, it } from 'node:test'
import assert from 'node:assert/strict'
import { mkdtempSync, readFileSync } from 'node:fs'
import { tmpdir } from 'node:os'
import path from 'node:path'

import { dayOf, main, nextTag, provenance } from '../../../tools/github/create-release.mjs'

describe('release tag', () => {
	it('formats the UTC day with zero padding', () => {
		assert.equal(dayOf(new Date('2026-03-04T23:59:00Z')), '2026.03.04')
	})

	it('takes -1 when the day has no release', () => {
		assert.equal(nextTag('2026.10.02', []), '2026.10.02-1')
	})

	it('takes the next number after the highest, sorted numerically', () => {
		const refs = ['refs/tags/2026.10.02-2', 'refs/tags/2026.10.02-10', 'refs/tags/2026.10.02-9']
		assert.equal(nextTag('2026.10.02', refs), '2026.10.02-11')
	})

	it('ignores refs that are not a plain number for the day', () => {
		const refs = ['refs/tags/2026.10.02-rc1', 'refs/tags/2026.10.02-0', 'refs/tags/2026.10.02-3x', 'refs/tags/2026.10.021-5']
		assert.equal(nextTag('2026.10.02', refs), '2026.10.02-1')
	})

	it('writes a provenance table that names the promote path', () => {
		const text = provenance({ sha: 'abc', server: 'https://github.com', repo: 'o/r', runId: '7', tag: 'T' })
		assert.match(text, /\| \[`abc`\]\(https:\/\/github.com\/o\/r\/commit\/abc\) \| \[run 7\]\(https:\/\/github.com\/o\/r\/actions\/runs\/7\) \|/)
		assert.match(text, /Tags → `T`\./)
	})
})

describe('create release', () => {
	const base = { REPO: 'o/r', REF: 'refs/heads/main', SHA: 'abc', SERVER: 'https://github.com', RUN_ID: '7' }
	const fixed = () => new Date('2026-10-02T12:00:00Z')

	it('refuses a run that is not on main', () => {
		const calls = []
		const out = []
		const code = main({ env: { ...base, REF: 'refs/heads/x' }, exec: (...c) => calls.push(c), now: fixed, log: (l) => out.push(l) })
		assert.equal(code, 1)
		assert.equal(calls.length, 0)
		assert.equal(out[0], '::error::Release from main only (this run is on refs/heads/x). Run workflow → Use workflow from → main.')
	})

	it('creates the next tag and release, then writes the output and summary', () => {
		const dir = mkdtempSync(path.join(tmpdir(), 'cr-'))
		const outputFile = path.join(dir, 'out')
		const summaryFile = path.join(dir, 'summary')
		const calls = []
		let notes
		const exec = (command, args) => {
			calls.push([command, ...args])
			if (args[0] === 'api') return { status: 0, stdout: 'refs/tags/2026.10.02-1\nrefs/tags/2026.10.02-2', stderr: '' }
			notes = readFileSync(args[args.indexOf('--notes-file') + 1], 'utf8')
			return { status: 0, stdout: '', stderr: '' }
		}
		const env = { ...base, GITHUB_OUTPUT: outputFile, GITHUB_STEP_SUMMARY: summaryFile }
		assert.equal(main({ env, exec, now: fixed, log: () => {} }), 0)
		assert.deepEqual(calls[0], ['gh', 'api', 'repos/o/r/git/matching-refs/tags/2026.10.02-', '--jq', '.[].ref'])
		assert.deepEqual(calls[1].slice(0, 9), ['gh', 'release', 'create', '2026.10.02-3', '--repo', 'o/r', '--target', 'abc', '--title'])
		assert.ok(calls[1].includes('--generate-notes'))
		assert.match(notes, /Tags → `2026\.10\.02-3`/)
		assert.equal(readFileSync(outputFile, 'utf8'), 'tag=2026.10.02-3\n')
		assert.equal(readFileSync(summaryFile, 'utf8'), '### Release [2026.10.02-3](https://github.com/o/r/releases/tag/2026.10.02-3)\n')
	})
})
