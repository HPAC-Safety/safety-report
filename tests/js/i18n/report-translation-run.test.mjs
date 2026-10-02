import { describe, it } from 'node:test'
import assert from 'node:assert/strict'
import { mkdtempSync, readFileSync, writeFileSync } from 'node:fs'
import { tmpdir } from 'node:os'
import path from 'node:path'

import { describeEvent, errorFromLog, failedSteps, keyList, main, verdictFor } from '../../../tools/i18n/report-translation-run.mjs'

describe('translation run report pieces', () => {
	it('describes a pull request or a branch event', () => {
		assert.equal(describeEvent({ PR_NUMBER: '12' }), 'pull request #12')
		assert.equal(describeEvent({ PR_NUMBER: '', GITHUB_EVENT_NAME: 'push', GITHUB_REF_NAME: 'main' }), 'push on main')
	})

	it('lists keys as backticked bullets', () => {
		assert.equal(keyList('a.b,c'), '- `a.b`\n- `c`')
		assert.equal(keyList(''), '')
	})

	it('words the verdict by the check outcome', () => {
		assert.match(verdictFor('success'), /passed on the result/)
		assert.match(verdictFor('failure'), /\*\*failed\*\*.*stays open/)
		assert.match(verdictFor('skipped'), /\*\*failed\*\*/)
	})

	it('names only the steps that failed', () => {
		assert.equal(failedSteps('node=success translate=failure commit_to_pr=skipped open_pr=failure'), 'translate,open_pr')
		assert.equal(failedSteps('node=success'), '')
	})

	it('quotes the provider status line, else the last five lines', () => {
		assert.equal(errorFromLog('a\nThe translation provider answered 400 bad\nb\n'), 'The translation provider answered 400 bad')
		assert.equal(errorFromLog('1\n2\n3\n4\n5\n6\n7\n'), '3\n4\n5\n6\n7')
		assert.equal(errorFromLog(''), '')
	})
})

function setup(env, { issues = '7\n9', log } = {}) {
	const dir = mkdtempSync(path.join(tmpdir(), 'rtr-'))
	if (log !== undefined) writeFileSync(path.join(dir, 'translate.log'), log)
	const calls = []
	const comments = []
	const exec = (command, args) => {
		calls.push([command, ...args])
		if (args[1] === 'list') return { status: 0, stdout: issues, stderr: '' }
		if (args[1] === 'comment') comments.push(readFileSync(args[args.indexOf('--body-file') + 1], 'utf8'))
		return { status: 0, stdout: '', stderr: '' }
	}
	const logged = []
	const base = { GITHUB_REPOSITORY: 'o/r', RUNNER_TEMP: dir, RUN_URL: 'https://x/run/1', PR_NUMBER: '', GITHUB_EVENT_NAME: 'push', GITHUB_REF_NAME: 'main', ...env }
	return { calls, comments, logged, run: (mode) => main({ argv: [mode], env: base, exec, log: (l) => logged.push(l) }) }
}

describe('report translation run', () => {
	it('does nothing when no issue is waiting', () => {
		const t = setup({}, { issues: '' })
		assert.equal(t.run('success'), 0)
		assert.deepEqual(t.logged, ['No open issue is waiting on a translation run.'])
		assert.equal(t.calls.length, 1)
	})

	it('comments on every waiting issue and closes them when the check passed', () => {
		const t = setup({ TRANSLATED: '2', TRANSLATED_KEYS: 'a,b', CHECK_OUTCOME: 'success', PROVIDER: 'gemini-3.7-flash' })
		assert.equal(t.run('success'), 0)
		assert.equal(
			t.comments[0],
			'A translation run called the provider (`gemini-3.7-flash`) and succeeded.\n\n' +
				'- Run: https://x/run/1\n- Event: push on main\n- Keys sent to the provider: 2\n\n' +
				'- `a`\n- `b`\n\n' +
				'`translate-locale.mjs --check` passed on the result, including the term-list rule in `locales/terms.json`.\n\n' +
				'_Posted by `.github/workflows/i18n-translate.yml` to every open issue labelled `verify:translation-run`._\n',
		)
		const verbs = t.calls.map((call) => call.slice(0, 3).join(' '))
		assert.deepEqual(verbs.slice(1), ['gh issue comment', 'gh issue close', 'gh issue comment', 'gh issue close'])
		assert.deepEqual(t.calls[2], ['gh', 'issue', 'close', '7', '--repo', 'o/r', '--reason', 'completed'])
	})

	it('leaves the issues open when the check failed', () => {
		const t = setup({ TRANSLATED: '1', TRANSLATED_KEYS: 'a', CHECK_OUTCOME: 'failure', PROVIDER: 'p' })
		assert.equal(t.run('success'), 0)
		assert.ok(t.calls.every((call) => call[2] !== 'close'))
		assert.match(t.comments[0], /\*\*failed\*\*/)
	})

	it('quotes the provider error and names the failed step on failure', () => {
		const t = setup({ OUTCOMES: 'node=success translate=failure' }, { log: 'x\nThe translation provider answered 400 nope\n' })
		assert.equal(t.run('failure'), 0)
		assert.equal(
			t.comments[0],
			'A translation run **failed**.\n\n- Run: https://x/run/1\n- Event: push on main\n- Failed step(s): translate\n\n' +
				'```\nThe translation provider answered 400 nope\n```\n\nThis issue stays open.\n\n' +
				'_Posted by `.github/workflows/i18n-translate.yml` to every open issue labelled `verify:translation-run`._\n',
		)
		assert.ok(t.calls.every((call) => call[2] !== 'close'))
	})

	it('says a step before translation failed when no step reports failure, and omits the quote without a log', () => {
		const t = setup({ OUTCOMES: 'node=success', PR_NUMBER: '4' })
		assert.equal(t.run('failure'), 0)
		assert.match(t.comments[0], /- Event: pull request #4\n- Failed step\(s\): a step before translation\n\nThis issue stays open/)
		assert.doesNotMatch(t.comments[0], /```/)
	})

	it('rejects an unknown mode', () => {
		assert.equal(setup({}).run('other'), 1)
	})
})
