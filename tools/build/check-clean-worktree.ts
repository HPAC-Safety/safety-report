#!/usr/bin/env node
// Fails when a step left tracked or untracked changes in the working tree.
//
// Used after a build (ci.yml `web`: dist/ is generated and gitignored, so a
// build that produces a tracked change means something that should be
// generated has been committed) and after `skillfile install` (ci.yml
// `agent-config`: .claude/skills and .claude/agents are gitignored, so
// tracked files changing is drift worth failing on).
//
//   node tools/build/check-clean-worktree.ts "<what changed the tree>:"
//
// The argument is the annotation text, which is followed by the porcelain
// status lines.
import { annotation, exec as realExec, isMain, type Exec } from '../lib/actions.ts'

export interface MainOptions {
	argv?: readonly string[]
	exec?: Exec
	log?: (line: string) => void
}

export function main({ argv = process.argv.slice(2), exec = realExec, log = console.log }: MainOptions = {}): number {
	const message = argv.at(0)
	if (message === undefined || message === '') {
		log(annotation('error', 'A message is required.'))
		return 2
	}
	// Only whether anything is listed matters here; exec trims, so the listing
	// itself is printed by a second, untrimmed run.
	if (exec('git', ['status', '--porcelain'], { check: true }).stdout === '') return 0
	log(annotation('error', message))
	exec('git', ['status', '--porcelain'], { inherit: true })
	return 1
}

if (isMain(import.meta.url)) process.exit(main())
