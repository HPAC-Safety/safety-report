#!/usr/bin/env node
// Installs the pinned skillfile release onto the runner's PATH (ci.yml
// `agent-config`).
//
// Downloads the release binary with gh into $RUNNER_TEMP, makes it
// executable, and appends that directory to $GITHUB_PATH so the later steps
// run `skillfile` by name. The version is pinned here; bump it with the lock.
// Reads GH_TOKEN (for gh), RUNNER_TEMP, and GITHUB_PATH.
import { appendFileSync, chmodSync, statSync } from 'node:fs'
import { exec as realExec, isMain, required } from '../lib/actions.mjs'

export const VERSION = 'v1.9.1'
export const REPO = 'eljulians/skillfile'
export const ASSET = 'skillfile-x86_64-linux'

/** Adds the execute bits to a file, as `chmod +x` does. */
function makeExecutable(file) {
	chmodSync(file, statSync(file).mode | 0o111)
}

export function main({ env = process.env, exec = realExec, chmod = makeExecutable, appendPath = appendFileSync } = {}) {
	const temp = required(env, 'RUNNER_TEMP')
	const output = `${temp}/skillfile`
	exec('gh', ['release', 'download', VERSION, '--repo', REPO, '--pattern', ASSET, '--output', output], { check: true })
	chmod(output)
	appendPath(required(env, 'GITHUB_PATH'), `${temp}\n`)
	return 0
}

if (isMain(import.meta.url)) process.exit(main())
