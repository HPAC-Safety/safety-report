import { describe, it, before, after } from 'node:test'
import assert from 'node:assert/strict'
import { spawnSync } from 'node:child_process'
import { chmodSync, cpSync, mkdirSync, mkdtempSync, readdirSync, readFileSync, rmSync, statSync, writeFileSync } from 'node:fs'
import { tmpdir } from 'node:os'
import { dirname, join } from 'node:path'
import { fileURLToPath } from 'node:url'

const REPO = join(dirname(fileURLToPath(import.meta.url)), '..', '..', '..')
const CHECKS = join(REPO, '.githooks', 'pre-commit.d')

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

const checkFiles = () => readdirSync(CHECKS).filter((name) => name.endsWith('.sh')).sort()

describe('.githooks/pre-commit.d', () => {
	it('holds one executable, numbered file per check', () => {
		const files = checkFiles()

		assert.ok(files.length > 0)
		for (const name of files) {
			assert.match(name, /^\d\d-[a-z0-9-]+\.sh$/, name)
			assert.ok((statSync(join(CHECKS, name)).mode & 0o111) !== 0, `${name} is executable`)
		}
		assert.equal(new Set(files.map((name) => name.slice(0, 2))).size, files.length, 'no two checks share a number')
	})

	it('opens each check with a header saying what it guards', () => {
		for (const name of checkFiles()) {
			const lines = readFileSync(join(CHECKS, name), 'utf8').split('\n')

			assert.equal(lines[0], '#!/usr/bin/env sh', name)
			assert.match(lines.slice(1, 4).join('\n'), /^#\n# \S/, `${name} has a header comment`)
		}
	})

	it('runs the locale stubber before the parity check, which reads what it re-staged', () => {
		const code = readFileSync(join(CHECKS, '10-locales.sh'), 'utf8')
			.split('\n')
			.filter((line) => !line.trimStart().startsWith('#'))
			.join('\n')

		assert.ok(code.indexOf('stub-missing-translations.ts') > -1)
		assert.ok(code.indexOf('check-locales.ts') > code.indexOf('stub-missing-translations.ts'))
	})

	it('runs dotnet format last, after every check that reads the index', () => {
		assert.equal(checkFiles().at(-1), '90-dotnet-format.sh')
	})
})

describe('the pre-commit runner', () => {
	let root = ''

	// The real runner and its checks, in a repository whose commits touch none
	// of the files the real checks wake on — plus stand-in checks that record
	// that they ran, and one that fails.
	before(() => {
		root = mkdtempSync(join(tmpdir(), 'pre-commit-runner-'))
		git(root, 'init', '-q', '-b', 'main')
		cpSync(join(REPO, '.githooks'), join(root, '.githooks'), { recursive: true })
		writeFileSync(join(root, 'file.txt'), 'one\n')
		git(root, 'add', 'file.txt')
		git(root, '-c', 'core.hooksPath=/dev/null', 'commit', '-q', '-m', 'first')
		git(root, 'checkout', '-q', '-b', 'issue-1/work')
	})

	after(() => rmSync(root, { recursive: true, force: true }))

	// A stand-in `node`, `npm`, and `dotnet` first on the PATH record any call,
	// so a test can tell whether a real check woke.
	function runHook() {
		const bin = join(root, '.fake-bin')
		mkdirSync(bin, { recursive: true })
		for (const tool of ['node', 'npm', 'dotnet']) {
			writeFileSync(join(bin, tool), `#!/usr/bin/env sh\necho "${tool} $*" >> "${join(root, 'tools-called.txt')}"\n`)
			chmodSync(join(bin, tool), 0o755)
		}
		rmSync(join(root, 'tools-called.txt'), { force: true })
		rmSync(join(root, 'ran.txt'), { force: true })
		return spawnSync('sh', [join(root, '.githooks', 'pre-commit')], {
			cwd: root,
			env: { ...env, PATH: `${bin}:${process.env.PATH ?? ''}` },
			encoding: 'utf8',
		})
	}

	const read = (name: string) => {
		try {
			return readFileSync(join(root, name), 'utf8').trim().split('\n')
		} catch {
			return []
		}
	}

	function stub(name: string, body: string) {
		writeFileSync(join(root, '.githooks', 'pre-commit.d', name), `#!/usr/bin/env sh\n${body}\n`)
	}

	it('runs no check when the commit touches none of their files', () => {
		writeFileSync(join(root, 'file.txt'), 'two\n')
		git(root, 'add', 'file.txt')

		const result = runHook()

		assert.equal(result.status, 0, result.stderr)
		assert.deepEqual(read('tools-called.txt'), [], 'no check called a tool')
	})

	it('wakes only the check for the staged kind of file', () => {
		writeFileSync(join(root, 'notes.md'), '# Notes\n')
		git(root, 'add', 'notes.md')

		const result = runHook()
		git(root, 'reset', '-q', 'notes.md')

		assert.equal(result.status, 0, result.stderr)
		assert.deepEqual(read('tools-called.txt'), ['node tools/docs/check-frontmatter.ts notes.md', 'node tools/docs/check-links.ts notes.md'])
	})

	it('runs every check in lexical order, with the staged files and the branch', () => {
		stub('00-first.sh', 'echo "00 $BRANCH $STAGED" >> ran.txt')
		stub('99-last.sh', 'echo 99 >> ran.txt')

		const result = runHook()

		assert.equal(result.status, 0, result.stderr)
		assert.deepEqual(read('ran.txt'), ['00 issue-1/work file.txt', '99'])
	})

	it('still runs every later check when one fails, then refuses the commit and names it', () => {
		stub('00-first.sh', 'echo 00 >> ran.txt; exit 3')
		stub('99-last.sh', 'echo 99 >> ran.txt')

		const result = runHook()

		assert.equal(result.status, 1)
		assert.deepEqual(read('ran.txt'), ['00', '99'])
		assert.match(result.stderr, /^pre-commit: 00-first\.sh failed$/m)
		assert.doesNotMatch(result.stderr, /99-last\.sh failed/)
	})

	it('runs a check whether or not it is executable', () => {
		stub('00-first.sh', 'echo 00 >> ran.txt')
		stub('99-last.sh', 'echo 99 >> ran.txt')
		chmodSync(join(root, '.githooks', 'pre-commit.d', '00-first.sh'), 0o644)

		const result = runHook()

		assert.equal(result.status, 0, result.stderr)
		assert.deepEqual(read('ran.txt'), ['00', '99'])
	})

	it('fails closed, naming the directory, when it finds no check at all', () => {
		const checks = join(root, '.githooks', 'pre-commit.d')
		const aside = join(root, '.githooks', 'pre-commit.d.aside')
		cpSync(checks, aside, { recursive: true })
		rmSync(checks, { recursive: true, force: true })
		try {
			const result = runHook()

			assert.equal(result.status, 1)
			assert.match(result.stderr, /^pre-commit: no checks found in .*pre-commit\.d$/m)
		} finally {
			cpSync(aside, checks, { recursive: true })
			rmSync(aside, { recursive: true, force: true })
		}
	})

	it('is a runner: every check it runs is a file under pre-commit.d', () => {
		const runner = readFileSync(join(REPO, '.githooks', 'pre-commit'), 'utf8')

		assert.ok(runner.split('\n').length < 40, 'under 40 lines')
		assert.doesNotMatch(runner, /node tools\//, 'holds no check of its own')
		assert.match(runner, /for check in "\$HOOK_DIR"\/pre-commit\.d\/\*\.sh/)
	})
})
