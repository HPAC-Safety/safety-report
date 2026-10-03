#!/usr/bin/env node
// docs/issue-traceability.md, generated from GitHub (ADR-0191).
//
// The page lists every open issue with its milestone, labels, and parent, read
// from GitHub's REST API. It is never written by hand: a hand-kept page drifted
// for a month before ADR-0143, and its prose disposition column restated what
// the issues and the specification already say. The page and each row are
// rendered by tools/spec/check-issue-traceability.ts, which the scheduled
// workflow runs to keep one drift issue open while the committed page and
// GitHub differ; this script only fetches and writes.
//
//   node tools/spec/generate-issue-traceability.ts   write docs/issue-traceability.md
//
// The token comes from GITHUB_TOKEN, then GH_TOKEN, then `gh auth token`.
// With none it writes nothing and exits 2. The repository comes from
// GITHUB_REPOSITORY, defaulting to this one.
import { writeFileSync } from 'node:fs'

import { type Env, type Exec, exec as realExec, isMain } from '../lib/actions.ts'
import { type Api, PAGE, findToken, github, openIssues, renderPage } from './check-issue-traceability.ts'

export const DEFAULT_REPOSITORY = 'HPAC-Safety/safety-report'

/** Reads the open issues and returns the page the generator writes. */
export async function generate({ api, repository }: { api: Api; repository: string }): Promise<string> {
	return renderPage(await openIssues(api), repository)
}

/** Writes the page. Returns the exit code. */
export async function main({
	env = process.env,
	exec = realExec,
	write = (path: string, text: string) => {
		writeFileSync(path, text)
	},
	makeApi = github,
}: {
	env?: Env
	exec?: Exec
	write?: (path: string, text: string) => void
	makeApi?: typeof github
} = {}): Promise<number> {
	const token = findToken({ env, exec })
	if (!token) {
		console.error('No GitHub token: set GITHUB_TOKEN or GH_TOKEN, or run `gh auth login`.')
		return 2
	}
	const repository = env.GITHUB_REPOSITORY || DEFAULT_REPOSITORY
	write(PAGE, await generate({ api: makeApi({ token, repository }), repository }))
	console.log(`Wrote ${PAGE}.`)
	return 0
}

if (isMain(import.meta.url)) process.exitCode = await main()
