import { describe, it } from 'node:test'
import assert from 'node:assert/strict'

import {
	DRIFT_MILESTONE,
	DRIFT_TITLE,
	driftBody,
	driftProblems,
	github,
	listedIssues,
	main,
	openIssues,
	syncDriftIssue,
} from '../../tools/issue-traceability.mjs'

const PAGE = [
	'| Issue | Area | Disposition |',
	'|---|---|---|',
	'| [#30 — Deploy](https://github.com/o/r/issues/30) | Infrastructure | Open. |',
	'| [#47 — Dependency Dashboard](https://github.com/o/r/issues/47) | — | Standing. |',
	'',
	'See [#99](https://github.com/o/r/issues/99) in prose.',
].join('\n')

const OPEN = [
	{ number: 30, title: 'Deploy' },
	{ number: 47, title: 'Dependency Dashboard' },
]

/** A fake GitHub client that records every write and serves fixed lists. */
function fakeApi({ issues = [], milestones = [] } = {}) {
	const writes = []
	return {
		writes,
		async list(path) {
			if (path.startsWith('/issues')) return issues
			if (path.startsWith('/milestones')) return milestones
			throw new Error(`unexpected list ${path}`)
		},
		async request(method, path, body) {
			writes.push({ method, path, body })
			return { number: 900 }
		},
	}
}

/** Runs `main` with console output captured, restoring it afterwards even on failure. */
async function runMain(input) {
	const output = { log: [] }
	const original = console.log
	console.log = (...args) => output.log.push(args.join(' '))
	try {
		return { code: await main({ repository: 'o/r', ...input }), output }
	} finally {
		console.log = original
	}
}

describe('listedIssues', () => {
	it('reads the issue each table row names, and ignores prose', () => {
		assert.deepEqual(listedIssues(PAGE), [30, 47])
	})
})

describe('driftProblems', () => {
	it('finds nothing when the page lists exactly the open issues', () => {
		assert.deepEqual(driftProblems({ openIssues: OPEN, markdown: PAGE }), [])
	})

	it('names an open issue with no row, and a row whose issue is not open', () => {
		const problems = driftProblems({ openIssues: [OPEN[0], { number: 51, title: 'New' }], markdown: PAGE })

		assert.deepEqual(problems, ['#51 (New) is open and has no row.', '#47 has a row but is not an open issue.'])
	})

	it('needs no row for the drift issue itself', () => {
		assert.deepEqual(driftProblems({ openIssues: [...OPEN, { number: 60, title: DRIFT_TITLE }], markdown: PAGE }), [])
	})
})

describe('syncDriftIssue', () => {
	const problems = ['#51 (New) is open and has no row.']

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
			{ method: 'POST', path: '/issues', body: { title: DRIFT_TITLE, body: driftBody(problems, 'o/r'), labels: ['documentation'], milestone: 4 } },
		])
	})

	it('opens it without a milestone when the milestone is gone', async () => {
		const api = fakeApi()

		await syncDriftIssue({ api, problems, openIssues: OPEN, repository: 'o/r' })

		assert.equal(api.writes[0].body.milestone, undefined)
	})

	it('updates the open drift issue only when its body changed', async () => {
		const stale = { number: 60, title: DRIFT_TITLE, body: 'old' }
		const current = { ...stale, body: driftBody(problems, 'o/r') }
		const api = fakeApi()

		assert.equal(await syncDriftIssue({ api, problems, openIssues: [...OPEN, stale], repository: 'o/r' }), 'updated #60')
		assert.equal(await syncDriftIssue({ api, problems, openIssues: [...OPEN, current], repository: 'o/r' }), 'updated #60')
		assert.deepEqual(api.writes, [{ method: 'PATCH', path: '/issues/60', body: { body: driftBody(problems, 'o/r') } }])
	})

	it('comments on and closes the drift issue once the page matches', async () => {
		const api = fakeApi()

		const done = await syncDriftIssue({ api, problems: [], openIssues: [...OPEN, { number: 60, title: DRIFT_TITLE }], repository: 'o/r' })

		assert.equal(done, 'closed #60')
		assert.deepEqual(
			api.writes.map(({ method, path }) => `${method} ${path}`),
			['POST /issues/60/comments', 'PATCH /issues/60'],
		)
		assert.equal(api.writes[1].body.state, 'closed')
	})
})

describe('driftBody', () => {
	it('lists every problem and links the page in the repository', () => {
		const body = driftBody(['#51 (New) is open and has no row.'], 'o/r')

		assert.match(body, /https:\/\/github\.com\/o\/r\/blob\/main\/docs\/issue-traceability\.md/)
		assert.match(body, /^- #51 \(New\) is open and has no row\.$/m)
	})
})

describe('openIssues', () => {
	it('drops pull requests from the issue list', async () => {
		const api = fakeApi({ issues: [{ number: 1, title: 'Issue', body: 'b' }, { number: 2, title: 'PR', body: '', pull_request: {} }] })

		assert.deepEqual(await openIssues(api), [{ number: 1, title: 'Issue', body: 'b' }])
	})
})

describe('github', () => {
	it('sends the token and pages a list until a short page', async () => {
		const calls = []
		const fetch = async (url, init) => {
			calls.push({ url, init })
			const page = Number(new URL(url).searchParams.get('page'))
			const items = page === 1 ? Array.from({ length: 100 }, (_, i) => ({ number: i })) : [{ number: 100 }]
			return { ok: true, json: async () => items }
		}

		const items = await github({ token: 't', repository: 'o/r', fetch }).list('/issues?state=open')

		assert.equal(items.length, 101)
		assert.equal(calls[0].url, 'https://api.github.com/repos/o/r/issues?state=open&per_page=100&page=1')
		assert.equal(calls[1].url, 'https://api.github.com/repos/o/r/issues?state=open&per_page=100&page=2')
		assert.equal(calls[0].init.headers.authorization, 'Bearer t')
	})

	it('pages a path with no query of its own', async () => {
		const calls = []
		const fetch = async (url) => {
			calls.push(url)
			return { ok: true, json: async () => [] }
		}

		await github({ token: 't', repository: 'o/r', fetch }).list('/milestones')

		assert.equal(calls[0], 'https://api.github.com/repos/o/r/milestones?per_page=100&page=1')
	})

	it('sends a write as JSON and returns the answer', async () => {
		let sent
		const fetch = async (url, init) => {
			sent = { url, init }
			return { ok: true, json: async () => ({ number: 7 }) }
		}

		const answer = await github({ token: 't', repository: 'o/r', fetch }).request('PATCH', '/issues/7', { state: 'closed' })

		assert.deepEqual(answer, { number: 7 })
		assert.equal(sent.init.method, 'PATCH')
		assert.equal(sent.init.body, '{"state":"closed"}')
	})

	it('throws on a failed call', async () => {
		const fetch = async () => ({ ok: false, status: 403 })

		await assert.rejects(github({ token: 't', repository: 'o/r', fetch }).request('GET', '/issues'), /answered 403/)
	})
})

describe('main', () => {
	it('skips with a notice when there is no token', async () => {
		const { code, output } = await runMain({ api: null, markdown: PAGE, sync: false })

		assert.equal(code, 0)
		assert.match(output.log.join('\n'), /::notice::No GitHub token/)
	})

	it('passes a page that matches', async () => {
		const { code, output } = await runMain({ api: fakeApi({ issues: OPEN }), markdown: PAGE, sync: false })

		assert.equal(code, 0)
		assert.match(output.log.join('\n'), /lists every open issue and nothing else/)
	})

	it('warns about each problem and exits 1 when only reporting', async () => {
		const { code, output } = await runMain({ api: fakeApi({ issues: [OPEN[0]] }), markdown: PAGE, sync: false })

		assert.equal(code, 1)
		assert.match(output.log.join('\n'), /::warning file=docs\/issue-traceability\.md::#47 has a row but is not an open issue/)
	})

	it('keeps the drift issue and exits 0 when syncing, so the scheduled run never fails for drift', async () => {
		const api = fakeApi({ issues: [OPEN[0]] })

		const { code, output } = await runMain({ api, markdown: PAGE, sync: true })

		assert.equal(code, 0)
		assert.match(output.log.join('\n'), /opened #900/)
	})
})
