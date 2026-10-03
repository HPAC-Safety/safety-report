import { describe, it } from 'node:test'
import assert from 'node:assert/strict'

import type { Exec } from '../../../tools/lib/actions.ts'
import { isBehavior, main, pullRequestOf } from '../../../tools/spec/check-feature-coverage-diff.ts'

const pr = (body = 'Closes #1') => ({ EVENT_NAME: 'pull_request', BASE_SHA: 'abc', PR_BODY: body })

const CLAIMS = JSON.stringify({
	claims: [
		{ id: 'REQ-SUB-013', area: 'report-submission', engine: 'Reqnroll', stepFiles: ['tests/HpacSafety.Acceptance.Tests/SubmitSteps.cs'] },
		{ id: 'REQ-MED-001', area: 'media', engine: 'Reqnroll', stepFiles: ['tests/HpacSafety.Acceptance.Tests/MediaSteps.cs'] },
	],
})

const AREA_PATHS = JSON.stringify({ every: ['src/Program.cs'], areas: { 'report-submission': ['src/Reports/**'], media: ['src/Media/**'] } })

const FEATURE = '.spec/features/report-submission/report-submission.feature'
const OTHER = '.spec/features/media/media.feature'
const feature = (then: string): string => `Feature: Submission\n\n  @REQ-SUB-013\n  Scenario: A report is accepted\n    Given a report\n    When it is submitted\n    Then ${then}\n`
const media = (then: string): string => `Feature: Media\n\n  @REQ-MED-001\n  Scenario: A file is kept\n    Given a file\n    Then ${then}\n`

/** A fake exec answering git by its arguments, recording every call. */
function fakeGit({
	diffs = {},
	files = {},
	commits = '',
	subjects = {},
	messages = {},
}: {
	/** `<range>|<behavior|features|steps>` to the names it lists. */
	diffs?: Partial<Record<string, string>>
	/** `<rev>:<path>` to its contents; a missing one fails, as git show does. */
	files?: Partial<Record<string, string>>
	commits?: string
	subjects?: Partial<Record<string, string>>
	messages?: Partial<Record<string, string>>
} = {}): { exec: Exec; calls: string[][] } {
	const calls: string[][] = []
	const exec: Exec = (command, args = []) => {
		calls.push([command, ...args])
		const [verb] = args
		if (verb === 'merge-base') return { status: 0, stdout: 'mb\n', stderr: '' }
		if (verb === 'diff') {
			const range = args[2]
			const kind = args.some((arg) => arg.endsWith('.feature')) ? 'features' : args.some((arg) => arg.includes('Acceptance.Tests')) ? 'steps' : 'behavior'
			return { status: 0, stdout: diffs[`${range}|${kind}`] ?? '', stderr: '' }
		}
		if (verb === 'show') {
			const text = files[args[1] ?? '']
			return text === undefined ? { status: 128, stdout: '', stderr: 'fatal: no such path' } : { status: 0, stdout: text, stderr: '' }
		}
		if (verb === 'rev-list') return { status: 0, stdout: commits, stderr: '' }
		if (args[3] === '--format=%s' || args[2] === '--format=%s') return { status: 0, stdout: subjects[args.at(-1) ?? ''] ?? '', stderr: '' }
		return { status: 0, stdout: messages[args.at(-1) ?? ''] ?? '', stderr: '' }
	}
	return { exec, calls }
}

function quiet<T>(fn: (log: (line: string) => void) => T): { result: T; output: string[] } {
	const original = { log: console.log, error: console.error }
	const output: string[] = []
	console.log = (...args: unknown[]) => output.push(args.join(' '))
	console.error = (...args: unknown[]) => output.push(args.join(' '))
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
	const run = (git: ReturnType<typeof fakeGit>, env: Record<string, string>) => quiet((log) => main({ env, exec: git.exec, log, claims: CLAIMS, areaPaths: AREA_PATHS }))

	it('compares the merge base with HEAD, and passes a changed scenario in the area the code maps to', () => {
		const git = fakeGit({
			diffs: { 'mb..HEAD|behavior': 'src/Reports/Submit.cs\nsrc/README.md', 'mb..HEAD|features': FEATURE },
			files: { [`mb:${FEATURE}`]: feature('it is stored'), [`HEAD:${FEATURE}`]: feature('it is stored atomically') },
		})
		const { result, output } = run(git, pr())
		assert.equal(result, 0)
		assert.match(output.join('\n'), /in an area the code maps to: REQ-SUB-013 \(report-submission\)/)
		assert.deepEqual(git.calls[0], ['git', 'merge-base', 'abc', 'HEAD'])
	})

	it('passes a new scenario, which the base does not have', () => {
		const git = fakeGit({ diffs: { 'mb..HEAD|behavior': 'src/Reports/Submit.cs', 'mb..HEAD|features': FEATURE }, files: { [`HEAD:${FEATURE}`]: feature('it is stored') } })
		assert.equal(run(git, pr()).result, 0)
	})

	it('fails an unrelated feature edit', () => {
		const git = fakeGit({
			diffs: { 'mb..HEAD|behavior': 'src/Reports/Submit.cs', 'mb..HEAD|features': OTHER },
			files: { [`mb:${OTHER}`]: media('it is kept'), [`HEAD:${OTHER}`]: media('it is kept forever') },
		})
		const { result, output } = run(git, pr(''))
		assert.equal(result, 1)
		assert.match(output.join('\n'), /REQ-MED-001 \(media\) changed, but the changed code maps to report-submission/)
	})

	it('fails a whitespace- or comment-only feature edit', () => {
		const git = fakeGit({
			diffs: { 'mb..HEAD|behavior': 'src/Reports/Submit.cs', 'mb..HEAD|features': FEATURE },
			files: { [`mb:${FEATURE}`]: feature('it is stored'), [`HEAD:${FEATURE}`]: `# reworded\n${feature('it   is stored').replace(/\n/g, '\n\n')}` },
		})
		const { result, output } = run(git, pr(''))
		assert.equal(result, 1)
		assert.match(output.join('\n'), /no scenario's text did/)
	})

	it('fails an exemption citing claims of an unrelated area', () => {
		const body = 'No .feature scenario needed: refactor — moved the submit handler; nothing it does changed\nClaims preserved: REQ-MED-001\n'
		const git = fakeGit({ diffs: { 'mb..HEAD|behavior': 'src/Reports/Submit.cs' } })
		const { result, output } = run(git, pr(body))
		assert.equal(result, 1)
		assert.match(output.join('\n'), /REQ-MED-001 belongs to media, which none of the changed files maps to \(report-submission\)/)
	})

	it('counts a step definition\'s areas from the claims it binds', () => {
		const body = 'No .feature scenario needed: refactor — moved the submit handler; nothing it does changed\nClaims preserved: REQ-MED-001\n'
		const git = fakeGit({ diffs: { 'mb..HEAD|behavior': 'src/Reports/Submit.cs', 'mb..HEAD|steps': 'tests/HpacSafety.Acceptance.Tests/MediaSteps.cs' } })
		assert.equal(run(git, pr(body)).result, 0)
	})

	it('maps a file listed under every to every area', () => {
		const body = 'No .feature scenario needed: refactor — reordered service registration; nothing it does changed\nClaims preserved: REQ-MED-001\n'
		const git = fakeGit({ diffs: { 'mb..HEAD|behavior': 'src/Program.cs' } })
		assert.equal(run(git, pr(body)).result, 0)
	})

	it('fails a pull request that changes behavior without a scenario', () => {
		const git = fakeGit({ diffs: { 'mb..HEAD|behavior': 'src/Reports/Submit.cs' } })
		assert.equal(run(git, pr('')).result, 1)
	})

	it('fails on an area map that is not one', () => {
		const git = fakeGit()
		const { result, output } = quiet((log) => main({ env: pr(), exec: git.exec, log, claims: CLAIMS, areaPaths: '[]' }))
		assert.equal(result, 1)
		assert.match(output.join('\n'), /area-paths\.json is not an object/)
	})

	it('fails an empty merge group', () => {
		const git = fakeGit()
		const { result, output } = run(git, { EVENT_NAME: 'merge_group', BASE_SHA: 'abc' })
		assert.equal(result, 1)
		assert.match(output.join('\n'), /merge group holds no commit between abc and HEAD/)
	})

	it('checks every queued commit with its own message and names the one that fails', () => {
		const git = fakeGit({
			commits: 'c1\nc2',
			subjects: { c1: 'Fine (#1)', c2: 'Broken (#2)' },
			messages: { c1: 'Closes #1', c2: 'Closes #2' },
			diffs: { 'c1^..c1|behavior': '', 'c2^..c2|behavior': 'src/Reports/Submit.cs' },
		})
		const { result, output } = run(git, { EVENT_NAME: 'merge_group', BASE_SHA: 'abc' })
		assert.equal(result, 1)
		const text = output.join('\n')
		assert.match(text, /::group::Fine \(#1\)/)
		assert.match(text, /::group::Broken \(#2\)/)
		assert.match(text, /Queued commit c2 \(pull request #2\) fails the check above/)
		assert.doesNotMatch(text, /Queued commit c1/)
		assert.equal(text.match(/::endgroup::/g)?.length, 2)
	})

	it('passes a merge group whose commits all pass', () => {
		const git = fakeGit({ commits: 'c1', subjects: { c1: 'Fine (#1)' } })
		assert.equal(run(git, { EVENT_NAME: 'merge_group', BASE_SHA: 'abc' }).result, 0)
	})
})
