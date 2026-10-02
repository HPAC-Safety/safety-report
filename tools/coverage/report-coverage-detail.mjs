#!/usr/bin/env node
// Appends the detailed per-file coverage table to the job summary (ci.yml
// `coverage`), always, so a failure is diagnosable from the run page without
// downloading anything. Does nothing when ReportGenerator wrote no summary.
import { existsSync, readFileSync } from 'node:fs'
import { appendSummary, isMain } from '../lib/actions.mjs'

export const SUMMARY = './artifacts/report/SummaryGithub.md'

/** The collapsible block that wraps ReportGenerator's markdown. */
export function detailBlock(markdown) {
	return `\n<details><summary>Per-assembly detail</summary>\n\n${markdown}\n</details>\n`
}

export function main({ env = process.env, summary = SUMMARY } = {}) {
	if (existsSync(summary)) appendSummary(detailBlock(readFileSync(summary, 'utf8')), env)
	return 0
}

if (isMain(import.meta.url)) process.exit(main())
