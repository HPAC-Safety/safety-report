#!/usr/bin/env node
// Hands the raw coverage reports from the `test` job to the `coverage` job
// under act (ci.yml, ADR-0145).
//
// act's artifact server rejects upload-artifact@v7 and download-artifact@v8
// (nektos/act#6022), so under act the two jobs exchange the reports through
// the per-run directory tools/dev/ci-local.sh mounts at /ci-local-share.
//
//   node tools/coverage/hand-over-raw-reports.mjs push   # test: ./artifacts/coverage -> share
//   node tools/coverage/hand-over-raw-reports.mjs pull   # coverage: share -> ./artifacts/coverage
//
// `push` makes the copy a+rwX: the container writes as root, and on a Linux
// host the wrapper removing the run's directory afterwards is not root.
// CI_LOCAL_SHARE overrides the mount point, for the test only.
import { existsSync, mkdirSync } from 'node:fs'
import { annotation, exec as realExec, isMain } from '../lib/actions.mjs'

const LOCAL = './artifacts/coverage'

export function main({ argv = process.argv.slice(2), env = process.env, exec = realExec, log = console.log } = {}) {
	const share = env.CI_LOCAL_SHARE || '/ci-local-share'
	const shared = `${share}/coverage`
	const direction = argv[0]
	if (direction === 'push') {
		if (!existsSync(share)) {
			log(annotation('error', `${share} is not mounted; run this workflow through tools/dev/ci-local.sh`))
			return 1
		}
		mkdirSync(shared, { recursive: true })
		exec('cp', ['-R', `${LOCAL}/.`, `${shared}/`], { check: true })
		exec('chmod', ['-R', 'a+rwX', shared], { check: true })
		return 0
	}
	if (direction === 'pull') {
		if (!existsSync(shared)) {
			log(annotation('error', `no raw reports in ${shared}; run this workflow through tools/dev/ci-local.sh`))
			return 1
		}
		mkdirSync(LOCAL, { recursive: true })
		exec('cp', ['-R', `${shared}/.`, `${LOCAL}/`], { check: true })
		return 0
	}
	log(annotation('error', 'Usage: hand-over-raw-reports.mjs push|pull'))
	return 2
}

if (isMain(import.meta.url)) process.exit(main())
