#!/usr/bin/env node
// Pushes a bot's one commit onto a same-repo pull request's own branch, past
// another bot that pushed first (ADR-0113, lesson 0016).
//
// traceability.yml and i18n-translate.yml both fire on the same push and both
// commit onto the branch they were fired for. Whichever pushes second is
// rejected. The rejection means one of two things, and only the files the
// branch gained since the event can tell them apart:
//
//   * A file this workflow is triggered by changed. That push started its own
//     run of this workflow over the newer head, so this run is superseded and
//     exits 0 without pushing.
//   * No such file changed — the other bot pushed its own output. GitHub
//     filters `paths` per push, so that push started no run of this workflow,
//     and nothing else will ever redo this one's work. The commit is
//     cherry-picked onto the newer head and pushed again. The files that
//     changed are not this workflow's inputs, so its output is still correct.
//
// Usage (in a checkout whose HEAD is the bot's commit on top of the event SHA):
//   node tools/push-to-pr-branch.mjs --branch <ref> --event-sha <sha> --paths <pattern,...>
//
// --paths repeats the workflow's own `pull_request_target.paths`;
// tests/js/push-to-pr-branch.test.mjs fails if the two lists disagree.
import { execFileSync, spawnSync } from 'node:child_process'

const ATTEMPTS = 5

/**
 * Whether a changed path matches one of a workflow's `paths` patterns. Only the
 * two shapes the workflows use are understood — `dir/**` and an exact path — and
 * anything else throws, so a new pattern can't silently match nothing.
 */
export function matchesAny(file, patterns) {
	return patterns.some((pattern) => {
		if (pattern.endsWith('/**')) {
			const prefix = pattern.slice(0, -2)
			if (/[*?[\]!]/.test(prefix)) throw new Error(`Unsupported paths pattern: ${pattern}`)
			return file.startsWith(prefix)
		}
		if (/[*?[\]!]/.test(pattern)) throw new Error(`Unsupported paths pattern: ${pattern}`)
		return file === pattern
	})
}

/** What a rejected push means, given the files the branch gained since the event. */
export function afterRejection({ branchSha, eventSha, changed, patterns }) {
	if (branchSha === eventSha) return 'failed'
	return changed.some((file) => matchesAny(file, patterns)) ? 'superseded' : 'retry'
}

function git(cwd, ...args) {
	return execFileSync('git', args, { cwd, encoding: 'utf8', stdio: ['ignore', 'pipe', 'pipe'] }).trim()
}

function tryGit(cwd, ...args) {
	const result = spawnSync('git', args, { cwd, encoding: 'utf8' })
	return { ok: result.status === 0, output: `${result.stdout}${result.stderr}`.trim() }
}

/** Parses `--name value` pairs. */
function parse(argv) {
	const options = {}
	for (let i = 0; i < argv.length; i += 2) options[argv[i].replace(/^--/, '')] = argv[i + 1]
	return options
}

/**
 * Pushes HEAD, retrying past another bot's push. Reports the way the command
 * line does, without exiting, so a test can run it in-process. Returns the exit
 * code.
 */
export function main(argv = [], cwd = process.cwd()) {
	const { branch, 'event-sha': eventSha, paths } = parse(argv)
	if (!branch || !eventSha || !paths) {
		console.error('Usage: node tools/push-to-pr-branch.mjs --branch <ref> --event-sha <sha> --paths <pattern,...>')
		return 1
	}
	const patterns = paths.split(',').map((pattern) => pattern.trim()).filter(Boolean)
	const commit = git(cwd, 'rev-parse', 'HEAD')

	for (let attempt = 1; attempt <= ATTEMPTS; attempt++) {
		const push = tryGit(cwd, 'push', 'origin', `HEAD:refs/heads/${branch}`)
		if (push.ok) {
			console.log(`Pushed ${git(cwd, 'rev-parse', '--short', 'HEAD')} onto ${branch}.`)
			return 0
		}
		console.log(push.output)

		const fetch = tryGit(cwd, 'fetch', '--depth=1', 'origin', `refs/heads/${branch}`)
		if (!fetch.ok) {
			console.error(`::error::Could not fetch ${branch} after a rejected push.\n${fetch.output}`)
			return 1
		}
		const branchSha = git(cwd, 'rev-parse', 'FETCH_HEAD')
		const changed = git(cwd, 'diff', '--name-only', eventSha, branchSha).split('\n').filter(Boolean)

		switch (afterRejection({ branchSha, eventSha, changed, patterns })) {
			case 'failed':
				console.error(`::error::Could not push onto ${branch}, and the branch has not moved.`)
				return 1
			case 'superseded':
				console.log(`::notice::${branch} moved to ${branchSha} with a change this workflow is triggered by. The run for that push does this work; nothing to do here.`)
				return 0
		}

		console.log(`${branch} moved to ${branchSha} with no change this workflow is triggered by (${changed.join(', ')}). Replaying the commit on top of it.`)
		git(cwd, 'checkout', '--quiet', '--detach', branchSha)
		const pick = tryGit(cwd, 'cherry-pick', commit)
		if (!pick.ok) {
			console.error(`::error::The commit does not apply on top of ${branchSha}.\n${pick.output}`)
			return 1
		}
	}

	console.error(`::error::Could not push onto ${branch} in ${ATTEMPTS} attempts.`)
	return 1
}

const runAsCommand = String(process.argv[1]).endsWith('push-to-pr-branch.mjs')
if (runAsCommand) process.exit(main(process.argv.slice(2)))
