import { describe, it, before, after } from 'node:test'
import assert from 'node:assert/strict'
import { spawnSync } from 'node:child_process'
import { chmodSync, copyFileSync, mkdirSync, mkdtempSync, readFileSync, rmSync, writeFileSync } from 'node:fs'
import { tmpdir } from 'node:os'
import { dirname, join } from 'node:path'
import { fileURLToPath } from 'node:url'

const REPO = join(dirname(fileURLToPath(import.meta.url)), '..', '..', '..')
const SHIM = join(REPO, 'tools/dev/git-hook-shim.sh')

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
	return { status: result.status, out: result.stdout.trim(), err: result.stderr.trim() }
}

function trackedHook(root: string, name: string, body: string, mode = 0o644) {
	mkdirSync(join(root, '.githooks'), { recursive: true })
	writeFileSync(join(root, '.githooks', name), `#!/usr/bin/env sh\n${body}\n`)
	chmodSync(join(root, '.githooks', name), mode)
}

function installShim(root: string, name: string) {
	const hooks = join(root, '.git', 'hooks')
	mkdirSync(hooks, { recursive: true })
	copyFileSync(SHIM, join(hooks, name))
	chmodSync(join(hooks, name), 0o755)
}

describe('tools/dev/git-hook-shim.sh', () => {
	let dir = ''
	let main = ''

	before(() => {
		dir = mkdtempSync(join(tmpdir(), 'hook-shim-'))
		main = join(dir, 'main')
		mkdirSync(main)
		git(main, 'init', '-q', '-b', 'main')
		writeFileSync(join(main, 'file.txt'), 'one\n')
		// The tracked hook is not executable (as post-merge and post-rewrite are not), so the shim must not need it to be.
		trackedHook(main, 'pre-commit', 'echo main > "$(git rev-parse --show-toplevel)/ran.txt"', 0o644)
		git(main, 'add', '.')
		git(main, '-c', 'core.hooksPath=/dev/null', 'commit', '-q', '-m', 'first')
		installShim(main, 'pre-commit')
	})

	after(() => rmSync(dir, { recursive: true, force: true }))

	it('runs the tracked hook when git commits', () => {
		writeFileSync(join(main, 'file.txt'), 'two\n')
		git(main, 'add', 'file.txt')

		const commit = git(main, 'commit', '-q', '-m', 'second')

		assert.equal(commit.status, 0, commit.err)
		assert.equal(readFileSync(join(main, 'ran.txt'), 'utf8').trim(), 'main')
	})

	it('runs the hook of the worktree git is working in, not the main checkout\'s', () => {
		const tree = join(dir, 'tree')
		git(main, 'worktree', 'add', '-q', '-b', 'feature', tree)
		trackedHook(tree, 'pre-commit', 'echo worktree > "$(git rev-parse --show-toplevel)/ran.txt"')
		writeFileSync(join(tree, 'file.txt'), 'three\n')
		git(tree, 'add', '.')

		const commit = git(tree, 'commit', '-q', '-m', 'third')

		assert.equal(commit.status, 0, commit.err)
		assert.equal(readFileSync(join(tree, 'ran.txt'), 'utf8').trim(), 'worktree')
		assert.equal(readFileSync(join(main, 'ran.txt'), 'utf8').trim(), 'main')
	})

	it('follows the hook when a tool it calls moves, because nothing about the hook was copied', () => {
		writeFileSync(join(main, 'moved.sh'), 'echo moved > "$1"\n')
		trackedHook(main, 'pre-commit', 'sh "$(git rev-parse --show-toplevel)/moved.sh" "$(git rev-parse --show-toplevel)/ran.txt"')
		writeFileSync(join(main, 'file.txt'), 'four\n')
		git(main, 'add', '.')

		const commit = git(main, 'commit', '-q', '-m', 'fourth')

		assert.equal(commit.status, 0, commit.err)
		assert.equal(readFileSync(join(main, 'ran.txt'), 'utf8').trim(), 'moved')
	})

	it('refuses the commit when the hook exits non-zero', () => {
		trackedHook(main, 'pre-commit', 'exit 3')
		writeFileSync(join(main, 'file.txt'), 'five\n')
		git(main, 'add', 'file.txt')

		const commit = git(main, 'commit', '-q', '-m', 'fifth')

		assert.notEqual(commit.status, 0)
	})

	it('passes the hook its arguments and its standard input, and its exit status back', () => {
		installShim(main, 'post-rewrite')
		trackedHook(main, 'post-rewrite', 'printf "%s|%s" "$1" "$(cat)" > "$(git rev-parse --show-toplevel)/rewrite.txt"; exit 7')

		const run = spawnSync('sh', [join(main, '.git', 'hooks', 'post-rewrite'), 'rebase'], { cwd: main, env, input: 'old new\n', encoding: 'utf8' })

		assert.equal(run.status, 7)
		assert.equal(readFileSync(join(main, 'rewrite.txt'), 'utf8'), 'rebase|old new')
	})

	it('runs nothing, and succeeds, when the branch has no tracked hook of that name', () => {
		installShim(main, 'commit-msg')

		const run = spawnSync('sh', [join(main, '.git', 'hooks', 'commit-msg'), 'msg.txt'], { cwd: main, env, encoding: 'utf8' })

		assert.equal(run.status, 0)
	})

	it('runs nothing, and succeeds, outside a work tree', () => {
		const outside = mkdtempSync(join(tmpdir(), 'hook-shim-outside-'))
		try {
			const run = spawnSync('sh', [SHIM], { cwd: outside, env, encoding: 'utf8' })
			assert.equal(run.status, 0)
		} finally {
			rmSync(outside, { recursive: true, force: true })
		}
	})
})

describe('init-dev.sh installs the shim', () => {
	const script = readFileSync(join(REPO, 'init-dev.sh'), 'utf8')

	it('copies the shim under every hook name, not the hook itself', () => {
		assert.match(script, /cp "tools\/dev\/git-hook-shim\.sh" "\$HOOKS_DIR\/\$hook"/)
		assert.doesNotMatch(script, /cp "\.githooks\/\$hook"/)
	})

	it('compares each installed hook with the shim, so a stale full copy is replaced', () => {
		assert.match(script, /cmp -s "tools\/dev\/git-hook-shim\.sh" "\$HOOKS_DIR\/\$hook"/)
	})
})
