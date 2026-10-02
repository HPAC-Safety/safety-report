#!/usr/bin/env node
// Downloads the main baseline the coverage ratchet compares against and
// outputs where it is (ci.yml `coverage`, "Fetch the main baseline").
//
// The ratchet compares against main's last green run rather than recomputing
// it, which would re-run main's Testcontainers suite for a number that is
// already known (ADR-0014). A merge group is ratcheted too: it is the tree
// that will become main. The baseline is only ever a push run on main - never
// a merge-group run (branch gh-readonly-queue/main/*) or a pull request run
// (ADR-0147). main's newest green run does not always carry the artifact: the
// coverage job is skipped on a docs-, infra-, or workflow-only change, so
// tools/coverage/find-coverage-baseline.mjs walks back to the newest run that
// actually ran coverage (#589, ADR-0165). tools/dev/ci-local.sh calls the same
// script, so both pick the same run.
//
// Outputs `path` and `run-id`, both `none` when there is no usable baseline.
// Reads GITHUB_REPOSITORY and GH_TOKEN (for gh).
import { existsSync } from 'node:fs'
import { annotation, exec as realExec, isMain, required, setOutput } from '../lib/actions.mjs'

export const BASELINE_DIR = './artifacts/baseline'
export const BASELINE_FILE = `${BASELINE_DIR}/Cobertura.xml`

export function main({ env = process.env, exec = realExec, log = console.log, exists = existsSync } = {}) {
	const none = () => {
		setOutput('path', 'none', env)
		setOutput('run-id', 'none', env)
		return 0
	}

	// `|| true`: a failing finder leaves the ratchet skipped, never fails the job.
	const found = exec('node', ['tools/coverage/find-coverage-baseline.mjs', '--repo', required(env, 'GITHUB_REPOSITORY')])
	if (found.stderr) log(found.stderr)
	const runId = found.stdout
	if (runId === '' || runId === 'none') return none()

	const download = exec('gh', ['run', 'download', runId, '--name', 'coverage-report', '--dir', BASELINE_DIR])
	if (download.status === 0 && exists(BASELINE_FILE)) {
		setOutput('path', BASELINE_FILE, env)
		setOutput('run-id', runId, env)
		return 0
	}
	log(annotation('notice', `main run ${runId} reported a coverage-report artifact, but the download failed. Ratchet skipped; the floor still applies.`))
	return none()
}

if (isMain(import.meta.url)) process.exit(main())
