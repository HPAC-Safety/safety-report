#!/usr/bin/env node
// Each queued commit's message carries no agent session link (ADR-0107,
// ADR-0147).
//
// The body becomes the squash commit message on a public repository, so a
// coding agent's session link in it would publish that session. On merge_group
// this checks what will actually reach main: each queued squash commit's
// message, fixed when the queue built the group. A body edited after queuing
// does not eject the entry, and no commit-msg hook runs on a server-side
// squash, so this is the last place to catch it. An empty range would check
// nothing, so it fails rather than passes.
//
// Environment: BASE_SHA. Checks every commit before reporting, like the loop it
// replaces, rather than stopping at the first.
import { exec, isMain, required, type Env, type Exec } from '../lib/actions.ts'
import { main as checkText } from './check-agent-session-links.ts'

export function main({ env = process.env, exec: run = exec, log = console.log }: { env?: Env; exec?: Exec; log?: (line: string) => void } = {}): number {
	const base = required(env, 'BASE_SHA')
	const commits = run('git', ['rev-list', '--reverse', `${base}..HEAD`], { check: true }).stdout.split('\n').filter(Boolean)
	if (commits.length === 0) {
		console.error(`::error::The merge group holds no commit between ${base} and HEAD, so nothing was checked.`)
		return 1
	}

	let failed = false
	for (const commit of commits) {
		log(`::group::${run('git', ['log', '-1', '--format=%s', commit], { check: true }).stdout}`)
		const message = run('git', ['log', '-1', '--format=%B', commit], { check: true }).stdout
		if (checkText(message, 'The commit message') !== 0) failed = true
		log('::endgroup::')
	}
	return failed ? 1 : 0
}

if (isMain(import.meta.url)) process.exit(main())
