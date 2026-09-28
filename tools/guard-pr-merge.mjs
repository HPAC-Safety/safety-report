#!/usr/bin/env node
// Refuses a Bash tool call that would merge or enqueue a pull request
// (ADR-0147 amendment, issue #427). Only the owner enables auto-merge or
// enqueues a pull request, by hand — an agent opens the pull request, gets
// its checks green, and reports it ready.
//
// Wired in as a Claude Code `PreToolUse` hook (matcher: Bash) by
// `init-dev.sh`, which merges a hook entry into `.claude/settings.json` —
// that file is machine-local and gitignored (written by
// `graphify claude install`), so this script, not the settings file, is what
// the repository actually tracks.
//
//   <hook JSON on stdin> | node tools/guard-pr-merge.mjs
//
// Exit code is the contract Claude Code reads: 0 allows the tool call, 2
// blocks it and shows stderr to the agent as the reason. Any input that is
// not a Bash call with a command, or that matches neither rule, is allowed.
import { readFileSync } from 'node:fs'

// `gh pr merge` in any form: any flags before or after, any casing of `gh`.
const GH_PR_MERGE = /\bgh\s+pr\s+merge\b/i

// The two mutations that enable auto-merge or enqueue a pull request. Blocked
// wherever they appear in the command — a `gh api graphql` call, a heredoc
// body, an inline query string — not only when `gh api graphql` itself is
// spelled out literally, so re-wrapping the same mutation does not evade it.
const AUTOMERGE_MUTATIONS = /\b(enablePullRequestAutoMerge|enqueuePullRequest)\b/

/** Decides whether a Bash command merges or enqueues a pull request. Returns the reason, or null to allow it. */
export function blockReason(toolName, command) {
	if (toolName !== 'Bash') return null
	if (typeof command !== 'string' || command.trim() === '') return null

	if (GH_PR_MERGE.test(command)) {
		return (
			'Only the owner enables auto-merge or merges a pull request, by hand ' +
			'(ADR-0147 amendment, issue #427). Open the pull request, get its ' +
			'checks green, and report it ready — do not run `gh pr merge`.'
		)
	}

	const mutation = command.match(AUTOMERGE_MUTATIONS)
	if (mutation) {
		return (
			`Only the owner enables auto-merge or enqueues a pull request, by hand ` +
			`(ADR-0147 amendment, issue #427). This command names the ` +
			`\`${mutation[1]}\` mutation — remove it and report the pull request ` +
			'ready instead.'
		)
	}

	return null
}

/** Reads one Claude Code PreToolUse hook payload and returns the exit code. */
export function main(input) {
	let payload
	try {
		payload = JSON.parse(input)
	} catch {
		return 0 // not a hook payload we understand; never block on a parse failure
	}

	const toolName = payload?.tool_name
	const command = payload?.tool_input?.command
	const reason = blockReason(toolName, command)
	if (reason === null) return 0

	console.error(reason)
	return 2
}

const runAsCommand = String(process.argv[1]).endsWith('guard-pr-merge.mjs')
if (runAsCommand) {
	process.exit(main(readFileSync(0, 'utf8')))
}
