import { describe, it } from 'node:test'
import assert from 'node:assert/strict'

import type { Exec } from '../../../tools/lib/actions.ts'
import { type FetchIssue, type IssueState, closedBy, githubIssues, ignoredClaims, main } from '../../../tools/spec/check-ignored-claims.ts'

const claim = (id: string, tags: string[], status: 'Built' | 'Planned' = 'Planned') => ({ id, file: '.spec/features/media/media.feature', status, tags })
const claims = (...list: ReturnType<typeof claim>[]): string => JSON.stringify({ claims: list })

const issues =
	(states: Record<number, IssueState>): FetchIssue =>
	(issue) =>
		Promise.resolve(states[issue] ?? { error: 'GitHub answered 404' })

async function run(options: Parameters<typeof main>[0]): Promise<{ code: number; output: string }> {
	const lines: string[] = []
	const code = await main({ env: {}, ...options, log: (line) => lines.push(line) })
	return { code, output: lines.join('\n') }
}

describe('ignoredClaims', () => {
	it('reads the one issue each @ignore claim names, and skips built claims', () => {
		const read = ignoredClaims(claims(claim('REQ-MED-001', ['@ignore', '@issue-842']), claim('REQ-MED-002', ['@ui'], 'Built')))
		assert.deepEqual(read, [{ id: 'REQ-MED-001', file: '.spec/features/media/media.feature', issue: 842 }])
	})

	it('says why a claim names no issue, or more than one', () => {
		const [none, two] = ignoredClaims(claims(claim('REQ-MED-001', ['@ignore']), claim('REQ-MED-002', ['@ignore', '@issue-1', '@issue-2'])))
		assert.match(none.problem ?? '', /names no issue/)
		assert.match(two.problem ?? '', /names 2 issues \(#1, #2\)/)
	})
})

describe('closedBy', () => {
	it('reads every closing keyword GitHub honors', () => {
		assert.deepEqual([...closedBy('Closes #1\nfixes #2, Resolved: #3\nRelates to #4\nclose #5', 'o/r')], [1, 2, 3, 5])
	})

	it('reads owner/repo#N and an issue URL, for this repository only', () => {
		const text = 'Closes o/r#6\nFixes https://github.com/O/R/issues/7\nCloses other/repo#8\nResolves https://github.com/other/repo/issues/9'
		assert.deepEqual([...closedBy(text, 'o/r')], [6, 7])
	})

	it('does not read negated prose as closing', () => {
		assert.deepEqual([...closedBy('This does not close #1, and it won\'t fix #2; it never resolves #3.', 'o/r')], [])
	})
})

describe('main', () => {
	it('passes with no @ignore claim, asking GitHub nothing', async () => {
		const { code, output } = await run({ claims: claims(claim('REQ-MED-001', [], 'Built')), fetchIssue: () => Promise.reject(new Error('asked')) })
		assert.equal(code, 0)
		assert.match(output, /0 @ignore claim\(s\)/)
	})

	it('passes an @ignore claim owned by an open issue', async () => {
		const { code, output } = await run({ claims: claims(claim('REQ-MED-001', ['@ignore', '@issue-842'])), fetchIssue: issues({ 842: { state: 'open', pullRequest: false } }) })
		assert.equal(code, 0)
		assert.match(output, /owned by an open issue: #842/)
	})

	it('fails an @ignore claim with no issue', async () => {
		const { code, output } = await run({ claims: claims(claim('REQ-MED-001', ['@ignore'])), fetchIssue: issues({}) })
		assert.equal(code, 1)
		assert.match(output, /::error file=\.spec\/features\/media\/media\.feature::REQ-MED-001 is @ignore and names no issue/)
	})

	it('fails a closed issue, a pull request, and an issue it cannot read', async () => {
		const { code, output } = await run({
			claims: claims(claim('REQ-MED-001', ['@ignore', '@issue-1']), claim('REQ-MED-002', ['@ignore', '@issue-2']), claim('REQ-MED-003', ['@ignore', '@issue-3'])),
			fetchIssue: issues({ 1: { state: 'closed', pullRequest: false }, 2: { state: 'open', pullRequest: true } }),
		})
		assert.equal(code, 1)
		assert.match(output, /REQ-MED-001 name #1, which is closed/)
		assert.match(output, /REQ-MED-002 name #2, a pull request/)
		assert.match(output, /Could not read #3, named by REQ-MED-003: GitHub answered 404/)
	})

	it('fails a pull request that closes an issue an @ignore claim still names', async () => {
		const { code, output } = await run({
			env: { EVENT_NAME: 'pull_request', PR_BODY: 'Closes #842' },
			claims: claims(claim('REQ-MED-001', ['@ignore', '@issue-842'])),
			fetchIssue: issues({ 842: { state: 'open', pullRequest: false } }),
		})
		assert.equal(code, 1)
		assert.match(output, /This closes #842, which REQ-MED-001 still name as @ignore/)
	})

	it('passes a pull request that closes an issue no @ignore claim names', async () => {
		const { code } = await run({ env: { EVENT_NAME: 'pull_request', PR_BODY: 'Closes #1' }, claims: claims(claim('REQ-MED-001', [], 'Built')) })
		assert.equal(code, 0)
	})

	it('asks GitHub for the repository it runs in, with its token', async () => {
		const original = globalThis.fetch
		const asked: { url: string; auth: string | undefined }[] = []
		globalThis.fetch = ((url: string, init: { headers: Record<string, string> }) => {
			asked.push({ url, auth: init.headers.authorization })
			return Promise.resolve(new Response(JSON.stringify({ state: 'open' }), { status: 200 }))
		}) as unknown as typeof fetch
		try {
			const { code } = await run({ env: { GITHUB_REPOSITORY: 'o/r', GH_TOKEN: 't' }, claims: claims(claim('REQ-MED-001', ['@ignore', '@issue-7'])) })
			assert.equal(code, 0)
			assert.deepEqual(asked, [{ url: 'https://api.github.com/repos/o/r/issues/7', auth: 'Bearer t' }])
		} finally {
			globalThis.fetch = original
		}
	})

	it('reads a pull request, an error, and anonymous access from GitHub', async () => {
		const original = globalThis.fetch
		const answers = [new Response(JSON.stringify({ state: 'open', pull_request: {} }), { status: 200 }), new Response('', { status: 403 })]
		const auth: (string | undefined)[] = []
		globalThis.fetch = ((_url: string, init: { headers: Record<string, string> }) => {
			auth.push(init.headers.authorization)
			return Promise.resolve(answers.shift() ?? new Response('', { status: 500 }))
		}) as unknown as typeof fetch
		try {
			const fetchIssue = githubIssues('o/r', undefined)
			assert.deepEqual(await fetchIssue(1), { state: 'open', pullRequest: true })
			assert.deepEqual(await fetchIssue(2), { error: 'GitHub answered 403 for #2' })
			assert.deepEqual(auth, [undefined, undefined])
			const { output } = await run({ claims: claims(claim('REQ-MED-001', ['@ignore', '@issue-9'])) })
			assert.match(output, /Could not read #9/)
		} finally {
			globalThis.fetch = original
		}
	})

	it('turns a network failure into a clear error, and reads GITHUB_TOKEN too', async () => {
		const original = globalThis.fetch
		const auth: (string | undefined)[] = []
		globalThis.fetch = ((_url: string, init: { headers: Record<string, string> }) => {
			auth.push(init.headers.authorization)
			return Promise.reject(new Error('getaddrinfo ENOTFOUND api.github.com'))
		}) as unknown as typeof fetch
		try {
			const { code, output } = await run({ env: { GITHUB_TOKEN: 'g' }, claims: claims(claim('REQ-MED-001', ['@ignore', '@issue-9'])) })
			assert.equal(code, 1)
			assert.match(output, /Could not read #9, named by REQ-MED-001: GitHub could not be reached for #9 \(getaddrinfo ENOTFOUND api\.github\.com\)/)
			assert.deepEqual(auth, ['Bearer g'])
		} finally {
			globalThis.fetch = original
		}
	})

	it('reads every queued commit message in a merge group', async () => {
		const calls: string[][] = []
		const exec: Exec = (command, args = []) => {
			calls.push([command, ...args])
			return { status: 0, stdout: 'Build it (#9)\n\nCloses #842\n', stderr: '' }
		}
		const { code } = await run({
			env: { EVENT_NAME: 'merge_group', BASE_SHA: 'abc' },
			exec,
			claims: claims(claim('REQ-MED-001', ['@ignore', '@issue-842'])),
			fetchIssue: issues({ 842: { state: 'open', pullRequest: false } }),
		})
		assert.equal(code, 1)
		assert.deepEqual(calls, [['git', 'log', '--format=%B', 'abc..HEAD']])
	})
})
