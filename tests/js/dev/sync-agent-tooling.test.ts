import { describe, it, beforeEach, afterEach } from 'node:test'
import assert from 'node:assert/strict'
import { spawnSync } from 'node:child_process'
import { chmodSync, copyFileSync, existsSync, mkdirSync, mkdtempSync, readFileSync, rmSync, symlinkSync, utimesSync, writeFileSync } from 'node:fs'
import { tmpdir } from 'node:os'
import { dirname, join } from 'node:path'
import { fileURLToPath } from 'node:url'

const REPO = join(dirname(fileURLToPath(import.meta.url)), '..', '..', '..')

const env = {
	...process.env,
	GIT_CONFIG_GLOBAL: '/dev/null',
	GIT_CONFIG_SYSTEM: '/dev/null',
	GIT_AUTHOR_NAME: 'Test',
	GIT_AUTHOR_EMAIL: 'test@example.test',
	GIT_COMMITTER_NAME: 'Test',
	GIT_COMMITTER_EMAIL: 'test@example.test',
}

// A stand-in `skillfile`: every call is recorded; `list` prints the names declared in agents.txt or skills.txt,
// and `list --json` prints list.json when the test wrote one; `install` can be slow or fail.
const FAKE_SKILLFILE = `#!/usr/bin/env sh
echo "$@" >> "$FAKE_DIR/calls.txt"
case "$1" in
install)
	[ -n "\${FAKE_INSTALL_SLEEP-}" ] && sleep "$FAKE_INSTALL_SLEEP"
	[ -n "\${FAKE_INSTALL_FAILS-}" ] && exit 1
	exit 0 ;;
list)
	if [ "$2" = --json ]; then cat "$FAKE_DIR/list.json" 2>/dev/null; exit 0; fi
	case "$3" in
	--agents) cat "$FAKE_DIR/agents.txt" ;;
	--skills) cat "$FAKE_DIR/skills.txt" ;;
	esac ;;
esac
`

const STATE = '.skillfile/cache/agent-tooling'

describe('the session-start agent-tooling check', () => {
	let root = ''
	let fake = ''

	function run(extra: Record<string, string> = {}, withSkillfile = true) {
		const path = withSkillfile ? `${join(fake, 'bin')}:${process.env.PATH ?? ''}` : '/usr/bin:/bin'
		return spawnSync('sh', [join(root, 'tools/dev/sync-agent-tooling.sh')], {
			cwd: root,
			env: { ...env, PATH: path, FAKE_DIR: fake, ...extra },
			encoding: 'utf8',
		})
	}

	// The sync runs detached: wait for its lock to go, which it removes when the job ends.
	function settle() {
		const lock = join(root, STATE, 'sync.lock')
		const until = Date.now() + 10000
		while (existsSync(lock) && Date.now() < until) spawnSync('sleep', ['0.05'])
		assert.ok(!existsSync(lock), 'the background job finished')
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

	// Record what the "last install" was made from, as a successful install does.
	function stamp() {
		const lib = join(root, '.githooks/lib/install-agent-tooling.sh')
		const result = spawnSync('sh', ['-c', `. "${lib}"; _write_stamp`], { cwd: root, env })
		assert.equal(result.status, 0)
	}

	// The two kinds in the shape of a synced checkout, names declared out of alphabetical order.
	function inSyncCheckout() {
		declare(['ux', 'backend'], ['test-from-scenarios', 'coding-conventions'])
		installed('.claude/agents/backend.md', '.claude/agents/ux.md', '.claude/skills/coding-conventions/SKILL.md', '.claude/skills/test-from-scenarios/SKILL.md')
		stamp()
	}

	beforeEach(() => {
		root = mkdtempSync(join(tmpdir(), 'sync-tooling-'))
		fake = mkdtempSync(join(tmpdir(), 'sync-tooling-fake-'))
		mkdirSync(join(fake, 'bin'))
		writeFileSync(join(fake, 'bin', 'skillfile'), FAKE_SKILLFILE)
		chmodSync(join(fake, 'bin', 'skillfile'), 0o755)
		// The script finds the repository and the lib from its own location.
		mkdirSync(join(root, '.githooks', 'lib'), { recursive: true })
		mkdirSync(join(root, 'tools', 'dev'), { recursive: true })
		copyFileSync(join(REPO, '.githooks/lib/install-agent-tooling.sh'), join(root, '.githooks/lib/install-agent-tooling.sh'))
		copyFileSync(join(REPO, 'tools/dev/sync-agent-tooling.sh'), join(root, 'tools/dev/sync-agent-tooling.sh'))
		writeFileSync(join(root, 'Skillfile'), 'install claude-code local\n')
		writeFileSync(join(root, 'Skillfile.lock'), 'locked\n')
		mkdirSync(join(root, 'skills/x'), { recursive: true })
		writeFileSync(join(root, 'skills/x/SKILL.md'), 'skill\n')
		spawnSync('git', ['init', '-q', '-b', 'work'], { cwd: root, env })
	})

	afterEach(() => {
		rmSync(root, { recursive: true, force: true })
		rmSync(fake, { recursive: true, force: true })
	})

	it('is silent and runs no install when names and content match', () => {
		inSyncCheckout()

		const result = run()

		assert.equal(result.status, 0)
		assert.equal(result.stdout, '')
		assert.ok(!installCalled())
	})

	it('installs when a declared entry is missing', () => {
		declare(['backend', 'ux'], ['coding-conventions'])
		installed('.claude/agents/backend.md', '.claude/skills/coding-conventions/SKILL.md')
		stamp()

		const result = run()
		settle()

		assert.equal(result.status, 0)
		assert.ok(installCalled())
		assert.match(result.stdout, /^session-start: agent tooling out of step with the Skillfile — syncing in the background \(log: .*sync\.log\)\n$/)
	})

	it('installs and prunes when an installed agent is no longer declared', () => {
		inSyncCheckout()
		installed('.claude/agents/implementer.md')

		run()
		settle()

		assert.ok(installCalled())
		assert.ok(!existsSync(join(root, '.claude/agents/implementer.md')))
		assert.ok(existsSync(join(root, '.claude/agents/backend.md')))
	})

	it('installs and prunes every agent copy when the Skillfile declares only skills', () => {
		declare([], ['coding-conventions'])
		installed('.claude/agents/backend.md', '.claude/skills/coding-conventions/SKILL.md')
		stamp()

		const result = run()
		settle()

		assert.match(result.stdout, /out of step with the Skillfile/)
		assert.ok(installCalled())
		assert.ok(!existsSync(join(root, '.claude/agents/backend.md')))
		assert.ok(existsSync(join(root, '.claude/skills/coding-conventions/SKILL.md')))
	})

	it('stays silent when the Skillfile declares only skills and no agent is installed', () => {
		declare([], ['coding-conventions'])
		installed('.claude/skills/coding-conventions/SKILL.md')
		stamp()

		const result = run()
		settle()

		assert.equal(result.stdout, '')
		assert.ok(!installCalled())
	})

	it('installs and prunes when an installed skill is no longer declared', () => {
		inSyncCheckout()
		installed('.claude/skills/retired/SKILL.md')

		run()
		settle()

		assert.ok(installCalled())
		assert.ok(!existsSync(join(root, '.claude/skills/retired')))
	})

	it('installs when a skill file was edited and no name changed', () => {
		inSyncCheckout()
		writeFileSync(join(root, 'skills/x/SKILL.md'), 'edited\n')

		run()
		settle()

		assert.ok(installCalled())
	})

	it('installs when Skillfile.lock changed and no name changed', () => {
		inSyncCheckout()
		writeFileSync(join(root, 'Skillfile.lock'), 'bumped ref\n')

		run()
		settle()

		assert.ok(installCalled())
	})

	it('installs when there is no stamp', () => {
		inSyncCheckout()
		rmSync(join(root, STATE, 'stamp'))

		run()
		settle()

		assert.ok(installCalled())
		assert.ok(existsSync(join(root, STATE, 'stamp')), 'the install wrote the stamp')
	})

	it('is in sync again after the install it started', () => {
		inSyncCheckout()
		writeFileSync(join(root, 'skills/x/SKILL.md'), 'edited\n')
		run()
		settle()
		rmSync(join(fake, 'calls.txt'))

		const again = run()

		assert.equal(again.stdout, '')
		assert.ok(!installCalled())
	})

	it('trusts nothing and does nothing for a kind that skillfile lists as nothing', () => {
		declare([], [])
		installed('.claude/agents/backend.md')
		stamp()

		const result = run()

		assert.equal(result.status, 0)
		assert.ok(!installCalled())
	})

	it('does not compare a kind whose Skillfile has a directory entry', () => {
		inSyncCheckout()
		writeFileSync(
			join(fake, 'list.json'),
			JSON.stringify({ entries: [{ name: 'one', entity_type: 'skill', source_type: 'github', location: 'owner/repo:skills' }] }, null, 2),
		)
		installed('.claude/skills/deployed-by-the-directory/SKILL.md')

		const result = run()

		assert.equal(result.stdout, '')
		assert.ok(!installCalled())
	})

	it('does not compare a kind whose installed directory is a symlink', () => {
		declare(['backend', 'ux'], ['coding-conventions'])
		const elsewhere = join(fake, 'elsewhere')
		mkdirSync(elsewhere, { recursive: true })
		mkdirSync(join(root, '.claude'), { recursive: true })
		symlinkSync(elsewhere, join(root, '.claude/agents'))
		installed('.claude/skills/coding-conventions/SKILL.md')
		stamp()

		const result = run()

		assert.equal(result.stdout, '')
		assert.ok(!installCalled())
	})

	it('counts a symlinked entry whose name is declared as installed', () => {
		declare(['backend', 'ux'], ['coding-conventions'])
		installed('.claude/agents/backend.md', '.claude/skills/coding-conventions/SKILL.md')
		writeFileSync(join(fake, 'ux-real.md'), 'ux\n')
		symlinkSync(join(fake, 'ux-real.md'), join(root, '.claude/agents/ux.md'))
		stamp()

		const result = run()

		assert.equal(result.stdout, '')
		assert.ok(!installCalled())
	})

	it('does not count an undeclared symlinked entry as an extra one', () => {
		inSyncCheckout()
		writeFileSync(join(fake, 'mine.md'), 'mine\n')
		symlinkSync(join(fake, 'mine.md'), join(root, '.claude/agents/mine.md'))

		const result = run()

		assert.equal(result.stdout, '')
		assert.ok(!installCalled())
	})

	it('does not count a directory named like an agent file', () => {
		inSyncCheckout()
		mkdirSync(join(root, '.claude/agents/stray.md'))

		const result = run()

		assert.equal(result.stdout, '')
		assert.ok(!installCalled())
	})

	it('ignores a plain file under .claude/skills', () => {
		inSyncCheckout()
		installed('.claude/skills/notes.md')

		const result = run()

		assert.equal(result.stdout, '')
		assert.ok(!installCalled())
	})

	it('returns at once while the install is still running, and a held lock stays quiet', () => {
		inSyncCheckout()
		installed('.claude/agents/implementer.md')

		const started = Date.now()
		const first = run({ FAKE_INSTALL_SLEEP: '2' })
		const took = Date.now() - started

		assert.equal(first.status, 0)
		assert.ok(took < 1500, `returned in ${took} ms, before the 2 s install`)
		assert.match(first.stdout, /syncing in the background/)

		const second = run({ FAKE_INSTALL_SLEEP: '2' })
		assert.equal(second.status, 0)
		assert.equal(second.stdout, '', 'a held lock prints nothing')
		settle()
	})

	it('treats a lock older than 10 minutes as stale', () => {
		inSyncCheckout()
		installed('.claude/agents/implementer.md')
		mkdirSync(join(root, STATE, 'sync.lock'), { recursive: true })
		const old = new Date(Date.now() - 11 * 60 * 1000)
		utimesSync(join(root, STATE, 'sync.lock'), old, old)

		const result = run()
		settle()

		assert.match(result.stdout, /syncing in the background/)
		assert.ok(installCalled())
	})

	it('is silent when skillfile is not on PATH', () => {
		declare(['backend'], [])

		const result = run({}, false)

		assert.equal(result.status, 0)
		assert.equal(result.stdout, '')
		assert.ok(!installCalled())
	})

	it('exits 0 and logs the failure when the install fails', () => {
		inSyncCheckout()
		installed('.claude/agents/implementer.md')

		const result = run({ FAKE_INSTALL_FAILS: '1' })
		settle()

		assert.equal(result.status, 0)
		assert.match(readFileSync(join(root, STATE, 'sync.log'), 'utf8'), /session-start: skillfile install failed/)
		assert.ok(existsSync(join(root, '.claude/agents/implementer.md')), 'nothing is pruned after a failed install')
	})

	it('exits 0 when its lib is missing', () => {
		rmSync(join(root, '.githooks/lib/install-agent-tooling.sh'))

		const result = run()

		assert.equal(result.status, 0)
		assert.equal(result.stdout, '')
	})

	it('exits 0 when its lib predates agent_tooling_in_sync', () => {
		writeFileSync(join(root, '.githooks/lib/install-agent-tooling.sh'), '# an older lib\n')

		const result = run()

		assert.equal(result.status, 0)
		assert.equal(result.stdout, '')
	})
})
