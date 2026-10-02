#!/usr/bin/env node
// Posts a terraform plan as a pull request comment.
//
// The comment is the point of the plan job. A plan nobody reads is not a review
// artifact, and a plan buried in a job log is a plan nobody reads. One comment
// PER ACCOUNT (a distinct marker each), `-edit-last --create-if-none` keeping
// each one to a single, updated comment across pushes, matching what the
// coverage job does. GitHub caps a comment at 65536 characters, so a long plan
// is tailed rather than truncated at the front — the end of a plan is where the
// summary line is.
//
// Environment: ACCOUNT, PLAN_OUTCOME, PR_NUMBER, GH_TOKEN (read by gh).
// Reads plan.txt, writes comment.md, both in the working directory.
import { readFileSync, writeFileSync } from 'node:fs'
import { exec as realExec, isMain, required } from '../lib/actions.mjs'

export const TAIL_BYTES = 60000

/** The comment body, as bytes: the plan is tailed by bytes (`tail -c`) and echoed raw. */
export function commentBody({ account, outcome, planBytes }) {
	const head = [
		`<!-- terraform-plan-${account} -->`,
		`### Terraform plan — ${account} — \`${outcome}\``,
		'',
		'<details><summary>Plan output</summary>',
		'',
		'```terraform',
		'',
	].join('\n')
	const tail = ['```', '', '</details>', ''].join('\n')
	return Buffer.concat([Buffer.from(head), planBytes.subarray(Math.max(0, planBytes.length - TAIL_BYTES)), Buffer.from(tail)])
}

export function main({ env = process.env, exec = realExec, readPlan = () => readFileSync('plan.txt'), writeComment = (body) => writeFileSync('comment.md', body) } = {}) {
	const body = commentBody({ account: required(env, 'ACCOUNT'), outcome: required(env, 'PLAN_OUTCOME'), planBytes: readPlan() })
	writeComment(body)
	exec('gh', ['pr', 'comment', required(env, 'PR_NUMBER'), '--body-file', 'comment.md', '--edit-last', '--create-if-none'], {
		check: true,
		inherit: true,
	})
	return 0
}

if (isMain(import.meta.url)) process.exit(main())
