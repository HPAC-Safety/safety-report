import { describe, it } from 'node:test'
import assert from 'node:assert/strict'
import { execFileSync } from 'node:child_process'
import { chmodSync, mkdirSync, mkdtempSync, readFileSync, writeFileSync } from 'node:fs'
import { tmpdir } from 'node:os'
import { dirname, join } from 'node:path'
import { fileURLToPath } from 'node:url'

import { afterRejection, main, matchesAny } from '../../tools/push-to-pr-branch.mjs'

const REPO = join(dirname(fileURLToPath(import.meta.url)), '..', '..')
const BRANCH = 'issue-1/feature'
const PATHS = 'features/**,docs/traceability.md'

const git = (cwd, ...args) => execFileSync('git', args, { cwd, encoding: 'utf8', stdio: ['ignore', 'pipe', 'pipe'] }).trim()

function commitFile(cwd, file, content, message) {
	mkdirSync(dirname(join(cwd, file)), { recursive: true })
	writeFileSync(join(cwd, file), content)
	git(cwd, 'add', file)
	git(cwd, 'commit', '--quiet', '-m', message)
	return git(cwd, 'rev-parse', 'HEAD')
}

function clone(origin, name, root) {
	const dir = join(root, name)
	git(root, 'clone', '--quiet', origin, dir)
	git(dir, 'config', 'user.name', 'test')
	git(dir, 'config', 'user.email', 'test@example.invalid')
	git(dir, 'config', 'commit.gpgsign', 'false')
	return dir
}

/**
 * A bare origin with the pull request's branch at the event SHA, and a bot's
 * checkout holding its one commit on top of that SHA — what either workflow
 * has when it reaches its push.
 */
function scenario({ paths = PATHS } = {}) {
	const root = mkdtempSync(join(tmpdir(), 'push-to-pr-branch-'))
	const origin = join(root, 'origin.git')
	git(root, 'init', '--quiet', '--bare', '--initial-branch', BRANCH, origin)

	const author = clone(origin, 'author', root)
	git(author, 'switch', '--quiet', '-c', BRANCH)
	commitFile(author, 'features/a.feature', 'Feature: A\n', 'Author')
	commitFile(author, 'docs/traceability.md', 'old matrix\n', 'Matrix')
	git(author, 'push', '--quiet', 'origin', BRANCH)
	const eventSha = git(author, 'rev-parse', 'HEAD')

	const bot = clone(origin, 'bot', root)
	git(bot, 'checkout', '--quiet', '--detach', eventSha)
	commitFile(bot, 'docs/traceability.md', 'new matrix\n', 'Regenerate the traceability matrix')

	const run = () => silently(() => main(['--branch', BRANCH, '--event-sha', eventSha, '--paths', paths], bot))
	const branchFiles = () => {
		git(author, 'fetch', '--quiet', 'origin', BRANCH)
		return git(author, 'log', '--format=%s', `origin/${BRANCH}`).split('\n')
	}
	/** Installs a pre-receive hook on the origin, so a test can refuse every push. */
	const hook = (script) => {
		writeFileSync(join(origin, 'hooks', 'pre-receive'), `#!/bin/sh\n${script}\n`)
		chmodSync(join(origin, 'hooks', 'pre-receive'), 0o755)
	}
	return { author, bot, eventSha, origin, run, branchFiles, hook }
}

function silently(action) {
	const original = { log: console.log, error: console.error }
	const output = []
	console.log = console.error = (...args) => output.push(args.join(' '))
	try {
		return { code: action(), output: output.join('\n') }
	} finally {
		console.log = original.log
		console.error = original.error
	}
}

describe('matchesAny', () => {
	it('matches a file under a dir/** pattern and an exact path', () => {
		assert.equal(matchesAny('features/review/review.feature', ['features/**']), true)
		assert.equal(matchesAny('locales/en-CA.json', ['locales/en-CA.json']), true)
	})

	it('does not match a sibling that only shares a prefix', () => {
		assert.equal(matchesAny('features-old/a.feature', ['features/**']), false)
		assert.equal(matchesAny('locales/fr-CA.json', ['locales/en-CA.json']), false)
	})

	it('refuses a pattern shape it does not understand', () => {
		assert.throws(() => matchesAny('a.md', ['docs/*.md']), /Unsupported paths pattern/)
		assert.throws(() => matchesAny('a.md', ['docs/*/**']), /Unsupported paths pattern/)
	})
})

describe('afterRejection', () => {
	const base = { eventSha: 'a', patterns: ['features/**'] }

	it('fails when the branch has not moved', () => {
		assert.equal(afterRejection({ ...base, branchSha: 'a', changed: [] }), 'failed')
	})

	it('is superseded when the newer push changed a file this workflow is triggered by', () => {
		assert.equal(afterRejection({ ...base, branchSha: 'b', changed: ['locales/fr-CA.json', 'features/a.feature'] }), 'superseded')
	})

	it('retries when the newer push changed only files outside its triggers', () => {
		assert.equal(afterRejection({ ...base, branchSha: 'b', changed: ['locales/fr-CA.json'] }), 'retry')
	})
})

describe('push-to-pr-branch', () => {
	it('pushes onto a branch that has not moved', () => {
		const { run, branchFiles } = scenario()

		assert.equal(run().code, 0)
		assert.equal(branchFiles()[0], 'Regenerate the traceability matrix')
	})

	it('replays its commit on top of another bot that pushed first', () => {
		const { author, run, branchFiles } = scenario()
		commitFile(author, 'locales/fr-CA.json', '{}\n', 'Translate new/changed locale keys')
		git(author, 'push', '--quiet', 'origin', BRANCH)

		const { code, output } = run()

		assert.equal(code, 0, output)
		assert.deepEqual(branchFiles().slice(0, 2), ['Regenerate the traceability matrix', 'Translate new/changed locale keys'])
	})

	it('stands down when the author pushed a change that starts its own run', () => {
		const { author, run, branchFiles } = scenario()
		commitFile(author, 'features/b.feature', 'Feature: B\n', 'Author again')
		git(author, 'push', '--quiet', 'origin', BRANCH)

		const { code, output } = run()

		assert.equal(code, 0, output)
		assert.match(output, /::notice::/)
		assert.equal(branchFiles()[0], 'Author again')
	})

	it('fails when its commit does not apply on top of the newer head', () => {
		// The matrix is not a trigger here, so the run replays rather than
		// stands down, and the replay conflicts with the matrix already there.
		const { author, run } = scenario({ paths: 'features/**' })
		commitFile(author, 'docs/traceability.md', 'hand-edited matrix\n', 'Hand edit')
		git(author, 'push', '--quiet', 'origin', BRANCH)

		const { code, output } = run()

		assert.equal(code, 1, output)
		assert.match(output, /does not apply/)
	})
})

describe('push-to-pr-branch failures', () => {
	it('prints its usage when an argument is missing', () => {
		const { code, output } = silently(() => main(['--branch', BRANCH]))

		assert.equal(code, 1)
		assert.match(output, /^Usage:/)
	})

	it('fails when the push is refused and the branch has not moved', () => {
		const { hook, run } = scenario()
		hook('exit 1')

		const { code, output } = run()

		assert.equal(code, 1)
		assert.match(output, /the branch has not moved/)
	})

	it('fails when it cannot fetch the branch after a rejection', () => {
		const { bot, run } = scenario()
		git(bot, 'remote', 'set-url', 'origin', join(dirname(bot), 'gone.git'))

		const { code, output } = run()

		assert.equal(code, 1)
		assert.match(output, /Could not fetch/)
	})

	it('gives up after five attempts when the branch keeps moving under it', () => {
		const { hook, run } = scenario()
		// Every push is refused, and the branch gains an empty commit each time
		// — a push that changes no trigger path, so the run keeps replaying.
		// Refs can't be updated inside the push's quarantine, so step out of it.
		hook(`env -u GIT_QUARANTINE_PATH -u GIT_OBJECT_DIRECTORY -u GIT_ALTERNATE_OBJECT_DIRECTORIES sh -c '
parent=$(git rev-parse refs/heads/${BRANCH})
next=$(git commit-tree "$parent^{tree}" -p "$parent" -m moved)
git update-ref refs/heads/${BRANCH} "$next"'
exit 1`)

		const { code, output } = run()

		assert.equal(code, 1)
		assert.match(output, /in 5 attempts/)
	})
})

describe('the workflows that push through it', () => {
	/** The `pull_request_target.paths` list a workflow file declares. */
	function triggerPaths(workflow) {
		const text = readFileSync(join(REPO, '.github/workflows', workflow), 'utf8')
		const block = text.match(/^ {2}pull_request_target:\n {4}paths:\n((?: {6}- .*\n)+)/m)
		assert.ok(block, `${workflow} declares no pull_request_target paths`)
		return block[1].split('\n').filter(Boolean).map((line) => line.replace(/^ {6}- /, '').trim())
	}

	/** The --paths a workflow passes to this tool. */
	function passedPaths(workflow) {
		const text = readFileSync(join(REPO, '.github/workflows', workflow), 'utf8')
		const match = text.match(/push-to-pr-branch\.mjs[\s\S]*?--paths '([^']+)'/)
		assert.ok(match, `${workflow} does not push through tools/push-to-pr-branch.mjs`)
		return match[1].split(',')
	}

	for (const workflow of ['traceability.yml', 'i18n-translate.yml']) {
		it(`${workflow} passes exactly the paths it is triggered by`, () => {
			assert.deepEqual(passedPaths(workflow), triggerPaths(workflow))
		})
	}
})
