#!/usr/bin/env node
// Merges the pull-request merge/enqueue guard (tools/guard-pr-merge.mjs,
// ADR-0147 amendment, issue #427) into .claude/settings.json as a Claude Code
// `PreToolUse` hook on the `Bash` matcher.
//
// .claude/settings.json is gitignored — `graphify claude install` writes it
// with a machine-local, absolute path to the graphify binary, so nothing
// there is committed. This script is the tracked half, run by `init-dev.sh`
// after that step (same shape as .githooks: a tracked template, installed
// into an untracked, real location), and it is a merge, not an overwrite, so
// it never disturbs graphify's own hooks or settings. It is idempotent:
// running it again on a file that already carries the guard changes nothing.
//
//   node tools/install-pr-merge-guard.mjs [settings.json path]
//
// Defaults to .claude/settings.json under the current working directory.
import { existsSync, mkdirSync, readFileSync, writeFileSync } from 'node:fs'
import { dirname } from 'node:path'

const GUARD_COMMAND = 'node "$CLAUDE_PROJECT_DIR/tools/guard-pr-merge.mjs"'

/** True when `settings` already carries a PreToolUse/Bash hook running the guard. */
function hasGuard(settings) {
	const entries = settings?.hooks?.PreToolUse
	if (!Array.isArray(entries)) return false
	return entries.some((entry) => entry?.matcher === 'Bash' && Array.isArray(entry.hooks) && entry.hooks.some((hook) => hook?.command?.includes('guard-pr-merge.mjs')))
}

/** Returns a new settings object with the guard hook merged in, reusing an existing Bash matcher entry when there is one. */
export function withGuardHook(settings) {
	if (hasGuard(settings)) return settings

	const result = { ...settings, hooks: { ...(settings?.hooks ?? {}) } }
	const preToolUse = Array.isArray(result.hooks.PreToolUse) ? [...result.hooks.PreToolUse] : []

	const bashEntryIndex = preToolUse.findIndex((entry) => entry?.matcher === 'Bash')
	const guardHook = { type: 'command', command: GUARD_COMMAND }

	if (bashEntryIndex === -1) {
		preToolUse.push({ matcher: 'Bash', hooks: [guardHook] })
	} else {
		const entry = preToolUse[bashEntryIndex]
		preToolUse[bashEntryIndex] = { ...entry, hooks: [...(entry.hooks ?? []), guardHook] }
	}

	result.hooks.PreToolUse = preToolUse
	return result
}

/** Reads, merges, and writes back the settings file at `path`. Returns whether it changed anything. */
export function install(path) {
	let settings = {}
	if (existsSync(path)) {
		try {
			settings = JSON.parse(readFileSync(path, 'utf8'))
		} catch (error) {
			throw new Error(`${path} is not valid JSON — fix it by hand before running this again: ${error.message}`)
		}
	}

	if (hasGuard(settings)) return false

	mkdirSync(dirname(path), { recursive: true })
	writeFileSync(path, `${JSON.stringify(withGuardHook(settings), null, '\t')}\n`)
	return true
}

const runAsCommand = String(process.argv[1]).endsWith('install-pr-merge-guard.mjs')
if (runAsCommand) {
	const path = process.argv[2] ?? '.claude/settings.json'
	try {
		process.exit(install(path) ? 0 : 0)
	} catch (error) {
		console.error(error.message)
		process.exit(1)
	}
}
