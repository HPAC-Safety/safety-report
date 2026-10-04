import { describe, it } from 'node:test'
import assert from 'node:assert/strict'
import { spawnSync } from 'node:child_process'
import { fileURLToPath } from 'node:url'
import path from 'node:path'

import { reminder } from '../../../tools/github/remind-critic.ts'

const scriptPath = path.join(path.dirname(fileURLToPath(import.meta.url)), '../../../tools/github/remind-critic.ts')

function run(payload: unknown) {
	return spawnSync(process.execPath, [scriptPath], { input: JSON.stringify(payload), encoding: 'utf8' })
}

describe('reminder', () => {
	it('reminds on ExitPlanMode', () => {
		assert.match(reminder('ExitPlanMode', undefined) ?? '', /critic/)
	})

	it('reminds on gh issue create, however it is spaced or cased', () => {
		assert.ok(reminder('Bash', 'gh issue create --title "x"'))
		assert.ok(reminder('Bash', 'GH   ISSUE   CREATE'))
	})

	it('stays silent for any other command or tool', () => {
		assert.equal(reminder('Bash', 'gh issue view 1'), null)
		assert.equal(reminder('Bash', 42), null)
		assert.equal(reminder('Read', 'gh issue create'), null)
	})
})

describe('as a hook command', () => {
	it('exits 0 with additionalContext on ExitPlanMode', () => {
		const result = run({ tool_name: 'ExitPlanMode', tool_input: {} })

		assert.equal(result.status, 0)
		const output = JSON.parse(result.stdout) as { hookSpecificOutput: { hookEventName: string; additionalContext: string } }
		assert.equal(output.hookSpecificOutput.hookEventName, 'PreToolUse')
		assert.match(output.hookSpecificOutput.additionalContext, /critic/)
	})

	it('exits 0 and prints nothing for an unrelated command', () => {
		const result = run({ tool_name: 'Bash', tool_input: { command: 'ls' } })

		assert.equal(result.status, 0)
		assert.equal(result.stdout, '')
	})

	it('exits 0 on input that is not JSON', () => {
		const result = spawnSync(process.execPath, [scriptPath], { input: 'nope', encoding: 'utf8' })

		assert.equal(result.status, 0)
	})
})
