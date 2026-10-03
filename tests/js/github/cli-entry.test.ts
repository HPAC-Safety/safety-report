import { describe, it } from 'node:test'
import assert from 'node:assert/strict'
import { mkdtempSync } from 'node:fs'
import { spawnSync } from 'node:child_process'
import { tmpdir } from 'node:os'
import path from 'node:path'

import type { Exec } from '../../../tools/lib/actions.ts'
import { main as checkLinkedIssue } from '../../../tools/github/check-linked-issue.ts'
import { main as createRelease } from '../../../tools/github/create-release.ts'
import { main as commitToPrBranch } from '../../../tools/github/commit-to-pr-branch.ts'
import { main as checkWorkflowSteps, commandLines, runSteps } from '../../../tools/github/check-workflow-steps.ts'

const tools = path.resolve(import.meta.dirname, '../../../tools/github')

const spawn = (script: string, args: string[] = [], { env = {}, cwd }: { env?: Record<string, string>; cwd?: string } = {}) =>
	spawnSync(process.execPath, [path.join(tools, script), ...args], {
		cwd,
		env: { PATH: process.env.PATH, NODE_V8_COVERAGE: process.env.NODE_V8_COVERAGE, ...env },
		encoding: 'utf8',
	})

describe('github scripts run as commands', () => {
	it('check-linked-issue reads its environment and exits with the verdict', () => {
		const passed = spawn('check-linked-issue.ts', [], { env: { PR_BODY: 'Closes #1' } })
		assert.equal(passed.status, 0)
		assert.match(passed.stdout, /Linked: Closes #1/)
		assert.equal(spawn('check-linked-issue.ts').status, 1)
	})

	it('check-linked-issue treats an unset environment as an empty body', () => {
		const out: string[] = []
		assert.equal(checkLinkedIssue({ env: {}, log: (line) => out.push(line) }), 1)
		assert.equal(out[0], '::error::This pull request does not close an issue.')
	})

	it('check-merge-group-session-links refuses without BASE_SHA', () => {
		const result = spawn('check-merge-group-session-links.ts')
		assert.notEqual(result.status, 0)
		assert.match(result.stderr, /BASE_SHA is not set/)
	})

	it('check-pr-screenshots-diff refuses without BASE_SHA', () => {
		const result = spawn('check-pr-screenshots-diff.ts')
		assert.notEqual(result.status, 0)
		assert.match(result.stderr, /BASE_SHA is not set/)
	})

	it('read-node-major prints usage without a file', () => {
		const result = spawn('read-node-major.ts')
		assert.equal(result.status, 1)
		assert.match(result.stdout, /Usage: node tools\/github\/read-node-major.ts/)
	})

	it('find-release-run refuses without REPO', () => {
		const result = spawn('find-release-run.ts')
		assert.notEqual(result.status, 0)
		assert.match(result.stderr, /REPO is not set/)
	})

	it('create-release refuses without REPO', () => {
		const result = spawn('create-release.ts')
		assert.notEqual(result.status, 0)
		assert.match(result.stderr, /REPO is not set/)
	})

	it('create-release takes the clock from the system when none is given', () => {
		const calls: (readonly string[])[] = []
		const exec: Exec = (command, args = []) => {
			calls.push([command, ...args])
			return { status: 0, stdout: '', stderr: '' }
		}
		const code = createRelease({
			env: { REPO: 'o/r', REF: 'refs/heads/main', SHA: 'abc', SERVER: 'https://github.com', RUN_ID: '9' },
			exec,
			log: () => {},
		})
		assert.equal(code, 0)
		assert.match(calls[0][2], /^repos\/o\/r\/git\/matching-refs\/tags\/\d{4}\.\d{2}\.\d{2}-$/)
		assert.match(calls[1][3], /^\d{4}\.\d{2}\.\d{2}-1$/)
	})

	it('commit-to-pr-branch prints usage without options', () => {
		const result = spawn('commit-to-pr-branch.ts')
		assert.equal(result.status, 1)
		assert.match(result.stdout, /Usage: node tools\/github\/commit-to-pr-branch.ts/)
	})

	it('commit-to-pr-branch accepts no add-if-exists, an unset body variable, and a silent git failure', () => {
		const out: string[] = []
		const calls: (readonly string[])[] = []
		const exec: Exec = (_command, args = []) => {
			calls.push(args)
			return { status: 0, stdout: '', stderr: '' }
		}
		const code = commitToPrBranch({
			argv: ['--adr', 'ADR-1', '--add', 'a.txt', '--subject', 's', '--paths', 'p', '--body-env', 'UNSET_BODY'],
			env: { GH_TOKEN: 't', GITHUB_REPOSITORY: 'o/r', HEAD_REF: 'b', HEAD_SHA: 'c' },
			exec,
			log: (line) => out.push(line),
			push: () => 0,
			cwd: '.',
		})
		assert.equal(code, 0)
		assert.deepEqual(calls.at(-1), ['commit', '-m', 's', '-m', '.'])

		const failed = commitToPrBranch({
			argv: ['--adr', 'ADR-1', '--add', 'a.txt', '--subject', 's', '--paths', 'p'],
			env: { GH_TOKEN: 't', GITHUB_REPOSITORY: 'o/r' },
			exec: () => ({ status: 1, stdout: '', stderr: '' }),
			log: (line) => out.push(line),
			push: () => 0,
			cwd: '.',
		})
		assert.equal(failed, 1)
		assert.equal(out.at(-1), '::error::git config failed')
	})

	it('check-workflow-steps checks the working directory by default', () => {
		const empty = mkdtempSync(path.join(tmpdir(), 'cws-'))
		const result = spawn('check-workflow-steps.ts', [], { cwd: empty })
		assert.equal(result.status, 0)
		assert.match(result.stdout, /0 run step\(s\), each one command/)
		assert.equal(checkWorkflowSteps(empty, () => {}), 0)
	})

	it('check-workflow-steps reads a double-quoted run, an all-blank block, and a trailing continuation', () => {
		assert.deepEqual(runSteps('run: "echo hi"\n'), [{ line: 1, script: 'echo hi' }])
		assert.deepEqual(runSteps('  - run: |\n\n\n  next: 1\n'), [{ line: 1, script: '' }])
		assert.deepEqual(commandLines('echo one \\'), ['echo one'])
	})
})
