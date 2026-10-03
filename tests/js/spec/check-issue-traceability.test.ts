import { describe, it } from 'node:test'
import assert from 'node:assert/strict'
import { spawnSync } from 'node:child_process'
import { dirname, join } from 'node:path'
import { fileURLToPath } from 'node:url'
import { present } from '../helpers/present.ts'

import {
	type Api,
	BOT,
	DRIFT_MILESTONE,
	DRIFT_TITLE,
	driftBody,
	driftProblems,
	escapeCommand,
	type Fetch,
	github,
	issueRow,
	type Item,
	type Issue,
	listedIssues,
	main,
	openIssues,
	renderPage,
	syncDriftIssue,
} from '../../../tools/spec/check-issue-traceability.ts'

const REPO = join(dirname(fileURLToPath(import.meta.url)), '..', '..', '..')

const drift = (number: number, body?: string): Issue => ({ number, title: DRIFT_TITLE, body, user: BOT })

const OPEN: Issue[] = [
	{ number: 30, title: 'Deploy', milestone: 'Phase 2', labels: ['area:infra', 'enhancement'], parent: 9 },
	{ number: 47, title: 'Dependency Dashboard' },
]

const PAGE = renderPage(OPEN, 'o/r')

/** OPEN as GitHub's REST API sends it. */
const OPEN_ITEMS: Item[] = [
	{ number: 30, title: 'Deploy', milestone: { title: 'Phase 2' }, labels: [{ name: 'area:infra' }, { name: 'enhancement' }], parent_issue_url: 'https://api.github.com/repos/o/r/issues/9' },
	{ number: 47, title: 'Dependency Dashboard' },
]

/** A fake GitHub client that records every write and serves fixed lists. */
function fakeApi({ issues = [], closed = [], milestones = [] }: { issues?: Item[]; closed?: Item[]; milestones?: Item[] } = {}): Api & { writes: { method: string; path: string; body: Record<string, unknown> }[] } {
	const writes: { method: string; path: string; body: Record<string, unknown> }[] = []
	return {
		writes,
		list(path) {
			if (path.startsWith('/issues?state=closed')) return Promise.resolve(closed)
			if (path.startsWith('/issues')) return Promise.resolve(issues)
			if (path.startsWith('/milestones')) return Promise.resolve(milestones)
			return Promise.reject(new Error(`unexpected list ${path}`))
		},
		request(method, path, body) {
			writes.push({ method, path, body: body as Record<string, unknown> })
			return Promise.resolve({ number: 900 })
		},
	}
}

/** Runs `main` with console output captured, restoring it afterwards even on failure. */
async function runMain(input: { api: Api | null; markdown: string; sync: boolean }): Promise<{ code: number; output: { log: string[] } }> {
	const output: { log: string[] } = { log: [] }
	const original = console.log
	console.log = (...args: unknown[]) => output.log.push(args.join(' '))
	try {
		return { code: await main({ repository: 'o/r', ...input }), output }
	} finally {
		console.log = original
	}
}

describe('listedIssues', () => {
	it('reads the issue each table row names, and ignores prose', () => {
		assert.deepEqual(listedIssues(`${PAGE}\nSee [#99](https://github.com/o/r/issues/99) in prose.\n`), [30, 47])
	})
})

describe('issueRow', () => {
	it('names the issue, its milestone, its labels in order, and its parent', () => {
		const row = issueRow({ number: 30, title: 'Deploy', milestone: 'Phase 2', labels: ['enhancement', 'area:infra'], parent: 9 }, 'o/r')

		assert.equal(row, '| [#30 — Deploy](https://github.com/o/r/issues/30) | Phase 2 | `area:infra`, `enhancement` | [#9](https://github.com/o/r/issues/9) |')
	})

	it('shows a dash for what an issue lacks', () => {
		assert.equal(issueRow({ number: 47, title: 'Dashboard' }, 'o/r'), '| [#47 — Dashboard](https://github.com/o/r/issues/47) | — | — | — |')
	})

	it('leaves out a label that only says someone is working on it', () => {
		assert.match(issueRow({ number: 47, title: 'x', labels: ['in progress', 'bug'] }, 'o/r'), /\| `bug` \|/)
	})

	it('escapes a backslash before the characters it escapes, so a title cannot undo an escape', () => {
		assert.match(issueRow({ number: 47, title: 'a\\| b' }, 'o/r'), /\[#47 — a\\\\\\\| b\]/)
	})

	it('quotes a label with backticks replaced, so it cannot break out of its code span', () => {
		assert.match(issueRow({ number: 47, title: 'x', labels: ['a`b'] }, 'o/r'), /\| `a'b` \|/)
	})

	it('escapes a title so it cannot break the table or the link', () => {
		assert.match(issueRow({ number: 47, title: 'a | b [c]\nd' }, 'o/r'), /\[#47 — a \\\| b \\\[c\\\] d\]/)
	})
})

describe('renderPage', () => {
	it('lists every open issue by number and leaves out the drift issue', () => {
		const page = renderPage([OPEN[1], drift(60), OPEN[0]], 'o/r')

		assert.deepEqual(listedIssues(page), [30, 47])
		assert.match(page, /^> \*\*Generated file — do not edit by hand\.\*\*$/m)
	})
})

describe('driftProblems', () => {
	it('finds nothing when the page lists exactly the open issues', () => {
		assert.deepEqual(driftProblems({ openIssues: OPEN, markdown: PAGE, repository: 'o/r' }), [])
	})

	it('names an open issue with no row, and a row whose issue is not open', () => {
		const problems = driftProblems({ openIssues: [OPEN[0], { number: 51, title: 'New' }], markdown: PAGE, repository: 'o/r' })

		assert.deepEqual(problems, ['#51 (`New`) is open and has no row.', '#47 has a row but is not an open issue.'])
	})

	it('needs no row for the drift issue itself', () => {
		assert.deepEqual(driftProblems({ openIssues: [...OPEN, drift(60)], markdown: PAGE, repository: 'o/r' }), [])
	})

	it('needs a row for an issue someone else filed under the drift title', () => {
		const impostor = { number: 61, title: DRIFT_TITLE, user: 'someone' }

		assert.deepEqual(driftProblems({ openIssues: [...OPEN, impostor], markdown: PAGE, repository: 'o/r' }), [`#61 (\`${DRIFT_TITLE}\`) is open and has no row.`])
	})

	it('names a row whose title, milestone, labels, or parent changed', () => {
		const changed = [
			{ ...OPEN[0], title: 'Deploy now' },
			{ ...OPEN[0], milestone: 'Phase 3' },
			{ ...OPEN[0], labels: ['bug'] },
			{ ...OPEN[0], parent: undefined },
		]
		for (const issue of changed) {
			assert.deepEqual(driftProblems({ openIssues: [issue, OPEN[1]], markdown: PAGE, repository: 'o/r' }), [`#30 (\`${issue.title}\`) has a row that is out of date.`])
		}
	})

	it('ignores a change to a label that only says someone is working on it', () => {
		const working = { ...OPEN[0], labels: [...(OPEN[0].labels ?? []), 'in progress'] }

		assert.deepEqual(driftProblems({ openIssues: [working, OPEN[1]], markdown: PAGE, repository: 'o/r' }), [])
	})

	it('names a page whose rows match but whose text was edited by hand', () => {
		assert.deepEqual(driftProblems({ openIssues: OPEN, markdown: PAGE.replace('Every open issue', 'Each open issue'), repository: 'o/r' }), ['The page differs from what the generator writes.'])
	})

	it('quotes a title so it can mention no one, whatever backticks it holds', () => {
		const [problem] = driftProblems({ openIssues: [{ number: 51, title: 'Ask @owner about `x`' }], markdown: '', repository: 'o/r' })

		assert.equal(problem, "#51 (`Ask @owner about 'x'`) is open and has no row.")
	})
})

describe('syncDriftIssue', () => {
	const problems = ['#51 (`New`) is open and has no row.']

	it('does nothing when there is no drift and no drift issue', async () => {
		const api = fakeApi()

		assert.equal(await syncDriftIssue({ api, problems: [], openIssues: OPEN, repository: 'o/r' }), 'none')
		assert.deepEqual(api.writes, [])
	})

	it('opens the drift issue with its label and milestone when drift appears', async () => {
		const api = fakeApi({ milestones: [{ number: 4, title: DRIFT_MILESTONE }] })

		const done = await syncDriftIssue({ api, problems, openIssues: OPEN, repository: 'o/r' })

		assert.equal(done, 'opened #900')
		assert.deepEqual(api.writes, [
			{ method: 'POST', path: '/issues', body: { title: DRIFT_TITLE, body: driftBody(problems, 'o/r'), labels: ['documentation', 'area:ci'], milestone: 4 } },
		])
	})

	it('opens it without a milestone when the milestone is gone', async () => {
		const api = fakeApi()

		await syncDriftIssue({ api, problems, openIssues: OPEN, repository: 'o/r' })

		assert.equal(api.writes[0].body.milestone, undefined)
	})

	it('updates the open drift issue only when its body changed', async () => {
		const stale = drift(60, 'old')
		const current = { ...stale, body: driftBody(problems, 'o/r') }
		const api = fakeApi()

		assert.equal(await syncDriftIssue({ api, problems, openIssues: [...OPEN, stale], repository: 'o/r' }), 'updated #60')
		assert.equal(await syncDriftIssue({ api, problems, openIssues: [...OPEN, current], repository: 'o/r' }), 'updated #60')
		assert.deepEqual(api.writes, [{ method: 'PATCH', path: '/issues/60', body: { body: driftBody(problems, 'o/r') } }])
	})

	it('comments on and closes the drift issue once the page matches', async () => {
		const api = fakeApi()

		const done = await syncDriftIssue({ api, problems: [], openIssues: [...OPEN, drift(60)], repository: 'o/r' })

		assert.equal(done, 'closed #60')
		assert.deepEqual(
			api.writes.map(({ method, path }) => `${method} ${path}`),
			['POST /issues/60/comments', 'PATCH /issues/60'],
		)
		assert.equal(api.writes[1].body.state, 'closed')
	})

	it('reopens the most recent closed drift issue instead of filing another', async () => {
		const closed = [
			{ number: 70, title: DRIFT_TITLE, user: { login: BOT } },
			{ number: 90, title: DRIFT_TITLE, user: { login: 'someone' } },
			{ number: 80, title: DRIFT_TITLE, user: { login: BOT } },
		]
		const api = fakeApi({ closed })

		const done = await syncDriftIssue({ api, problems, openIssues: OPEN, repository: 'o/r' })

		assert.equal(done, 'reopened #80')
		assert.deepEqual(api.writes, [{ method: 'PATCH', path: '/issues/80', body: { state: 'open', body: driftBody(problems, 'o/r') } }])
	})

	it('leaves an impostor alone: never updates or closes an issue someone else filed under the title', async () => {
		const impostor = { number: 61, title: DRIFT_TITLE, body: 'mine', user: 'someone' }
		const api = fakeApi()

		assert.equal(await syncDriftIssue({ api, problems: [], openIssues: [...OPEN, impostor], repository: 'o/r' }), 'none')
		assert.equal(await syncDriftIssue({ api, problems, openIssues: [...OPEN, impostor], repository: 'o/r' }), 'opened #900')
		assert.deepEqual(
			api.writes.map(({ method, path }) => `${method} ${path}`),
			['POST /issues'],
		)
	})
})

describe('driftBody', () => {
	it('lists every problem and links the page in the repository', () => {
		const body = driftBody(['#51 (`New`) is open and has no row.'], 'o/r')

		assert.match(body, /https:\/\/github\.com\/o\/r\/blob\/main\/docs\/issue-traceability\.md/)
		assert.match(body, /^- #51 \(`New`\) is open and has no row\.$/m)
	})
})

describe('openIssues', () => {
	it('drops pull requests from the issue list', async () => {
		const api = fakeApi({
			issues: [
				{ number: 1, title: 'Issue', body: 'b', user: { login: BOT } },
				{ number: 2, title: 'PR', body: '', user: { login: BOT }, pull_request: {} },
			],
		})

		assert.deepEqual(await openIssues(api), [{ number: 1, title: 'Issue', body: 'b', user: BOT, milestone: undefined, labels: [], parent: undefined }])
	})

	it('reads the milestone, label names, and parent number', async () => {
		const api = fakeApi({
			issues: [
				{
					number: 3,
					title: 'Child',
					milestone: { title: 'Phase 2' },
					labels: [{ name: 'bug' }, 'area:ci'],
					parent_issue_url: 'https://api.github.com/repos/o/r/issues/2',
				},
			],
		})

		const [issue] = await openIssues(api)

		assert.deepEqual([issue.milestone, issue.labels, issue.parent], ['Phase 2', ['bug', 'area:ci'], 2])
	})
})

describe('github', () => {
	it('sends the token and pages a list until a short page', async () => {
		const calls: { url: string; init: Parameters<Fetch>[1] }[] = []
		const fetch: Fetch = (url, init) => {
			calls.push({ url, init })
			const page = Number(new URL(url).searchParams.get('page'))
			const items = page === 1 ? Array.from({ length: 100 }, (_, i) => ({ number: i })) : [{ number: 100 }]
			return Promise.resolve({ ok: true, status: 200, json: () => Promise.resolve(items) })
		}

		const items = await github({ token: 't', repository: 'o/r', fetch }).list('/issues?state=open')

		assert.equal(items.length, 101)
		assert.equal(calls[0].url, 'https://api.github.com/repos/o/r/issues?state=open&per_page=100&page=1')
		assert.equal(calls[1].url, 'https://api.github.com/repos/o/r/issues?state=open&per_page=100&page=2')
		assert.equal(calls[0].init.headers.authorization, 'Bearer t')
	})

	it('pages a path with no query of its own', async () => {
		const calls: string[] = []
		const fetch: Fetch = (url) => {
			calls.push(url)
			return Promise.resolve({ ok: true, status: 200, json: () => Promise.resolve([]) })
		}

		await github({ token: 't', repository: 'o/r', fetch }).list('/milestones')

		assert.equal(calls[0], 'https://api.github.com/repos/o/r/milestones?per_page=100&page=1')
	})

	it('sends a write as JSON and returns the answer', async () => {
		let sent: { url: string; init: Parameters<Fetch>[1] } | undefined
		const fetch: Fetch = (url, init) => {
			sent = { url, init }
			return Promise.resolve({ ok: true, status: 200, json: () => Promise.resolve({ number: 7 }) })
		}

		const answer = await github({ token: 't', repository: 'o/r', fetch }).request('PATCH', '/issues/7', { state: 'closed' })

		assert.deepEqual(answer, { number: 7 })
		const written = present(sent, 'the write')
		assert.equal(written.init.method, 'PATCH')
		assert.equal(written.init.body, '{"state":"closed"}')
	})

	it('throws on a failed call', async () => {
		const fetch: Fetch = () => Promise.resolve({ ok: false, status: 403, json: () => Promise.resolve({}) })

		await assert.rejects(github({ token: 't', repository: 'o/r', fetch }).request('GET', '/issues'), { status: 403, message: /answered 403/ })
	})

	it('throws when a page fails partway through a list', async () => {
		const fetch: Fetch = (url) => {
			const page = Number(new URL(url).searchParams.get('page'))
			if (page === 2) return Promise.resolve({ ok: false, status: 502, json: () => Promise.resolve({}) })
			return Promise.resolve({ ok: true, status: 200, json: () => Promise.resolve(Array.from({ length: 100 }, (_, i) => ({ number: i }))) })
		}

		await assert.rejects(github({ token: 't', repository: 'o/r', fetch }).list('/issues'), { status: 502 })
	})
})

describe('main', () => {
	it('skips with a notice when there is no token', async () => {
		const { code, output } = await runMain({ api: null, markdown: PAGE, sync: false })

		assert.equal(code, 0)
		assert.match(output.log.join('\n'), /::notice::No GitHub token/)
	})

	it('passes a page that matches', async () => {
		const { code, output } = await runMain({ api: fakeApi({ issues: OPEN_ITEMS }), markdown: PAGE, sync: false })

		assert.equal(code, 0)
		assert.match(output.log.join('\n'), /is what the generator writes for the open issues/)
	})

	it('warns about each problem and exits 1 when only reporting', async () => {
		const { code, output } = await runMain({ api: fakeApi({ issues: [OPEN_ITEMS[0]] }), markdown: PAGE, sync: false })

		assert.equal(code, 1)
		assert.match(output.log.join('\n'), /::warning file=docs\/issue-traceability\.md::#47 has a row but is not an open issue/)
	})

	it('keeps the drift issue and exits 0 when syncing, so the scheduled run never fails for drift', async () => {
		const api = fakeApi({ issues: [OPEN_ITEMS[0]] })

		const { code, output } = await runMain({ api, markdown: PAGE, sync: true })

		assert.equal(code, 0)
		assert.match(output.log.join('\n'), /opened #900/)
	})

	it('escapes a title in a warning line, so it cannot inject a workflow command', async () => {
		const api = fakeApi({ issues: [...OPEN_ITEMS, { number: 51, title: '100% done\n::error::x' }] })

		const { output } = await runMain({ api, markdown: PAGE, sync: false })

		assert.ok(output.log.includes("::warning file=docs/issue-traceability.md::#51 (`100%25 done%0A::error::x`) is open and has no row."))
	})

	for (const status of [403, 429]) {
		it(`warns and exits 0 when GitHub answers ${status}`, async () => {
			const api: Api = {
				list: async () => Promise.reject(Object.assign(new Error(`GET /issues answered ${String(status)}`), { status })),
				request: async () => Promise.reject(new Error('unexpected request')),
			}

			const { code, output } = await runMain({ api, markdown: PAGE, sync: true })

			assert.equal(code, 0)
			assert.match(output.log.join('\n'), new RegExp(`::warning::GET /issues answered ${status}; issue traceability was not checked`))
		})
	}

	it('fails on any other error', async () => {
		const api: Api = {
			list: async () => Promise.reject(Object.assign(new Error('boom'), { status: 500 })),
			request: async () => Promise.reject(new Error('unexpected request')),
		}

		await assert.rejects(runMain({ api, markdown: PAGE, sync: true }), /boom/)
	})
})

describe('escapeCommand', () => {
	it('escapes percent, carriage return, and line feed', () => {
		assert.equal(escapeCommand('a%b\r\nc'), 'a%25b%0D%0Ac')
	})
})

describe('command line', () => {
	it('skips with a notice and exits 0 when there is no token and no gh login', () => {
		const env: NodeJS.ProcessEnv = { ...process.env, PATH: '' }
		delete env.GITHUB_TOKEN
		delete env.GH_TOKEN

		const result = spawnSync(process.execPath, ['tools/spec/check-issue-traceability.ts'], { cwd: REPO, env, encoding: 'utf8' })

		assert.equal(result.status, 0)
		assert.match(result.stdout, /::notice::No GitHub token/)
	})
})
