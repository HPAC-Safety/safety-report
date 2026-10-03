#!/usr/bin/env node
// Finds the green Release run for the tag promote.yml was dispatched on, and
// checks its artifacts are still there (issue #609, ADR-0166, CON-INF-012).
//
// It refuses, before any AWS call:
//   - a run not dispatched on a tag, or on one that is not YYYY.MM.DD-N
//     (ADR-0158);
//   - a tag with no successful Release run for its commit (never green on
//     staging);
//   - a run whose artifacts have expired (release.yml keeps them 90 days).
//
// The build job tagged every image with the commit the tag points to, and
// release.yml tags the very commit its run is on, so the run to promote is the
// one whose head_sha is this commit. Newest first; a success means its staging
// job was green.
//
// Outputs: tag, sha, run_id. Also writes the run summary.
//
// Environment: GH_TOKEN, REPO, REF_TYPE, TAG; GITHUB_SERVER_URL from Actions.
import { appendSummary, exec, isMain, required, setOutput, type Env, type Exec } from '../lib/actions.ts'

export const TAG_PATTERN = /^[0-9]{4}\.(0[1-9]|1[0-2])\.(0[1-9]|[12][0-9]|3[01])-[1-9][0-9]*$/
export const REQUIRED_ARTIFACTS = ['api-image', 'worker-image', 'web-dist']

/** The first required artifact the live list lacks, or null. */
export function missingArtifact(live: readonly string[]): string | null {
	return REQUIRED_ARTIFACTS.find((name) => !live.includes(name)) ?? null
}

export function main({ env = process.env, exec: run = exec, log = console.log }: { env?: Env; exec?: Exec; log?: (line: string) => void } = {}): number {
	const repo = required(env, 'REPO')
	const refType = required(env, 'REF_TYPE')
	const tag = required(env, 'TAG')
	const fail = (message: string): number => {
		log(`::error::${message}`)
		return 1
	}

	if (refType !== 'tag') {
		return fail(`Run this on a release tag, not the '${tag}' branch: Run workflow → Use workflow from → Tags → the tag (or: gh workflow run promote.yml --ref <tag>).`)
	}
	if (!TAG_PATTERN.test(tag)) return fail(`Tag '${tag}' does not match YYYY.MM.DD-N (e.g. 2026.10.02-1). See ADR-0158.`)

	const gh = (...args: string[]): string => run('gh', ['api', ...args], { check: true }).stdout
	const sha = gh(`repos/${repo}/commits/${tag}`, '--jq', '.sha')
	const runId = gh(`repos/${repo}/actions/workflows/release.yml/runs?head_sha=${sha}&status=success&per_page=1`, '--jq', '.workflow_runs[0].id // empty')
	if (!runId) {
		return fail(`No successful Release run for ${tag} (commit ${sha}). Only a release already green on staging is promoted: wait for its staging job to pass.`)
	}

	const live = gh('--paginate', `repos/${repo}/actions/runs/${runId}/artifacts`, '--jq', '.artifacts[] | select(.expired == false) | .name')
	const missing = missingArtifact(live.split('\n'))
	if (missing) {
		return fail(`Release run ${runId} for ${tag} no longer has its '${missing}' artifact (release.yml keeps them 90 days). Run Release to cut a new release from main, then promote that one.`)
	}

	setOutput('tag', tag, env)
	setOutput('sha', sha, env)
	setOutput('run_id', runId, env)
	appendSummary(`### Promoting ${tag}\n- Release run: ${env.GITHUB_SERVER_URL ?? ''}/${repo}/actions/runs/${runId}\n- Commit: \`${sha}\``, env)
	return 0
}

if (isMain(import.meta.url)) process.exit(main())
