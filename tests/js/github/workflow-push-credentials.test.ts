import { describe, it } from 'node:test'
import assert from 'node:assert/strict'
import { readdirSync, readFileSync } from 'node:fs'
import { dirname, join } from 'node:path'
import { fileURLToPath } from 'node:url'

const REPO = join(dirname(fileURLToPath(import.meta.url)), '..', '..', '..')
const WORKFLOWS = join(REPO, '.github/workflows')

/** Each `actions/checkout` step in a workflow, as the text of that step. */
function checkoutSteps(text: string): string[] {
	const lines = text.split('\n')
	const steps: string[] = []
	const indentOf = (line: string): number => line.search(/\S/)
	const opensStep = (line: string): boolean => /^\s*- /.test(line)
	lines.forEach((line, i) => {
		if (!/^\s*(- )?uses: actions\/checkout@/.test(line)) return
		// The step's dash is on this line, or on the `- name:` line above it.
		let start = i
		while (!opensStep(lines[start])) start--
		const dash = indentOf(lines[start])
		let end = i + 1
		while (end < lines.length && !(lines[end].trim() !== '' && indentOf(lines[end]) <= dash)) end++
		steps.push(lines.slice(start, end).join('\n'))
	})
	return steps
}

/**
 * Whether a workflow runs a script under tools/ that puts a token on the
 * remote URL. The `git remote set-url` lives in the script (ADR-0189), so the
 * workflow is found through the scripts it runs.
 */
function pushesWithTokenOnRemote(text: string): boolean {
	const scripts = [...text.matchAll(/node (?:\.\.\/base\/)?(tools\/[\w/.-]+\.ts)/g)].map((match) => match[1])
	return scripts.some((script) => {
		try {
			return /'remote',\s*'set-url',\s*'origin',\s*`https:\/\/x-access-token:/.test(readFileSync(join(REPO, script), 'utf8'))
		} catch {
			return false
		}
	})
}

// A workflow that puts a token on the remote URL does so to push as that
// token. actions/checkout's persisted GITHUB_TOKEN header outranks it, so the
// push lands as github-actions[bot] and its CI waits for approval (lesson 0018).
describe('a workflow that pushes with a token on the remote URL', () => {
	const pushers = readdirSync(WORKFLOWS)
		.filter((file) => /\.ya?ml$/.test(file))
		.filter((file) => pushesWithTokenOnRemote(readFileSync(join(WORKFLOWS, file), 'utf8')))

	it('exists, so this check is looking at something', () => {
		assert.ok(pushers.length > 0)
	})

	for (const workflow of pushers) {
		it(`${workflow} checks out without persisting credentials`, () => {
			const steps = checkoutSteps(readFileSync(join(WORKFLOWS, workflow), 'utf8'))
			assert.ok(steps.length > 0, `${workflow} has no checkout step`)
			for (const step of steps) {
				assert.match(step, /persist-credentials: false/, `${workflow}:\n${step}`)
			}
		})
	}
})
