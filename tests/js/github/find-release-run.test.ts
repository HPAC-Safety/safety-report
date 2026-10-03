import { describe, it } from 'node:test'
import assert from 'node:assert/strict'
import { mkdtempSync, readFileSync } from 'node:fs'
import { tmpdir } from 'node:os'
import path from 'node:path'

import type { Exec } from '../../../tools/lib/actions.ts'
import { main, missingArtifact, TAG_PATTERN } from '../../../tools/github/find-release-run.ts'

describe('promote tag shape', () => {
	it('accepts YYYY.MM.DD-N and nothing looser', () => {
		assert.ok(TAG_PATTERN.test('2026.10.02-1'))
		assert.ok(TAG_PATTERN.test('2026.12.31-12'))
		for (const bad of ['2026.13.02-1', '2026.10.32-1', '2026.10.02-0', '2026.10.02', 'v2026.10.02-1', '2026.10.02-1x']) {
			assert.equal(TAG_PATTERN.test(bad), false, bad)
		}
	})

	it('names the first required artifact that is missing', () => {
		assert.equal(missingArtifact(['api-image', 'worker-image', 'web-dist']), null)
		assert.equal(missingArtifact(['api-image', 'web-dist']), 'worker-image')
		assert.equal(missingArtifact(['']), 'api-image')
	})
})

describe('find release run', () => {
	function setup(answers: string[]) {
		const dir = mkdtempSync(path.join(tmpdir(), 'frr-'))
		const env = {
			REPO: 'o/r',
			REF_TYPE: 'tag',
			TAG: '2026.10.02-1',
			GITHUB_SERVER_URL: 'https://github.com',
			GITHUB_OUTPUT: path.join(dir, 'out'),
			GITHUB_STEP_SUMMARY: path.join(dir, 'summary'),
		}
		const calls: (readonly string[])[] = []
		const exec: Exec = (command, args = []) => {
			calls.push([command, ...args])
			return { status: 0, stdout: answers[calls.length - 1] ?? '', stderr: '' }
		}
		const out: string[] = []
		return { env, exec, calls, out, log: (line: string) => out.push(line) }
	}

	it('refuses a branch', () => {
		const t = setup([])
		assert.equal(main({ ...t, env: { ...t.env, REF_TYPE: 'branch', TAG: 'main' } }), 1)
		assert.match(t.out[0], /^::error::Run this on a release tag, not the 'main' branch/)
		assert.equal(t.calls.length, 0)
	})

	it('refuses a tag that is not a release tag', () => {
		const t = setup([])
		assert.equal(main({ ...t, env: { ...t.env, TAG: 'v1' } }), 1)
		assert.equal(t.out[0], "::error::Tag 'v1' does not match YYYY.MM.DD-N (e.g. 2026.10.02-1). See ADR-0158.")
	})

	it('refuses a tag with no successful release run', () => {
		const t = setup(['deadbeef', ''])
		assert.equal(main(t), 1)
		assert.match(t.out[0], /No successful Release run for 2026\.10\.02-1 \(commit deadbeef\)/)
	})

	it('refuses a run whose artifact expired', () => {
		const t = setup(['deadbeef', '99', 'api-image\nweb-dist'])
		assert.equal(main(t), 1)
		assert.match(t.out[0], /Release run 99 for 2026\.10\.02-1 no longer has its 'worker-image' artifact/)
	})

	it('outputs the tag, commit, and run for a green release', () => {
		const t = setup(['deadbeef', '99', 'api-image\nworker-image\nweb-dist'])
		assert.equal(main(t), 0)
		assert.deepEqual(t.calls[0], ['gh', 'api', 'repos/o/r/commits/2026.10.02-1', '--jq', '.sha'])
		assert.equal(t.calls[1][2], 'repos/o/r/actions/workflows/release.yml/runs?head_sha=deadbeef&status=success&per_page=1')
		assert.deepEqual(t.calls[2].slice(0, 3), ['gh', 'api', '--paginate'])
		assert.equal(readFileSync(t.env.GITHUB_OUTPUT, 'utf8'), 'tag=2026.10.02-1\nsha=deadbeef\nrun_id=99\n')
		assert.equal(
			readFileSync(t.env.GITHUB_STEP_SUMMARY, 'utf8'),
			'### Promoting 2026.10.02-1\n- Release run: https://github.com/o/r/actions/runs/99\n- Commit: `deadbeef`\n',
		)
	})
	it('writes a summary link without a host when the server URL is not set', () => {
		const t = setup(['deadbeef', '99', 'api-image\nworker-image\nweb-dist'])
		const { GITHUB_SERVER_URL: _unset, ...env } = t.env
		assert.equal(main({ ...t, env }), 0)
		assert.match(readFileSync(t.env.GITHUB_STEP_SUMMARY, 'utf8'), /- Release run: \/o\/r\/actions\/runs\/99\n/)
	})
})
