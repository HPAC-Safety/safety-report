#!/usr/bin/env node
// Hands the raw coverage reports from the `test` job, and the claim results
// from `test` and `e2e`, to the `coverage` job under act (ci.yml, ADR-0145,
// ADR-0195).
//
// act's artifact server rejects upload-artifact@v7 and download-artifact@v8
// (nektos/act#6022), so under act the jobs exchange them through the per-run
// directory tools/dev/ci-local.sh mounts at /ci-local-share.
//
//   node tools/coverage/hand-over-raw-reports.ts push [name]  # ./artifacts/<name> -> share
//   node tools/coverage/hand-over-raw-reports.ts pull [name]  # share -> ./artifacts/<name>
//
// <name> is `coverage` (the default) or `claims`. Two jobs may push the same
// name: their files merge in the share.
//
// `push` makes the copy a+rwX: the container writes as root, and on a Linux
// host the wrapper removing the run's directory afterwards is not root.
// CI_LOCAL_SHARE overrides the mount point, for the test only.
import { existsSync, mkdirSync } from 'node:fs'
import { type Env, type Exec, annotation, exec as realExec, isMain } from '../lib/actions.ts'

/** What may be handed over: a directory under ./artifacts. */
export const HANDED_OVER = ['coverage', 'claims'] as const

export interface MainOptions {
	argv?: readonly string[]
	env?: Env
	exec?: Exec
	log?: (line: string) => void
}

export function main({ argv = process.argv.slice(2), env = process.env, exec = realExec, log = console.log }: MainOptions = {}): number {
	const share = env.CI_LOCAL_SHARE || '/ci-local-share'
	const name = argv[1] ?? 'coverage'
	if (!(HANDED_OVER as readonly string[]).includes(name)) {
		log(annotation('error', `Usage: hand-over-raw-reports.ts push|pull [${HANDED_OVER.join('|')}]`))
		return 2
	}
	const local = `./artifacts/${name}`
	const shared = `${share}/${name}`
	const direction = argv[0]
	if (direction === 'push') {
		if (!existsSync(share)) {
			log(annotation('error', `${share} is not mounted; run this workflow through tools/dev/ci-local.sh`))
			return 1
		}
		mkdirSync(shared, { recursive: true })
		exec('cp', ['-R', `${local}/.`, `${shared}/`], { check: true })
		exec('chmod', ['-R', 'a+rwX', shared], { check: true })
		return 0
	}
	if (direction === 'pull') {
		if (!existsSync(shared)) {
			log(annotation('error', `no raw reports in ${shared}; run this workflow through tools/dev/ci-local.sh`))
			return 1
		}
		mkdirSync(local, { recursive: true })
		exec('cp', ['-R', `${shared}/.`, `${local}/`], { check: true })
		return 0
	}
	log(annotation('error', `Usage: hand-over-raw-reports.ts push|pull [${HANDED_OVER.join('|')}]`))
	return 2
}

if (isMain(import.meta.url)) process.exit(main())
