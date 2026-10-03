#!/usr/bin/env node
// Appends the test-count tables to comment.md (ci.yml `coverage`).
//
// Appended to the comment the Gate step already wrote rather than a separate
// comment, per #263 - one table pair, not two comments to scroll past. The C#
// counts come from the trx files in ./artifacts/coverage; the Playwright
// counts from the fragment the e2e job uploaded, which only exists when that
// job ran, so a dotnet-only pull request gets a "not run" line instead.
import { appendFileSync, existsSync, readFileSync } from 'node:fs'
import { type Exec, exec as realExec, isMain } from '../lib/actions.ts'

export const E2E_FRAGMENT = './artifacts/test-count/e2e.md'
export const NOT_RUN = '_Playwright suite not run in this job._\n'

export interface MainOptions {
	exec?: Exec
	comment?: string
	fragment?: string
}

export function main({ exec = realExec, comment = 'comment.md', fragment = E2E_FRAGMENT }: MainOptions = {}): number {
	const csharp = exec('node', ['tools/coverage/report-test-counts.ts', '--section', 'csharp', '--trx-dir', './artifacts/coverage'], { check: true })
	// exec trims; report-test-counts always ends its csharp output with a blank line.
	const playwright = existsSync(fragment) ? readFileSync(fragment, 'utf8') : NOT_RUN
	appendFileSync(comment, `\n## Test counts\n\n${csharp.stdout}\n\n${playwright}`)
	return 0
}

if (isMain(import.meta.url)) process.exit(main())
