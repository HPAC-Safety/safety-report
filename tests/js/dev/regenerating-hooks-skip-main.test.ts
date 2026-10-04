import { describe, it, before, after } from 'node:test'
import assert from 'node:assert/strict'
import { spawnSync } from 'node:child_process'
import { chmodSync, existsSync, mkdirSync, mkdtempSync, readFileSync, rmSync, writeFileSync } from 'node:fs'
import { tmpdir } from 'node:os'
import { dirname, join } from 'node:path'
import { fileURLToPath } from 'node:url'

const REPO = join(dirname(fileURLToPath(import.meta.url)), '..', '..', '..')

// A throwaway repository, isolated from the machine's git configuration.
const env = {
	...process.env,
	GIT_CONFIG_GLOBAL: '/dev/null',
	GIT_CONFIG_SYSTEM: '/dev/null',
	GIT_AUTHOR_NAME: 'Test',
	GIT_AUTHOR_EMAIL: 'test@example.test',
	GIT_COMMITTER_NAME: 'Test',
	GIT_COMMITTER_EMAIL: 'test@example.test',
}

function git(cwd: string, ...args: string[]) {
	const result = spawnSync('git', args, { cwd, env, encoding: 'utf8' })
	return { status: result.status, out: result.stdout.trim() }
}

// The hooks reach the generators through `node`. A stand-in `node` first on the
// PATH records that it was called, so a test can tell whether the hook got past
// its guard without running the real generators.
function runHook(root: string, name: string, args: string[], stdin = '') {
	const bin = join(root, '.fake-bin')
	mkdirSync(bin, { recursive: true })
	writeFileSync(join(bin, 'node'), `#!/usr/bin/env sh\necho "$@" >> "${join(root, 'node-called.txt')}"\n`)
	chmodSync(join(bin, 'node'), 0o755)
	// The hooks also install agent tooling; a stand-in `skillfile` keeps the real one out of the throwaway repository.
	writeFileSync(join(bin, 'skillfile'), `#!/usr/bin/env sh\necho "$@" >> "${join(root, 'skillfile-called.txt')}"\n`)
	chmodSync(join(bin, 'skillfile'), 0o755)
	return spawnSync('sh', [join(REPO, '.githooks', name), ...args], {
		cwd: root,
		env: { ...env, PATH: `${bin}:${process.env.PATH ?? ''}` },
		input: stdin,
		encoding: 'utf8',
	})
}

describe('the hooks that regenerate the specification', () => {
	let root = ''

	before(() => {
		root = mkdtempSync(join(tmpdir(), 'regen-hooks-'))
		git(root, 'init', '-q', '-b', 'main')
		writeFileSync(join(root, 'file.txt'), 'one\n')
		git(root, 'add', '.')
		git(root, '-c', 'core.hooksPath=/dev/null', 'commit', '-q', '-m', 'first')
	})

	after(() => rmSync(root, { recursive: true, force: true }))

	for (const [name, args] of [['post-merge', ['0']], ['post-rewrite', ['rebase']]] as const) {
		it(`${name} changes nothing on main, the branch that only fast-forwards to origin`, () => {
			git(root, 'checkout', '-q', 'main')
			rmSync(join(root, 'node-called.txt'), { force: true })

			const result = runHook(root, name, [...args])

			assert.equal(result.status, 0)
			// Only the graph merge, which writes to the untracked graphify-out/ (ADR-0193).
			assert.deepEqual(readFileSync(join(root, 'node-called.txt'), 'utf8').trim().split('\n'), ['tools/spec/graph-fragment.ts'], 'no generator ran')
			assert.equal(git(root, 'diff', '--cached', '--name-only').out, '', 'nothing is staged')
		})

		it(`${name} installs agent tooling on main too, and leaves no tracked change`, () => {
			git(root, 'checkout', '-q', 'main')
			rmSync(join(root, 'skillfile-called.txt'), { force: true })

			runHook(root, name, [...args])

			assert.equal(readFileSync(join(root, 'skillfile-called.txt'), 'utf8').split('\n')[0], 'install', 'skillfile install ran')
			assert.equal(git(root, 'diff', '--name-only').out, '', 'no tracked file changed')
		})

		it(`${name} still regenerates on a working branch`, () => {
			git(root, 'checkout', '-q', '-B', 'issue-1/work')
			rmSync(join(root, 'node-called.txt'), { force: true })

			const result = runHook(root, name, [...args])

			assert.equal(result.status, 0)
			assert.ok(existsSync(join(root, 'node-called.txt')))
			assert.deepEqual(readFileSync(join(root, 'node-called.txt'), 'utf8').trim().split('\n'), [
				'tools/spec/generate-traceability.ts --no-fail',
				'tools/spec/generate-spec-index.ts',
				'tools/spec/graph-fragment.ts',
			])
		})
	}
})
