#!/usr/bin/env node
// A scenario may lead its implementation, tagged `@ignore`, only while an open
// issue owns building it (CONV-001).
//
// Every `@ignore` claim in .spec/claims.json carries exactly one `@issue-<N>`
// tag on its scenario's tag lines, and issue N is open:
//
//     @REQ-MED-061 @ignore @issue-842
//     Scenario: A video longer than ten minutes is refused
//
// A pull request (or queued commit) whose body closes an issue that an
// `@ignore` claim still names fails too: closing the issue would leave the
// claim with no owner, so the pull request that builds it removes `@ignore`,
// or the claim is moved to the issue that will.
//
//   node tools/spec/check-ignored-claims.ts
//
// Environment: EVENT_NAME, PR_BODY, BASE_SHA (the merge group's base, to read
// its queued commits' messages), GITHUB_REPOSITORY, and GH_TOKEN or
// GITHUB_TOKEN, optional: a public repository answers anonymously, within its
// rate limit of 60 calls an hour, spent only when an @ignore claim exists.
// tools/dev/ci-local.sh gives act no token (ADR-0145), so a local run is
// anonymous.
//
// The exit code is the contract.
import { readFileSync } from 'node:fs'

import { type Env, type Exec, annotation, errorMessage, exec as realExec, isMain } from '../lib/actions.ts'
import { CLAIMS } from './spec-paths.ts'

const ISSUE_TAG = /^@issue-(\d+)$/
// A closing keyword GitHub honors, not negated ("does not close #5"), naming
// an issue as `#N`, `owner/repo#N`, or its URL.
const CLOSES =
	/(?<!\b(?:not|never|n't|won't|don't|doesn't)\s+)\b(?:close[sd]?|fix(?:e[sd])?|resolve[sd]?)\s*:?\s+(?:https:\/\/github\.com\/([\w.-]+\/[\w.-]+)\/issues\/|([\w.-]+\/[\w.-]+)#|#)(\d+)\b/gi

/** What this tool reads of .spec/claims.json. */
interface IgnoredInput {
	id: string
	file: string
	status: 'Built' | 'Planned'
	tags: string[]
}

/** A planned claim and the issue it names, or why it names none. */
export interface Ignored {
	id: string
	file: string
	issue: number | null
	problem?: string
}

/** Every `@ignore` claim and the issue its `@issue-<N>` tag names. */
export function ignoredClaims(claimsJson: string): Ignored[] {
	const { claims } = JSON.parse(claimsJson) as { claims: IgnoredInput[] }
	return claims
		.filter((claim) => claim.status === 'Planned')
		.map((claim): Ignored => {
			const issues = claim.tags.map((tag) => ISSUE_TAG.exec(tag)?.[1]).filter((issue): issue is string => issue !== undefined)
			if (issues.length === 1) return { id: claim.id, file: claim.file, issue: Number(issues[0]) }
			const problem =
				issues.length === 0
					? `${claim.id} is @ignore and names no issue. Tag it @issue-<N> with the open issue that will build it.`
					: `${claim.id} is @ignore and names ${issues.length} issues (${issues.map((issue) => `#${issue}`).join(', ')}); one issue owns building a claim.`
			return { id: claim.id, file: claim.file, issue: null, problem }
		})
}

/** The issues of `repository` a pull-request body or commit message closes. */
export function closedBy(text: string, repository: string): Set<number> {
	const ours = (named: string | undefined): boolean => named === undefined || named.toLowerCase() === repository.toLowerCase()
	// An unmatched group is undefined at run time, whatever the type says.
	const named = (match: RegExpExecArray): string | undefined => [match[1], match[2]].find((group: string | undefined) => group !== undefined)
	return new Set([...text.matchAll(CLOSES)].filter((match) => ours(named(match))).map((match) => Number(match[3])))
}

/** What GitHub says of one issue number. */
export type IssueState = { state: 'open' | 'closed'; pullRequest: boolean } | { error: string }

export type FetchIssue = (issue: number) => Promise<IssueState>

/** Reads an issue from the REST API, with the token when there is one. */
export function githubIssues(repository: string, token: string | undefined): FetchIssue {
	return async (issue) => {
		let response: Response
		try {
			response = await fetch(`https://api.github.com/repos/${repository}/issues/${issue}`, {
				headers: { accept: 'application/vnd.github+json', ...(token ? { authorization: `Bearer ${token}` } : {}) },
			})
		} catch (error) {
			return { error: `GitHub could not be reached for #${issue} (${errorMessage(error)})` }
		}
		if (!response.ok) return { error: `GitHub answered ${response.status} for #${issue}` }
		const body = (await response.json()) as { state: 'open' | 'closed'; pull_request?: unknown }
		return { state: body.state, pullRequest: body.pull_request !== undefined }
	}
}

export interface MainOptions {
	env?: Env
	claims?: string
	exec?: Exec
	fetchIssue?: FetchIssue
	log?: (line: string) => void
}

export async function main({ env = process.env, claims = readFileSync(CLAIMS, 'utf8'), exec = realExec, fetchIssue, log = console.log }: MainOptions = {}): Promise<number> {
	const ignored = ignoredClaims(claims)
	const problems: { file?: string; message: string }[] = ignored.flatMap((claim) => (claim.problem ? [{ file: claim.file, message: claim.problem }] : []))

	const named = [...new Set(ignored.map((claim) => claim.issue).filter((issue): issue is number => issue !== null))].sort((a, b) => a - b)
	const repository = env.GITHUB_REPOSITORY ?? 'HPAC-Safety/safety-report'
	const fetchOne = fetchIssue ?? githubIssues(repository, env.GH_TOKEN || env.GITHUB_TOKEN || undefined)
	for (const issue of named) {
		const state = await fetchOne(issue)
		const owners = ignored.filter((claim) => claim.issue === issue)
		const ids = owners.map((claim) => claim.id).join(', ')
		if ('error' in state) problems.push({ message: `Could not read #${issue}, named by ${ids}: ${state.error}.` })
		else if (state.pullRequest) problems.push({ file: owners[0]?.file, message: `${ids} name #${issue}, a pull request; @issue-<N> names the issue that will build the claim.` })
		else if (state.state !== 'open') problems.push({ file: owners[0]?.file, message: `${ids} name #${issue}, which is closed. Build the claim and remove @ignore, or move it to the open issue that will.` })
	}

	// What this change closes: the pull request's body, or every queued
	// commit's message, which is its pull request's body (ADR-0147).
	const texts =
		env.EVENT_NAME === 'merge_group' && env.BASE_SHA
			? [exec('git', ['log', '--format=%B', `${env.BASE_SHA}..HEAD`]).stdout]
			: [env.PR_BODY ?? '']
	for (const issue of closedBy(texts.join('\n'), repository)) {
		const owners = ignored.filter((claim) => claim.issue === issue)
		if (owners.length === 0) continue
		problems.push({
			file: owners[0]?.file,
			message: `This closes #${issue}, which ${owners.map((claim) => claim.id).join(', ')} still name as @ignore. Build them and remove @ignore here, or move them to the open issue that will.`,
		})
	}

	for (const problem of problems) log(annotation('error', problem.message, { file: problem.file }))
	if (problems.length > 0) return 1
	log(`${ignored.length} @ignore claim(s), each owned by an open issue${named.length > 0 ? `: ${named.map((issue) => `#${issue}`).join(', ')}` : ''}.`)
	return 0
}

if (isMain(import.meta.url)) process.exit(await main())
