#!/usr/bin/env node
// Reminds an agent to run the critic on a plan before the plan is final
// (CONV-007). It never blocks: every path exits 0, and the reminder travels as
// `additionalContext`, which Claude Code adds to the agent's context.
//
// Wired in as a Claude Code `PreToolUse` hook in the tracked
// `.claude/settings.json`, for the `ExitPlanMode` and `Bash` tools:
//
//   <hook JSON on stdin> | node tools/github/remind-critic.ts
//
// `ExitPlanMode` always reminds. `Bash` reminds only when the command runs
// `gh issue create`. Any other input prints nothing.
import { readFileSync } from 'node:fs'
import { isMain } from '../lib/actions.ts'

const GH_ISSUE_CREATE = /\bgh\s+issue\s+create\b/i

const REMINDER =
	'Before this plan is final, run the critic agent on it, unless it has already run on this plan. ' +
	'One pass, then at most one recheck of the prior findings only; open findings after that go to the owner (CONV-007).'

/** The reminder for a tool call, or null when none applies. */
export function reminder(toolName: unknown, command: unknown): string | null {
	if (toolName === 'ExitPlanMode') return REMINDER
	if (toolName === 'Bash' && typeof command === 'string' && GH_ISSUE_CREATE.test(command)) return REMINDER
	return null
}

/** The parts of a PreToolUse hook payload this reads; the rest is ignored. */
interface HookPayload {
	tool_name?: unknown
	tool_input?: { command?: unknown } | null
}

/** Reads one hook payload, prints a reminder when one applies, and always returns 0. */
export function main(input: string): number {
	let payload: HookPayload | null | undefined
	try {
		payload = JSON.parse(input) as HookPayload | null | undefined
	} catch {
		return 0
	}

	const text = reminder(payload?.tool_name, payload?.tool_input?.command)
	if (text !== null) {
		console.log(JSON.stringify({ hookSpecificOutput: { hookEventName: 'PreToolUse', additionalContext: text } }))
	}
	return 0
}

const runAsCommand = isMain(import.meta.url)
if (runAsCommand) {
	process.exit(main(readFileSync(0, 'utf8')))
}
