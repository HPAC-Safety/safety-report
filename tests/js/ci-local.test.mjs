import { describe, it, before, after } from 'node:test'
import assert from 'node:assert/strict'
import { spawnSync } from 'node:child_process'
import { chmodSync, mkdtempSync, readFileSync, rmSync, writeFileSync } from 'node:fs'
import { tmpdir } from 'node:os'
import { dirname, join } from 'node:path'
import { fileURLToPath } from 'node:url'

const REPO = join(dirname(fileURLToPath(import.meta.url)), '..', '..')
const SCRIPT = join(REPO, 'tools/ci-local.sh')
const text = readFileSync(SCRIPT, 'utf8')
const code = text
	.split('\n')
	.filter((line) => !/^\s*#/.test(line))
	.join('\n')

// act receives no token (ADR-0145, #554). act fills a missing GITHUB_TOKEN
// secret from `gh auth token`, so leaving the flag out is not enough: the
// secret has to be given, and given empty.
describe('tools/ci-local.sh gives act no token', () => {
	const act = code.match(/\bif act pull_request[\s\S]*?; then/)?.[0] ?? ''

	it('invokes act in one place this test can read', () => {
		assert.ok(act.length > 0)
	})

	it('passes GITHUB_TOKEN explicitly empty', () => {
		assert.match(act, /-s GITHUB_TOKEN= /)
	})

	it('passes no other secret', () => {
		const secrets = act.match(/(?:^|\s)(?:-s|--secret)\s+\S+/g) ?? []
		assert.deepEqual(secrets.map((s) => s.trim()), ['-s GITHUB_TOKEN='])
		assert.match(act, /--secret-file \/dev\/null/)
	})

	it('unsets the token variables before starting act', () => {
		assert.match(code, /unset GITHUB_TOKEN GH_TOKEN/)
	})

	it('no longer reads HPAC_ACT_TOKEN or offers --allow-gh-token', () => {
		assert.doesNotMatch(text, /HPAC_ACT_TOKEN|allow-gh-token|ALLOW_GH_TOKEN/)
	})
})

describe('the coverage job under act', () => {
	const ci = readFileSync(join(REPO, '.github/workflows/ci.yml'), 'utf8')

	it('skips the token-using baseline download', () => {
		assert.match(ci, /- name: Fetch the main baseline\n\s+id: baseline\n\s+if: .*&& !env\.ACT\n/)
	})

	it('reads the baseline the wrapper fetched, and reports whether it holds a token', () => {
		assert.match(ci, /id: baseline-local\n\s+if: .*&& env\.ACT\n/)
		assert.match(ci, /ci-local:github-token=/)
		assert.match(ci, /steps\.baseline\.outputs\.path \|\| steps\.baseline-local\.outputs\.path \|\| 'none'/)
	})
})

// Stubbed gh and act on PATH: no real login, token, or act run is involved.
describe('tools/ci-local.sh without a gh login', () => {
	let dir
	let body

	before(() => {
		dir = mkdtempSync(join(tmpdir(), 'ci-local-test-'))
		body = join(dir, 'body.md')
		writeFileSync(body, 'Closes #0\n')
		// gh is installed but logged out; act reports a version no pin names, so
		// a run that gets past the login check stops there, before Docker.
		writeFileSync(join(dir, 'gh'), '#!/bin/sh\n[ "$1 $2" = "auth status" ] && exit 1\nexit 1\n')
		writeFileSync(join(dir, 'act'), '#!/bin/sh\necho "act version 0.0.0-stub"\n')
		chmodSync(join(dir, 'gh'), 0o755)
		chmodSync(join(dir, 'act'), 0o755)
	})

	after(() => rmSync(dir, { recursive: true, force: true }))

	const run = (...args) =>
		spawnSync('sh', [SCRIPT, '--body', body, ...args], {
			cwd: REPO,
			encoding: 'utf8',
			env: { ...process.env, PATH: `${dir}:${process.env.PATH}`, GH_TOKEN: '', GITHUB_TOKEN: '' },
		})

	it('stops before act, naming gh auth login, when the run includes coverage', () => {
		const result = run()
		assert.equal(result.status, 2)
		assert.match(result.stderr, /coverage ratchet needs main's baseline artifact.*run gh auth login/)
	})

	it('stops the same way for --job coverage', () => {
		const result = run('--job', 'coverage')
		assert.equal(result.status, 2)
		assert.match(result.stderr, /run gh auth login/)
	})

	it('needs no login for a job other than coverage', () => {
		const result = run('--job', 'linked-issue')
		assert.equal(result.status, 2)
		assert.doesNotMatch(result.stderr, /gh auth login/)
		assert.match(result.stderr, /act 0\.0\.0-stub is installed/)
	})

	it('refuses the removed --allow-gh-token flag', () => {
		const result = run('--allow-gh-token')
		assert.equal(result.status, 2)
		assert.match(result.stderr, /unknown option: --allow-gh-token/)
	})
})
