#!/usr/bin/env node
// Appends a link to the HTML coverage report to comment.md (ci.yml
// `coverage`, #838). The totals in the comment say how much is covered; the
// report says which lines are not, so a reviewer can open it from the comment
// rather than hunting for the artifact on the run page. REPORT_URL is the
// `artifact-url` output of the step that uploaded `coverage-report`; it is
// empty under act, which has no artifact server (nektos/act#6022).
import { appendFileSync } from 'node:fs'
import { type Env, isMain } from '../lib/actions.ts'

export const NO_REPORT = '_No coverage report was uploaded in this run._\n'

export function main({ env = process.env, comment = 'comment.md' }: { env?: Env; comment?: string } = {}): number {
	const url = env.REPORT_URL?.trim()
	const line = url
		? `[Download the coverage report](${url}) — unzip it and open \`index.html\`: uncovered lines are red, partly covered branches yellow.\n`
		: NO_REPORT
	appendFileSync(comment, `\n## Coverage report\n\n${line}`)
	return 0
}

if (isMain(import.meta.url)) process.exit(main())
