#!/usr/bin/env node
// docs/issue-traceability.md lists every open issue (ADR-0143).
//
// This reports when an open issue has no row, or a row names an issue that is
// no longer open. It never gates a pull request: checked against live GitHub
// state, a pull request that closes an issue would have to keep that issue's
// row until it merges and would leave it stale the moment it did, and filing
// any issue would fail every open pull request. So a scheduled workflow runs
// it, and it keeps one drift issue current instead: opened (or the last one
// reopened) when drift appears, updated while it lasts, closed when it is gone.
// The drift issue is the one this workflow's bot wrote under DRIFT_TITLE; an
// issue anyone else files under that title is an ordinary issue.
//
//   GITHUB_TOKEN=… GITHUB_REPOSITORY=owner/repo node tools/spec/check-issue-traceability.ts          reports drift
//   GITHUB_TOKEN=… GITHUB_REPOSITORY=owner/repo node tools/spec/check-issue-traceability.ts --sync   also keeps the drift issue
//
// With no token it reads nothing and exits 0 with a notice. Without --sync the
// exit code says whether there is drift.
import { readFileSync } from 'node:fs'
import { isMain } from '../lib/actions.ts'

export const PAGE = 'docs/issue-traceability.md'
export const DRIFT_TITLE = 'Issue traceability drift'
export const DRIFT_LABELS = ['documentation', 'area:ci']
export const BOT = 'github-actions[bot]'

/** An open issue, as this check reads it. */
export interface Issue {
	number: number
	title: string
	body?: string | null | undefined
	user?: string | undefined
}

/** What GitHub's REST API sends for an issue or a milestone, of which this reads a few fields. */
export interface Item {
	number: number
	title: string
	body?: string | null | undefined
	user?: { login: string } | null | undefined
	pull_request?: object | undefined
}

/** What `github` hands its `fetch`. */
export interface FetchInit {
	method: string
	headers: Record<string, string>
	body: string | undefined
}

/** The part of a `Response` this reads. */
export interface FetchResponse {
	ok: boolean
	status: number
	json: () => Promise<unknown>
}

/** The shape of `fetch`, so a test can stand in for it. */
export type Fetch = (url: string, init: FetchInit) => Promise<FetchResponse>

/** The GitHub client `main` and `syncDriftIssue` use; a test passes a stand-in. */
export interface Api {
	request: (method: string, path: string, body?: unknown) => Promise<{ number: number }>
	list: (path: string) => Promise<Item[]>
}

/** Whether an issue is the drift issue this workflow keeps: its title, written by its bot. */
export const isDriftIssue = (issue: Issue): boolean => issue.title === DRIFT_TITLE && issue.user === BOT

// A title goes into a warning line and an issue body. In backticks it cannot
// mention anyone or link anything.
const quoted = (title: string): string => `\`${title.replaceAll('`', "'")}\``

/** Text made safe for one GitHub Actions workflow-command line. */
export const escapeCommand = (text: string): string => text.replaceAll('%', '%25').replaceAll('\r', '%0D').replaceAll('\n', '%0A')
export const DRIFT_MILESTONE = 'Docs & spec hygiene'

/** The issue number each table row names in its first cell. */
export function listedIssues(markdown: string): number[] {
	return [...markdown.matchAll(/^\|\s*\[#(\d+)\b/gm)].map(([, number]) => Number(number))
}

/**
 * What is wrong with the page, or an empty list. `openIssues` is every open
 * issue, pull requests excluded; the drift issue itself needs no row.
 */
export function driftProblems({ openIssues, markdown }: { openIssues: readonly Issue[]; markdown: string }): string[] {
	const open = openIssues.filter((issue) => !isDriftIssue(issue))
	const openNumbers = new Set(open.map((issue) => issue.number))
	const listed = listedIssues(markdown)
	const listedNumbers = new Set(listed)

	const problems: string[] = []
	for (const issue of [...open].sort((a, b) => a.number - b.number)) {
		if (!listedNumbers.has(issue.number)) problems.push(`#${issue.number} (${quoted(issue.title)}) is open and has no row.`)
	}
	for (const number of listed) {
		if (!openNumbers.has(number)) problems.push(`#${number} has a row but is not an open issue.`)
	}
	return problems
}

/** The drift issue's body. */
export function driftBody(problems: readonly string[], repository: string): string {
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
 * Opens, reopens, updates, or closes the one drift issue so it matches
 * `problems`. Returns what it did.
 */
export async function syncDriftIssue({ api, problems, openIssues, repository }: { api: Api; problems: readonly string[]; openIssues: readonly Issue[]; repository: string }): Promise<string> {
	const existing = openIssues.find(isDriftIssue)

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

	const closed = (await api.list(`/issues?state=closed&creator=${encodeURIComponent(BOT)}`))
		.map(summary)
		.filter(isDriftIssue)
		.sort((a, b) => b.number - a.number)
	const latest = closed.at(0)
	if (latest) {
		await api.request('PATCH', `/issues/${latest.number}`, { state: 'open', body })
		return `reopened #${latest.number}`
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

const summary = ({ number, title, body, user }: Item): Issue => ({ number, title, body, user: user?.login })

/** Every open issue, pull requests excluded. */
export async function openIssues(api: Api): Promise<Issue[]> {
	const items = await api.list('/issues?state=open')
	return items.filter((item) => !item.pull_request).map(summary)
}

/** A minimal GitHub REST client for one repository. */
export function github({ token, repository, fetch = globalThis.fetch }: { token: string; repository: string; fetch?: Fetch }): Api {
	const base = `https://api.github.com/repos/${repository}`
	const headers = { authorization: `Bearer ${token}`, accept: 'application/vnd.github+json', 'x-github-api-version': '2022-11-28' }

	async function call(method: string, url: string, body?: unknown): Promise<FetchResponse> {
		const response = await fetch(url, { method, headers, body: body === undefined ? undefined : JSON.stringify(body) })
		if (!response.ok) throw Object.assign(new Error(`${method} ${url} answered ${response.status}`), { status: response.status })
		return response
	}

	return {
		async request(method, path, body) {
			return (await (await call(method, `${base}${path}`, body)).json()) as { number: number }
		},
		async list(path) {
			const items: Item[] = []
			for (let page = 1; ; page++) {
				const separator = path.includes('?') ? '&' : '?'
				const batch = (await (await call('GET', `${base}${path}${separator}per_page=100&page=${page}`)).json()) as Item[]
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
export async function main({ api, markdown, sync, repository }: { api: Api | null; markdown: string; sync: boolean; repository: string }): Promise<number> {
	if (!api) {
		console.log('::notice::No GitHub token, so the open issues were not read and issue traceability was not checked.')
		return 0
	}

	try {
		const issues = await openIssues(api)
		const problems = driftProblems({ openIssues: issues, markdown })

		if (problems.length === 0) console.log(`::notice::${PAGE} lists every open issue and nothing else.`)
		for (const problem of problems) console.log(`::warning file=${PAGE}::${escapeCommand(problem)}`)

		if (sync) {
			console.log(await syncDriftIssue({ api, problems, openIssues: issues, repository }))
			return 0
		}
		return problems.length === 0 ? 0 : 1
	} catch (error) {
		// Refused or rate-limited: GitHub's state, not the page's. Try again next run.
		const status = error instanceof Error ? (error as Error & { status?: number }).status : undefined
		if (!(error instanceof Error) || (status !== 403 && status !== 429)) throw error
		console.log(`::warning::${escapeCommand(error.message)}; issue traceability was not checked.`)
		return 0
	}
}

const runAsCommand = isMain(import.meta.url)
if (runAsCommand) {
	const token = process.env.GITHUB_TOKEN || process.env.GH_TOKEN
	const repository = process.env.GITHUB_REPOSITORY || 'HPAC-Safety/safety-report'
	const api = token ? github({ token, repository }) : null
	process.exitCode = await main({ api, repository, markdown: readFileSync(PAGE, 'utf8'), sync: process.argv.includes('--sync') })
}
