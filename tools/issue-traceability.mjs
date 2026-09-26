#!/usr/bin/env node
// docs/issue-traceability.md lists every open issue (ADR-0143).
//
// This reports when an open issue has no row, or a row names an issue that is
// no longer open. It never gates a pull request: checked against live GitHub
// state, a pull request that closes an issue would have to keep that issue's
// row until it merges and would leave it stale the moment it did, and filing
// any issue would fail every open pull request. So a scheduled workflow runs
// it, and it keeps one drift issue current instead: opened when drift appears,
// updated while it lasts, closed when it is gone.
//
//   GITHUB_TOKEN=… GITHUB_REPOSITORY=owner/repo node tools/issue-traceability.mjs          reports drift
//   GITHUB_TOKEN=… GITHUB_REPOSITORY=owner/repo node tools/issue-traceability.mjs --sync   also keeps the drift issue
//
// With no token it reads nothing and exits 0 with a notice. Without --sync the
// exit code says whether there is drift.
import { readFileSync } from 'node:fs'

export const PAGE = 'docs/issue-traceability.md'
export const DRIFT_TITLE = 'Issue traceability drift'
export const DRIFT_LABELS = ['documentation']
export const DRIFT_MILESTONE = 'Docs & spec hygiene'

/** The issue number each table row names in its first cell. */
export function listedIssues(markdown) {
	return [...markdown.matchAll(/^\|\s*\[#(\d+)\b/gm)].map(([, number]) => Number(number))
}

/**
 * What is wrong with the page, or an empty list. `openIssues` is every open
 * issue, pull requests excluded; the drift issue itself needs no row.
 */
export function driftProblems({ openIssues, markdown }) {
	const open = openIssues.filter((issue) => issue.title !== DRIFT_TITLE)
	const openNumbers = new Set(open.map((issue) => issue.number))
	const listed = listedIssues(markdown)
	const listedNumbers = new Set(listed)

	const problems = []
	for (const issue of [...open].sort((a, b) => a.number - b.number)) {
		if (!listedNumbers.has(issue.number)) problems.push(`#${issue.number} (${issue.title}) is open and has no row.`)
	}
	for (const number of listed) {
		if (!openNumbers.has(number)) problems.push(`#${number} has a row but is not an open issue.`)
	}
	return problems
}

/** The drift issue's body. */
export function driftBody(problems, repository) {
	return [
		`[\`${PAGE}\`](https://github.com/${repository}/blob/main/${PAGE}) lists every open issue, and it has drifted:`,
		'',
		...problems.map((problem) => `- ${problem}`),
		'',
		'Add a row for each open issue, saying how it stands against the specification, and remove the row of each closed one.',
		'`.github/workflows/issue-traceability.yml` keeps this issue current and closes it once the page matches.',
	].join('\n')
}

/**
 * Opens, updates, or closes the one drift issue so it matches `problems`.
 * Returns what it did.
 */
export async function syncDriftIssue({ api, problems, openIssues, repository }) {
	const existing = openIssues.find((issue) => issue.title === DRIFT_TITLE)

	if (problems.length === 0) {
		if (!existing) return 'none'
		await api.request('POST', `/issues/${existing.number}/comments`, { body: `\`${PAGE}\` matches the open issues again.` })
		await api.request('PATCH', `/issues/${existing.number}`, { state: 'closed', state_reason: 'completed' })
		return `closed #${existing.number}`
	}

	const body = driftBody(problems, repository)
	if (existing) {
		if (existing.body !== body) await api.request('PATCH', `/issues/${existing.number}`, { body })
		return `updated #${existing.number}`
	}

	const milestones = await api.list('/milestones?state=open')
	const milestone = milestones.find((candidate) => candidate.title === DRIFT_MILESTONE)
	const created = await api.request('POST', '/issues', {
		title: DRIFT_TITLE,
		body,
		labels: DRIFT_LABELS,
		...(milestone ? { milestone: milestone.number } : {}),
	})
	return `opened #${created.number}`
}

/** Every open issue, pull requests excluded. */
export async function openIssues(api) {
	const items = await api.list('/issues?state=open')
	return items.filter((item) => !item.pull_request).map(({ number, title, body }) => ({ number, title, body }))
}

/** A minimal GitHub REST client for one repository. */
export function github({ token, repository, fetch = globalThis.fetch }) {
	const base = `https://api.github.com/repos/${repository}`
	const headers = { authorization: `Bearer ${token}`, accept: 'application/vnd.github+json', 'x-github-api-version': '2022-11-28' }

	async function call(method, url, body) {
		const response = await fetch(url, { method, headers, body: body === undefined ? undefined : JSON.stringify(body) })
		if (!response.ok) throw new Error(`${method} ${url} answered ${response.status}`)
		return response
	}

	return {
		async request(method, path, body) {
			return (await call(method, `${base}${path}`, body)).json()
		},
		async list(path) {
			const items = []
			for (let page = 1; ; page++) {
				const separator = path.includes('?') ? '&' : '?'
				const batch = await (await call('GET', `${base}${path}${separator}per_page=100&page=${page}`)).json()
				items.push(...batch)
				if (batch.length < 100) return items
			}
		},
	}
}

/**
 * Reports the way the command line does, without exiting. `api` is null when
 * no token is available. Returns the exit code.
 */
export async function main({ api, markdown, sync, repository }) {
	if (!api) {
		console.log('::notice::No GitHub token, so the open issues were not read and issue traceability was not checked.')
		return 0
	}

	const issues = await openIssues(api)
	const problems = driftProblems({ openIssues: issues, markdown })

	if (problems.length === 0) console.log(`::notice::${PAGE} lists every open issue and nothing else.`)
	for (const problem of problems) console.log(`::warning file=${PAGE}::${problem}`)

	if (sync) {
		console.log(await syncDriftIssue({ api, problems, openIssues: issues, repository }))
		return 0
	}
	return problems.length === 0 ? 0 : 1
}

const runAsCommand = String(process.argv[1]).endsWith('issue-traceability.mjs')
if (runAsCommand) {
	const token = process.env.GITHUB_TOKEN || process.env.GH_TOKEN
	const repository = process.env.GITHUB_REPOSITORY || 'HPAC-Safety/safety-report'
	const api = token ? github({ token, repository }) : null
	process.exitCode = await main({ api, repository, markdown: readFileSync(PAGE, 'utf8'), sync: process.argv.includes('--sync') })
}
