import { describe, it } from 'node:test'
import assert from 'node:assert/strict'

import { isBehavior, main, pullRequestOf } from '../../../tools/spec/check-feature-coverage-diff.mjs'

const MATRIX = '| REQ-SUB-013 | x |\n'

/** A fake exec answering git by its arguments, recording every call. */
function fakeGit({ diffs = {}, commits = '', subjects = {}, messages = {} } = {}) {
	const calls = []
	const exec = (command, args) => {
		calls.push([command, ...args])
		const [verb] = args
		if (verb === 'diff') {
			const range = args[2]
			const kind = args.includes(':(glob).spec/features/**/*.feature') ? 'features' : 'behavior'
			return { status: 0, stdout: diffs[`${range}|${kind}`] ?? '', stderr: '' }
		}
		if (verb === 'rev-list') return { status: 0, stdout: commits, stderr: '' }
		if (args[3] === '--format=%s' || args[2] === '--format=%s') return { status: 0, stdout: subjects[args.at(-1)] ?? '', stderr: '' }
		return { status: 0, stdout: messages[args.at(-1)] ?? '', stderr: '' }
	}
	return { exec, calls }
}

function quiet(fn) {
	const original = { log: console.log, error: console.error }
	const output = []
	console.log = (...args) => output.push(args.join(' '))
	console.error = (...args) => output.push(args.join(' '))
	try {
		return { result: fn((line) => output.push(line)), output }
	} finally {
		console.log = original.log
		console.error = original.error
	}
}

describe('behavior file filter', () => {
	it('drops generated files, e2e steps, and READMEs', () => {
		assert.equal(isBehavior('src/a/b.cs'), true)
		assert.equal(isBehavior('src/web/.features-gen/x.ts'), false)
		assert.equal(isBehavior('tests/e2e/steps/a.ts'), false)
		assert.equal(isBehavior('src/web/README.md'), false)
		assert.equal(isBehavior('tests/e2e/a.spec.ts'), true)
	})
})

describe('queued commit pull request number', () => {
	it('reads the number a squash subject ends with', () => {
		assert.equal(pullRequestOf('Do a thing (#123)'), '123')
		assert.equal(pullRequestOf('Do a thing'), undefined)
	})
})

describe('check feature coverage diff', () => {
	it('checks the pull request range with the body', () => {
		const git = fakeGit({ diffs: { 'abc...HEAD|behavior': 'src/a.cs\nsrc/README.md', 'abc...HEAD|features': '.spec/features/x/x.feature' } })
		const env = { EVENT_NAME: 'pull_request', BASE_SHA: 'abc', PR_BODY: 'Closes #1' }
		const { result, output } = quiet((log) => main({ env, exec: git.exec, log, matrix: MATRIX }))
		assert.equal(result, 0)
		assert.match(output.join('\n'), /Scenarios changed alongside: \.spec\/features\/x\/x\.feature/)
		assert.deepEqual(git.calls[0].slice(0, 4), ['git', 'diff', '--name-only', 'abc...HEAD'])
	})

	it('fails a pull request that changes behavior without a scenario', () => {
		const git = fakeGit({ diffs: { 'abc...HEAD|behavior': 'src/a.cs' } })
		const env = { EVENT_NAME: 'pull_request', BASE_SHA: 'abc', PR_BODY: '' }
		const { result } = quiet((log) => main({ env, exec: git.exec, log, matrix: MATRIX }))
		assert.equal(result, 1)
	})

	it('fails an empty merge group', () => {
		const git = fakeGit()
		const env = { EVENT_NAME: 'merge_group', BASE_SHA: 'abc' }
		const { result, output } = quiet((log) => main({ env, exec: git.exec, log, matrix: MATRIX }))
		assert.equal(result, 1)
		assert.match(output.join('\n'), /merge group holds no commit between abc and HEAD/)
	})

	it('checks every queued commit with its own message and names the one that fails', () => {
		const git = fakeGit({
			commits: 'c1\nc2',
			subjects: { c1: 'Fine (#1)', c2: 'Broken (#2)' },
			messages: { c1: 'Closes #1', c2: 'Closes #2' },
			diffs: { 'c1^..c1|behavior': '', 'c2^..c2|behavior': 'src/a.cs' },
		})
		const env = { EVENT_NAME: 'merge_group', BASE_SHA: 'abc' }
		const { result, output } = quiet((log) => main({ env, exec: git.exec, log, matrix: MATRIX }))
		assert.equal(result, 1)
		const text = output.join('\n')
		assert.match(text, /::group::Fine \(#1\)/)
		assert.match(text, /::group::Broken \(#2\)/)
		assert.match(text, /Queued commit c2 \(pull request #2\) fails the check above/)
		assert.doesNotMatch(text, /Queued commit c1/)
		assert.equal(text.match(/::endgroup::/g).length, 2)
	})

	it('passes a merge group whose commits all pass', () => {
		const git = fakeGit({ commits: 'c1', subjects: { c1: 'Fine (#1)' } })
		const env = { EVENT_NAME: 'merge_group', BASE_SHA: 'abc' }
		const { result } = quiet((log) => main({ env, exec: git.exec, log, matrix: MATRIX }))
		assert.equal(result, 0)
	})
})
