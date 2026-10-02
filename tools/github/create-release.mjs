#!/usr/bin/env node
// Creates the next `YYYY.MM.DD-N` tag and its GitHub Release from `main`
// (release.yml; ADR-0158, ADR-0166, CON-INF-011).
//
// Refuses to run anywhere but refs/heads/main. Today's releases are
// YYYY.MM.DD-1, -2, ...; this takes the next free N (today in UTC). The
// release body is a provenance table; `--generate-notes` then appends every
// pull request merged since the previous release, grouped by
// .github/release.yml.
//
// Outputs: `tag`. Also writes the run summary.
//
// Environment: GH_TOKEN, REPO, REF, SHA, SERVER, RUN_ID.
import { mkdtempSync, writeFileSync } from 'node:fs'
import { tmpdir } from 'node:os'
import path from 'node:path'

import { appendSummary, exec, isMain, required, setOutput } from '../lib/actions.mjs'

/** `YYYY.MM.DD` for a date, in UTC. */
export function dayOf(date) {
	const pad = (value) => String(value).padStart(2, '0')
	return `${date.getUTCFullYear()}.${pad(date.getUTCMonth() + 1)}.${pad(date.getUTCDate())}`
}

/** The next free tag for a day, given the `refs/tags/...` names that already exist for it. */
export function nextTag(day, refs) {
	const prefix = `refs/tags/${day}-`
	const taken = refs
		.filter((ref) => ref.startsWith(prefix) && /^[1-9][0-9]*$/.test(ref.slice(prefix.length)))
		.map((ref) => Number(ref.slice(prefix.length)))
	return `${day}-${Math.max(0, ...taken) + 1}`
}

/** The provenance table that opens the release body. */
export function provenance({ sha, server, repo, runId, tag }) {
	return [
		'## Provenance',
		'',
		'| Commit | Release run |',
		'|---|---|',
		`| [\`${sha}\`](${server}/${repo}/commit/${sha}) | [run ${runId}](${server}/${repo}/actions/runs/${runId}) |`,
		'',
		`This run deploys it to staging. Promote to production: Actions → Promote → Use workflow from → Tags → \`${tag}\`.`,
		'',
		'',
	].join('\n')
}

export function main({ env = process.env, exec: run = exec, now = () => new Date(), log = console.log } = {}) {
	const repo = required(env, 'REPO')
	const ref = required(env, 'REF')
	const sha = required(env, 'SHA')
	const server = required(env, 'SERVER')
	const runId = required(env, 'RUN_ID')

	if (ref !== 'refs/heads/main') {
		log(`::error::Release from main only (this run is on ${ref}). Run workflow → Use workflow from → main.`)
		return 1
	}

	const day = dayOf(now())
	const listing = run('gh', ['api', `repos/${repo}/git/matching-refs/tags/${day}-`, '--jq', '.[].ref'], { check: true }).stdout
	const tag = nextTag(day, listing.split('\n').filter(Boolean))

	const notes = path.join(mkdtempSync(path.join(tmpdir(), 'release-')), 'notes.md')
	writeFileSync(notes, provenance({ sha, server, repo, runId, tag }))

	run('gh', ['release', 'create', tag, '--repo', repo, '--target', sha, '--title', tag, '--notes-file', notes, '--generate-notes'], { check: true })

	setOutput('tag', tag, env)
	appendSummary(`### Release [${tag}](${server}/${repo}/releases/tag/${tag})`, env)
	return 0
}

if (isMain(import.meta.url)) process.exit(main())
