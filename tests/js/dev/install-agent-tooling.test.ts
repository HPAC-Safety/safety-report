import { describe, it, beforeEach, afterEach } from 'node:test'
import assert from 'node:assert/strict'
import { spawnSync } from 'node:child_process'
import { chmodSync, existsSync, mkdirSync, mkdtempSync, readFileSync, rmSync, symlinkSync, writeFileSync } from 'node:fs'
import { tmpdir } from 'node:os'
import { dirname, join } from 'node:path'
import { fileURLToPath } from 'node:url'

const REPO = join(dirname(fileURLToPath(import.meta.url)), '..', '..', '..')
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

function git(cwd: string, ...args: string[]) {
	const result = spawnSync('git', args, { cwd, env, encoding: 'utf8' })
	return { status: result.status, out: result.stdout.trim() }
}

// A stand-in `skillfile`: `install` is recorded and, when asked, fails or rewrites the lock; `list` prints the
// names the test declared in agents.txt or skills.txt.
const FAKE_SKILLFILE = `#!/usr/bin/env sh
echo "$@" >> "$FAKE_DIR/calls.txt"
case "$1" in
install)
	[ -n "\${FAKE_INSTALL_FAILS-}" ] && exit 1
	[ -n "\${FAKE_REWRITES_LOCK-}" ] && echo rewritten > Skillfile.lock
	exit 0 ;;
list)
	[ "$2" = --json ] && { cat "$FAKE_DIR/list.json"; exit 0; }
	case "$3" in
	--agents) cat "$FAKE_DIR/agents.txt" ;;
	--skills) cat "$FAKE_DIR/skills.txt" ;;
	esac ;;
esac
`

describe('the agent-tooling install and prune', () => {
	let root = ''
	let fake = ''

	function run(snippet: string, extra: Record<string, string> = {}, withSkillfile = true) {
		const path = withSkillfile ? `${join(fake, 'bin')}:${process.env.PATH ?? ''}` : (process.env.PATH ?? '')
		return spawnSync('sh', ['-c', `. "${LIB}"; ${snippet}`], {
			cwd: root,
			env: { ...env, PATH: path, FAKE_DIR: fake, ...extra },
			encoding: 'utf8',
		})
	}

	const calls = () => (existsSync(join(fake, 'calls.txt')) ? readFileSync(join(fake, 'calls.txt'), 'utf8') : '')

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
		root = mkdtempSync(join(tmpdir(), 'agent-tooling-'))
		fake = mkdtempSync(join(tmpdir(), 'agent-tooling-fake-'))
		mkdirSync(join(fake, 'bin'))
		writeFileSync(join(fake, 'bin', 'skillfile'), FAKE_SKILLFILE)
		chmodSync(join(fake, 'bin', 'skillfile'), 0o755)
		git(root, 'init', '-q', '-b', 'work')
		writeFileSync(join(root, 'Skillfile.lock'), 'locked\n')
		writeFileSync(join(root, 'README.md'), 'one\n')
		writeFileSync(join(root, '.gitignore'), '.skillfile/cache/\n.claude/\n')
		git(root, 'add', '.')
		git(root, 'commit', '-q', '-m', 'first')
	})

	afterEach(() => {
		rmSync(root, { recursive: true, force: true })
		rmSync(fake, { recursive: true, force: true })
	})

	function commitChange(file: string) {
		const orig = git(root, 'rev-parse', 'HEAD').out
		mkdirSync(dirname(join(root, file)), { recursive: true })
		writeFileSync(join(root, file), 'changed\n')
		git(root, 'add', '.')
		git(root, 'commit', '-q', '-m', 'change')
		git(root, 'update-ref', 'ORIG_HEAD', orig)
	}

	for (const file of ['Skillfile', 'Skillfile.lock', 'agents/backend.md', 'skills/x/SKILL.md']) {
		it(`wakes when ${file} changed`, () => {
			commitChange(file)
			assert.equal(run('agent_tooling_changed').status, 0)
		})
	}

	it('stays asleep when nothing it reads changed', () => {
		commitChange('src/app.cs')
		assert.notEqual(run('agent_tooling_changed').status, 0)
	})

	it('stays asleep for a look-alike path', () => {
		commitChange('docs/agents/notes.md')
		assert.notEqual(run('agent_tooling_changed').status, 0)
	})

	it('removes a retired agent and skill, and keeps the declared ones', () => {
		declare(['backend'], ['coding-conventions'])
		installed('.claude/agents/backend.md', '.claude/agents/implementer.md', '.claude/skills/coding-conventions/SKILL.md', '.claude/skills/clarify-hpac-requirements/SKILL.md')

		const result = run('install_agent_tooling test')

		assert.equal(result.status, 0)
		assert.ok(existsSync(join(root, '.claude/agents/backend.md')))
		assert.ok(!existsSync(join(root, '.claude/agents/implementer.md')))
		assert.ok(existsSync(join(root, '.claude/skills/coding-conventions/SKILL.md')))
		assert.ok(!existsSync(join(root, '.claude/skills/clarify-hpac-requirements')))
	})

	it('touches nothing outside .claude/agents and .claude/skills', () => {
		declare(['backend'], ['coding-conventions'])
		installed('.claude/settings.json', '.claude/commands/mine.md', 'agents/old.md')

		run('install_agent_tooling test')

		assert.ok(existsSync(join(root, '.claude/settings.json')))
		assert.ok(existsSync(join(root, '.claude/commands/mine.md')))
		assert.ok(existsSync(join(root, 'agents/old.md')))
	})

	it('prunes nothing when skillfile lists nothing', () => {
		declare([], [])
		installed('.claude/agents/backend.md', '.claude/skills/coding-conventions/SKILL.md')

		run('install_agent_tooling test')

		assert.ok(existsSync(join(root, '.claude/agents/backend.md')))
		assert.ok(existsSync(join(root, '.claude/skills/coding-conventions/SKILL.md')))
	})

	it('prunes nothing when the install fails, and returns failure', () => {
		declare(['backend'], ['coding-conventions'])
		installed('.claude/agents/implementer.md')

		const result = run('install_agent_tooling test', { FAKE_INSTALL_FAILS: '1' })

		assert.equal(result.status, 1)
		assert.ok(existsSync(join(root, '.claude/agents/implementer.md')))
	})

	it('is silent and does nothing when skillfile is not on PATH', () => {
		// The real PATH may hold a real skillfile; a PATH of /usr/bin:/bin alone does not.
		const result = spawnSync('sh', ['-c', `. "${LIB}"; install_agent_tooling test`], {
			cwd: root,
			env: { ...env, PATH: '/usr/bin:/bin' },
			encoding: 'utf8',
		})

		assert.equal(result.status, 0)
		assert.equal(result.stdout, '')
		assert.equal(calls(), '')
	})

	it('announces a Skillfile.lock the install rewrote, off main', () => {
		declare(['backend'], ['coding-conventions'])

		const result = run('install_agent_tooling post-merge', { FAKE_REWRITES_LOCK: '1' })

		assert.match(result.stdout, /post-merge: Skillfile\.lock changed by skillfile install — include it in your next commit\./)
		assert.equal(readFileSync(join(root, 'Skillfile.lock'), 'utf8'), 'rewritten\n')
	})

	it('removes a retired name that is a substring of a declared one', () => {
		declare(['spec-reviewer'], ['coding-conventions'])
		installed('.claude/agents/spec-reviewer.md', '.claude/agents/reviewer.md')

		run('install_agent_tooling test')

		assert.ok(existsSync(join(root, '.claude/agents/spec-reviewer.md')))
		assert.ok(!existsSync(join(root, '.claude/agents/reviewer.md')))
	})

	it('never prunes through a symlinked .claude/skills', () => {
		declare(['backend'], ['coding-conventions'])
		const elsewhere = join(fake, 'elsewhere')
		mkdirSync(join(elsewhere, 'personal'), { recursive: true })
		mkdirSync(join(root, '.claude'), { recursive: true })
		symlinkSync(elsewhere, join(root, '.claude/skills'))

		run('install_agent_tooling test')

		assert.ok(existsSync(join(elsewhere, 'personal')))
	})

	it('keeps a non-.md file and a subdirectory under .claude/agents', () => {
		declare(['backend'], ['coding-conventions'])
		installed('.claude/agents/notes.txt', '.claude/agents/sub/inner.md')

		run('install_agent_tooling test')

		assert.ok(existsSync(join(root, '.claude/agents/notes.txt')))
		assert.ok(existsSync(join(root, '.claude/agents/sub/inner.md')))
	})

	it('keeps a plain file under .claude/skills', () => {
		declare(['backend'], ['coding-conventions'])
		installed('.claude/skills/notes.md')

		run('install_agent_tooling test')

		assert.ok(existsSync(join(root, '.claude/skills/notes.md')))
	})

	it('prunes nothing of a kind whose Skillfile has a directory entry, and says why', () => {
		declare(['backend'], ['one'])
		writeFileSync(
			join(fake, 'list.json'),
			JSON.stringify({ entries: [{ name: 'one', entity_type: 'skill', source_type: 'github', location: 'owner/repo:skills' }] }, null, 2),
		)
		installed('.claude/skills/one/SKILL.md', '.claude/skills/deployed-by-the-directory/SKILL.md', '.claude/agents/implementer.md')

		const result = run('install_agent_tooling test')

		assert.ok(existsSync(join(root, '.claude/skills/deployed-by-the-directory')))
		assert.match(result.stdout, /not pruning skills/)
		assert.ok(!existsSync(join(root, '.claude/agents/implementer.md')), 'the other kind is still pruned')
	})

	it('wakes for a non-ASCII path git would otherwise quote', () => {
		commitChange('agents/é.md')
		assert.equal(run('agent_tooling_changed').status, 0)
	})

	it('leaves a Skillfile.lock that was already changed before the install', () => {
		git(root, 'checkout', '-q', '-B', 'main')
		declare(['backend'], ['coding-conventions'])
		writeFileSync(join(root, 'Skillfile.lock'), 'mine\n')

		const result = run('install_agent_tooling post-merge', { FAKE_REWRITES_LOCK: '1' })

		assert.equal(result.stdout, '')
		assert.equal(readFileSync(join(root, 'Skillfile.lock'), 'utf8'), 'rewritten\n', 'neither restored nor announced')
	})

	it('restores a Skillfile.lock the install rewrote, on a detached HEAD at origin/main', () => {
		git(root, 'update-ref', 'refs/remotes/origin/main', 'HEAD')
		git(root, 'checkout', '-q', '--detach')
		declare(['backend'], ['coding-conventions'])

		const result = run('install_agent_tooling post-merge', { FAKE_REWRITES_LOCK: '1' })

		assert.equal(result.stdout, '')
		assert.equal(readFileSync(join(root, 'Skillfile.lock'), 'utf8'), 'locked\n')
	})

	it('restores a Skillfile.lock the install rewrote, on main', () => {
		git(root, 'checkout', '-q', '-B', 'main')
		declare(['backend'], ['coding-conventions'])

		const result = run('install_agent_tooling post-merge', { FAKE_REWRITES_LOCK: '1' })

		assert.equal(result.stdout, '')
		assert.equal(readFileSync(join(root, 'Skillfile.lock'), 'utf8'), 'locked\n')
		assert.equal(git(root, 'status', '--porcelain').out, '')
	})
})
