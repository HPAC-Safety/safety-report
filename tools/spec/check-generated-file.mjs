#!/usr/bin/env node
// Fails when a committed generated file no longer matches its generator
// (ci.yml `docs`).
//
// Regenerating and diffing catches a committed file that no longer matches the
// artifacts it summarizes - the same shape as "No drift from installing
// skills" and dotnet format --verify-no-changes (ADR-0084, ADR-0184). On a
// pull request it is the backstop, not the fixer: traceability.yml
// regenerates the file onto a same-repo PR's branch, so this fails only for a
// fork or a changed generator (ADR-0101). On a merge group it is the gate:
// when two queued pull requests both change the specification, the second
// one's file is stale on the merged tree, so it fails here and leaves the
// queue to be rebased (ADR-0147).
//
//   node tools/spec/check-generated-file.mjs --generator <script> --file <path> --message <text>
//
// --message is the annotation text shown on the file when it is stale.
import { annotation, exec as realExec, isMain } from '../lib/actions.mjs'

/** Reads `--name value` pairs. */
export function parseArgs(argv) {
	const args = {}
	for (let i = 0; i < argv.length; i += 2) args[argv[i].replace(/^--/, '')] = argv[i + 1]
	return args
}

export function main({ argv = process.argv.slice(2), exec = realExec, log = console.log } = {}) {
	const { generator, file, message } = parseArgs(argv)
	if (!generator || !file || !message) {
		log(annotation('error', '--generator, --file, and --message are required.'))
		return 2
	}
	exec('node', [generator], { inherit: true, check: true })
	const diff = exec('git', ['diff', '--exit-code', '--stat', '--', file], { inherit: true })
	if (diff.status === 0) return 0
	log(annotation('error', message, { file }))
	exec('git', ['diff', '--', file], { inherit: true })
	return 1
}

if (isMain(import.meta.url)) process.exit(main())
