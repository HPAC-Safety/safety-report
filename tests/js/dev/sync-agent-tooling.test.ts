import { describe, it, beforeEach, afterEach } from 'node:test'
import assert from 'node:assert/strict'
import { spawnSync } from 'node:child_process'
import { chmodSync, copyFileSync, existsSync, mkdirSync, mkdtempSync, readFileSync, rmSync, writeFileSync } from 'node:fs'
import { tmpdir } from 'node:os'
import { dirname, join } from 'node:path'
import { fileURLToPath } from 'node:url'

const REPO = join(dirname(fileURLToPath(import.meta.url)), '..', '..', '..')
const SCRIPT = join(REPO, 'tools', 'dev', 'sync-agent-tooling.sh')
const LIB = join(REPO, '.githooks', 'lib', 'install-agent-tooling.sh')

const env = {
	...process.env,
	GIT_CONFIG_GLOBAL: '/dev/null',
	GIT_CONFIG_SYSTEM: '/dev/null',
	GIT_AUTHOR_NAME: 'Test',
	GIT_AUTHOR_EMAIL: 'test@example.test',
	GIT_COMMITTER_NAME: 'Test',
	GIT_COMMITTER_EMAIL: 'test@example.test',
}

// A stand-in `skillfile`: every call is recorded; `list` prints the names declared in agents.txt or skills.txt.
const FAKE_SKILLFILE = `#!/usr/bin/env sh
echo "$@" >> "$FAKE_DIR/calls.txt"
case "$1" in
install)
	[ -n "\${FAKE_INSTALL_FAILS-}" ] && exit 1
	exit 0 ;;
list)
	[ "$2" = --json ] && exit 0
	case "$3" in
	--agents) cat "$FAKE_DIR/agents.txt" ;;
	--skills) cat "$FAKE_DIR/skills.txt" ;;
	esac ;;
esac
`

describe('the session-start agent-tooling check', () => {
	let root = ''
	let fake = ''

	function run(extra: Record<string, string> = {}, withSkillfile = true) {
		const path = withSkillfile ? `${join(fake, 'bin')}:${process.env.PATH ?? ''}` : '/usr/bin:/bin'
		return spawnSync('sh', [SCRIPT], { cwd: root, env: { ...env, PATH: path, FAKE_DIR: fake, ...extra }, encoding: 'utf8' })
	}

	const installCalled = () => existsSync(join(fake, 'calls.txt')) && readFileSync(join(fake, 'calls.txt'), 'utf8').split('\n').includes('install')

	function declare(agents: string[], skills: string[]) {
		writeFileSync(join(fake, 'agents.txt'), agents.map((n) => `${n}\n`).join(''))
		writeFileSync(join(fake, 'skills.txt'), skills.map((n) => `${n}\n`).join(''))
	}

	function installed(...paths: string[]) {
		for (const p of paths) {
			mkdirSync(dirname(join(root, p)), { recursive: true })
			writeFileSync(join(root, p), 'x\n')
		}
	}

	beforeEach(() => {
		root = mkdtempSync(join(tmpdir(), 'sync-tooling-'))
		fake = mkdtempSync(join(tmpdir(), 'sync-tooling-fake-'))
		mkdirSync(join(fake, 'bin'))
		writeFileSync(join(fake, 'bin', 'skillfile'), FAKE_SKILLFILE)
		chmodSync(join(fake, 'bin', 'skillfile'), 0o755)
		// The script sources the lib from the repository it runs in.
		mkdirSync(join(root, '.githooks', 'lib'), { recursive: true })
		copyFileSync(LIB, join(root, '.githooks', 'lib', 'install-agent-tooling.sh'))
		spawnSync('git', ['init', '-q', '-b', 'work'], { cwd: root, env })
	})

	afterEach(() => {
		rmSync(root, { recursive: true, force: true })
		rmSync(fake, { recursive: true, force: true })
	})

	it('is silent and runs no install when the installed names equal the declared ones', () => {
		declare(['backend'], ['coding-conventions'])
		installed('.claude/agents/backend.md', '.claude/skills/coding-conventions/SKILL.md')

		const result = run()

		assert.equal(result.status, 0)
		assert.equal(result.stdout, '')
		assert.ok(!installCalled())
	})

	it('installs when a declared entry is missing', () => {
		declare(['backend', 'ux'], ['coding-conventions'])
		installed('.claude/agents/backend.md', '.claude/skills/coding-conventions/SKILL.md')

		const result = run()

		assert.equal(result.status, 0)
		assert.ok(installCalled())
		assert.match(result.stdout, /^session-start: /)
	})

	it('installs and prunes when an installed entry is no longer declared', () => {
		declare(['backend'], ['coding-conventions'])
		installed('.claude/agents/backend.md', '.claude/agents/implementer.md', '.claude/skills/coding-conventions/SKILL.md')

		const result = run()

		assert.equal(result.status, 0)
		assert.ok(installCalled())
		assert.ok(!existsSync(join(root, '.claude/agents/implementer.md')))
		assert.ok(existsSync(join(root, '.claude/agents/backend.md')))
	})

	it('compares skills as well as agents', () => {
		declare(['backend'], ['coding-conventions'])
		installed('.claude/agents/backend.md', '.claude/skills/coding-conventions/SKILL.md', '.claude/skills/retired/SKILL.md')

		run()

		assert.ok(installCalled())
		assert.ok(!existsSync(join(root, '.claude/skills/retired')))
	})

	it('trusts nothing and does nothing when skillfile lists nothing', () => {
		declare([], [])
		installed('.claude/agents/backend.md')

		const result = run()

		assert.equal(result.status, 0)
		assert.ok(!installCalled())
	})

	it('is silent when skillfile is not on PATH', () => {
		declare(['backend'], [])

		const result = run({}, false)

		assert.equal(result.status, 0)
		assert.equal(result.stdout, '')
		assert.ok(!installCalled())
	})

	it('exits 0 and says so when the install fails', () => {
		declare(['backend'], ['coding-conventions'])

		const result = run({ FAKE_INSTALL_FAILS: '1' })

		assert.equal(result.status, 0)
		assert.match(result.stdout, /session-start: skillfile install failed/)
	})
})
