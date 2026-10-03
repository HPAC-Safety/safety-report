#!/usr/bin/env node
// A rendered web change shows its screenshots (ADR-0142), judged on the files
// the pull request changed against its base.
//
// tools/web/check-pr-screenshots.ts holds the rule and reads only the body
// and the changed-file list; this supplies the list as CHANGED_FILES from
// `git diff --name-only $BASE_SHA...HEAD`, then runs that tool's own check.
//
// Environment: PR_BODY, BASE_SHA, and GITHUB_REPOSITORY (Actions sets it; it
// pins the screenshot links to this repository).
import { exec, isMain, required, type Env, type Exec } from '../lib/actions.ts'
import { main as check, readInput } from '../web/check-pr-screenshots.ts'

export function main({ env = process.env, exec: run = exec }: { env?: Env; exec?: Exec } = {}): number {
	const base = required(env, 'BASE_SHA')
	const changed = run('git', ['diff', '--name-only', `${base}...HEAD`], { check: true }).stdout
	return check(readInput({ ...env, CHANGED_FILES: changed }))
}

if (isMain(import.meta.url)) process.exit(main())
