#!/usr/bin/env node
/**
 * Finds the coverage ratchet's baseline run (#589): the newest successful
 * `push` run of a workflow on `main` that carries a non-expired
 * `coverage-report` artifact.
 *
 * The `coverage` job is skipped whenever a change touches no .NET or e2e code
 * (its `changes` filter), so main's most recent green run is not always a run
 * that ran coverage — a docs-, infra-, or workflow-only merge leaves main
 * green with no artifact. Taking only the newest run then means the ratchet
 * never runs, even though an earlier run has a perfectly good baseline. This
 * walks back until it finds one, or gives up after `--limit` runs and leaves
 * the ratchet skipped, exactly as when main has no green run at all.
 *
 * `ci.yml`'s "Fetch the main baseline" step and `tools/ci-local.sh` both call
 * this, so they can never pick a different run (ADR-0147, ADR-0165). Only a
 * `push` run on `main` ever qualifies: `gh run list --branch main --event
 * push --status success` already filters server side, but each candidate's
 * event, branch, and conclusion are checked again here rather than trusted,
 * because a merge-group run (branch `gh-readonly-queue/main/*`) or a fork
 * pull request whose branch happens to be named `main` must never qualify.
 *
 * Usage:
 *   node tools/find-coverage-baseline.mjs --repo <owner/repo>
 *                                          [--workflow CI] [--limit 20]
 *
 * Prints the chosen run's database id to stdout, or the literal `none` when
 * no candidate within `--limit` runs has a usable artifact. Diagnostics go to
 * stderr as `::notice::` lines - safe to ignore outside GitHub Actions, and
 * kept off stdout so a caller can read the run id (or `none`) with no
 * parsing beyond a trim.
 */

import { execFileSync } from 'node:child_process'

const args = new Map()
for (let i = 2; i < process.argv.length; i += 2) {
	args.set(process.argv[i].replace(/^--/, ''), process.argv[i + 1])
}

/** True when this file was run as a command rather than imported by a test. */
const runAsCommand = process.argv[1]?.endsWith('find-coverage-baseline.mjs') ?? false

const repo = args.get('repo')
const workflow = args.get('workflow') ?? 'CI'
// How far back to walk before giving up. Generous enough to cross several
// docs-only merges in a row, small enough that a main gone truly quiet still
// fails fast rather than paging through its entire run history.
const limit = Number(args.get('limit') ?? 20)

if (runAsCommand && !repo) {
	console.error('::error::--repo is required')
	process.exit(2)
}

/**
 * A run counts only when it really is a successful push run on main - checked
 * again here rather than trusted from `gh run list`'s own filters, because
 * this run id gates every pull request's coverage ratchet (ADR-0147).
 */
export function isEligible(run) {
	return run.event === 'push' && run.headBranch === 'main' && run.conclusion === 'success'
}

/**
 * The first eligible run, in the order `gh run list` already returns them
 * (newest first), that `hasArtifact` reports true for - or `null` when none
 * of `runs` does. `hasArtifact` is injected so this selection logic is
 * testable without shelling out to `gh`.
 */
export function selectBaselineRun(runs, hasArtifact) {
	for (const run of runs) {
		if (!isEligible(run)) continue
		if (hasArtifact(run.databaseId)) return run.databaseId
	}
	return null
}

function listRuns() {
	const json = execFileSync(
		'gh',
		[
			'run', 'list',
			'--repo', repo,
			'--workflow', workflow,
			'--branch', 'main',
			'--event', 'push',
			'--status', 'success',
			'--limit', String(limit),
			'--json', 'databaseId,event,headBranch,conclusion',
		],
		{ encoding: 'utf8' },
	)
	return JSON.parse(json)
}

function hasNonExpiredArtifact(runId) {
	const count = execFileSync(
		'gh',
		[
			'api', `repos/${repo}/actions/runs/${runId}/artifacts`,
			'--jq', '[.artifacts[] | select(.name == "coverage-report" and (.expired | not))] | length',
		],
		{ encoding: 'utf8' },
	).trim()
	return Number(count) > 0
}

function main() {
	const runs = listRuns()
	const runId = selectBaselineRun(runs, hasNonExpiredArtifact)

	if (runId === null) {
		console.error(
			`::notice::Walked back ${Math.min(runs.length, limit)} successful push run(s) of ${workflow} on main; ` +
				'none carries a non-expired coverage-report artifact. The ratchet will not run; the floor still applies.',
		)
		console.log('none')
		return
	}

	console.error(`::notice::Baseline from main run ${runId}.`)
	console.log(String(runId))
}

if (runAsCommand) {
	main()
}
