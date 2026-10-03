import { describe, it } from 'node:test'
import assert from 'node:assert/strict'
import { mkdtempSync } from 'node:fs'
import { tmpdir } from 'node:os'
import { join } from 'node:path'

import { type ClaimInput, engines, judge, main, readMessages, statusOf, summary } from '../../../tools/spec/check-claim-results.ts'

/** A Cucumber Messages stream: each pickle's tags and the attempts it ran, as step statuses. */
function messages(pickles: { id: string; tags: string[]; attempts?: string[][] }[]): string {
	const lines: object[] = []
	for (const pickle of pickles) lines.push({ pickle: { id: pickle.id, tags: pickle.tags.map((name) => ({ name })) } })
	for (const pickle of pickles) {
		if (!pickle.attempts) continue
		lines.push({ testCase: { id: `case-${pickle.id}`, pickleId: pickle.id } })
		for (const [attempt, steps] of pickle.attempts.entries()) {
			const started = `${pickle.id}-${attempt}`
			lines.push({ testCaseStarted: { id: started, testCaseId: `case-${pickle.id}`, attempt } })
			for (const status of steps) lines.push({ testStepFinished: { testCaseStartedId: started, testStepResult: { status } } })
			lines.push({ testCaseFinished: { testCaseStartedId: started, willBeRetried: attempt < pickle.attempts.length - 1 } })
		}
	}
	return lines.map((line) => JSON.stringify(line)).join('\n')
}

const claim = (id: string, engine: ClaimInput['engine'] = 'Reqnroll', status: ClaimInput['status'] = 'Built'): ClaimInput => ({
	id,
	title: `Scenario ${id}`,
	file: '.spec/features/media/media.feature',
	engine,
	status,
})

describe('readMessages', () => {
	it('keys each pickle by its claim tag and takes a test case its worst step', () => {
		const { rows } = readMessages(
			messages([
				{ id: 'p1', tags: ['@REQ-MED-001', '@ui'], attempts: [['PASSED', 'PASSED']] },
				{ id: 'p2', tags: ['@REQ-MED-002'], attempts: [['PASSED', 'FAILED', 'SKIPPED']] },
			]),
		)
		assert.deepEqual(rows.get('REQ-MED-001'), ['PASSED'])
		assert.deepEqual(rows.get('REQ-MED-002'), ['FAILED'])
	})

	it('takes a retried scenario from its last attempt', () => {
		const { rows } = readMessages(messages([{ id: 'p1', tags: ['@REQ-MED-001'], attempts: [['FAILED'], ['PASSED']] }]))
		assert.deepEqual(rows.get('REQ-MED-001'), ['PASSED'])
	})

	it('records a pickle that never ran as not run', () => {
		const { rows } = readMessages(messages([{ id: 'p1', tags: ['@REQ-MED-001'] }]))
		assert.deepEqual(rows.get('REQ-MED-001'), ['NOT RUN'])
	})

	it('records every Examples row of an outline', () => {
		const { rows } = readMessages(
			messages([
				{ id: 'row1', tags: ['@REQ-MED-001'], attempts: [['PASSED']] },
				{ id: 'row2', tags: ['@REQ-MED-001'] },
			]),
		)
		assert.deepEqual(rows.get('REQ-MED-001'), ['PASSED', 'NOT RUN'])
	})

	it('reports a line that is not JSON', () => {
		assert.deepEqual(readMessages('{}\nnot json\n').problems, ['line 2 is not JSON'])
	})
})

describe('statusOf', () => {
	it('passes only when every row passed', () => {
		assert.equal(statusOf(['PASSED', 'PASSED']), 'Passing')
		assert.equal(statusOf(['PASSED', 'SKIPPED']), 'Unexecuted')
		assert.equal(statusOf(['PASSED', 'NOT RUN']), 'Unexecuted')
		assert.equal(statusOf(['UNKNOWN']), 'Unexecuted')
		assert.equal(statusOf([]), 'Unexecuted')
		assert.equal(statusOf(undefined), 'Unexecuted')
	})

	it('fails a row that failed, was undefined, ambiguous, or pending', () => {
		for (const status of ['FAILED', 'UNDEFINED', 'AMBIGUOUS', 'PENDING']) assert.equal(statusOf(['PASSED', status]), 'Failing')
	})
})

describe('judge', () => {
	const stream = messages([
		{ id: 'p1', tags: ['@REQ-MED-001'], attempts: [['PASSED']] },
		{ id: 'p2', tags: ['@REQ-MED-002'], attempts: [['FAILED']] },
	])

	it('judges a built claim from its own engine only', () => {
		const { results } = judge([claim('REQ-MED-001'), claim('REQ-MED-002'), claim('REQ-MED-003'), claim('REQ-WLD-001', 'playwright-bdd')], new Map([['Reqnroll', stream]]))
		assert.deepEqual(
			results.map((result) => [result.id, result.status]),
			[
				['REQ-MED-001', 'Passing'],
				['REQ-MED-002', 'Failing'],
				['REQ-MED-003', 'Unexecuted'],
				['REQ-WLD-001', 'NotRun'],
			],
		)
	})

	it('never judges a planned claim', () => {
		const { results } = judge([claim('REQ-MED-009', 'Reqnroll', 'Planned')], new Map([['Reqnroll', stream]]))
		assert.equal(results[0]?.status, 'Planned')
	})

	it('fails every built claim of an engine whose results are missing', () => {
		const { results } = judge([claim('REQ-MED-001')], new Map([['Reqnroll', null]]))
		assert.equal(results[0]?.status, 'Unexecuted')
	})
})

describe('engines', () => {
	it('reads --results pairs', () => {
		assert.deepEqual([...(engines(['--results', 'Reqnroll=a.ndjson', '--results', 'playwright-bdd=b.ndjson'], {}) as Map<string, string>)], [
			['Reqnroll', 'a.ndjson'],
			['playwright-bdd', 'b.ndjson'],
		])
	})

	it('falls back to the environment, skipping an empty variable', () => {
		assert.deepEqual([...(engines([], { REQNROLL_RESULTS: 'a.ndjson', PLAYWRIGHT_BDD_RESULTS: '' }) as Map<string, string>)], [['Reqnroll', 'a.ndjson']])
	})

	it('refuses an unknown engine', () => {
		assert.match(engines(['--results', 'Cucumber=a'], {}) as string, /engine one of Reqnroll, playwright-bdd/)
	})
})

describe('summary', () => {
	it('lists the claims that failed the gate and says which engines were judged', () => {
		const claims = [claim('REQ-MED-001'), claim('REQ-WLD-001', 'playwright-bdd')]
		const text = summary(claims, judge(claims, new Map([['Reqnroll', '']])).results, new Map([['Reqnroll', '']]))
		assert.match(text, /\| Reqnroll \| yes \| 0 \| 0 \| 1 \| 0 \|/)
		assert.match(text, /\| playwright-bdd \| no — its job did not run \|/)
		assert.match(text, /\| REQ-MED-001 \| Reqnroll \| Unexecuted \| — \| Scenario REQ-MED-001 \|/)
		assert.match(text, /Every claim/)
		assert.doesNotMatch(summary(claims, [], new Map(), { every: false }), /Every claim/)
	})
})

describe('main', () => {
	const claims = JSON.stringify({ claims: [claim('REQ-MED-001'), claim('REQ-MED-002'), claim('REQ-WLD-001', 'playwright-bdd')] })
	const run = (argv: string[], files: Record<string, string>, env: Record<string, string> = {}): { code: number; output: string[]; written: Map<string, string> } => {
		const output: string[] = []
		const written = new Map<string, string>()
		// The job summary goes to a file of its own, as on a runner.
		const summaryFile = join(mkdtempSync(join(tmpdir(), 'claim-results-')), 'summary.md')
		const code = main({ argv, env: { ...env, GITHUB_STEP_SUMMARY: summaryFile }, claims, read: (path) => files[path] ?? null, write: (path, text) => written.set(path, text), log: (line) => output.push(line) })
		return { code, output, written }
	}
	const passing = messages([
		{ id: 'p1', tags: ['@REQ-MED-001'], attempts: [['PASSED']] },
		{ id: 'p2', tags: ['@REQ-MED-002'], attempts: [['PASSED']] },
	])

	it('passes when every built claim of every judged engine passed', () => {
		const { code, output, written } = run(['--results', 'Reqnroll=r.ndjson', '--out', 'out.json'], { 'r.ndjson': passing })
		assert.equal(code, 0)
		assert.match(output.join('\n'), /2 of 2 built claims judged in this run passed \(Reqnroll\)/)
		const out = JSON.parse(written.get('out.json') ?? '{}') as { engines: Record<string, boolean>; claims: { id: string; status: string }[] }
		assert.deepEqual(out.engines, { Reqnroll: true })
		assert.deepEqual(out.claims.map((result) => result.status), ['Passing', 'Passing', 'NotRun'])
	})

	it('fails a built claim with no passing execution, naming it and its file', () => {
		const stream = messages([{ id: 'p1', tags: ['@REQ-MED-001'], attempts: [['PASSED']] }])
		const { code, output } = run([], { 'r.ndjson': stream }, { REQNROLL_RESULTS: 'r.ndjson' })
		assert.equal(code, 1)
		assert.match(output.join('\n'), /::error file=\.spec\/features\/media\/media\.feature::REQ-MED-002 \(Reqnroll\) has no passing execution/)
	})

	it('fails a failing claim', () => {
		const stream = messages([
			{ id: 'p1', tags: ['@REQ-MED-001'], attempts: [['FAILED']] },
			{ id: 'p2', tags: ['@REQ-MED-002'], attempts: [['PASSED']] },
		])
		const { code, output } = run(['--results', 'Reqnroll=r.ndjson'], { 'r.ndjson': stream })
		assert.equal(code, 1)
		assert.match(output.join('\n'), /REQ-MED-001 \(Reqnroll\) failed/)
	})

	it('fails an engine named whose results file is missing', () => {
		const { code, output } = run(['--results', 'playwright-bdd=missing.ndjson'], {})
		assert.equal(code, 1)
		assert.match(output.join('\n'), /playwright-bdd was named but missing\.ndjson does not exist/)
	})

	it('refuses to judge nothing', () => {
		assert.equal(run([], {}).code, 2)
	})

	it('refuses a malformed --results', () => {
		assert.equal(run(['--results', 'Reqnroll'], {}).code, 2)
	})
})
