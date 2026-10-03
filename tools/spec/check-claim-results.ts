#!/usr/bin/env node
// A built claim counts only when its scenario passed in this run (ADR-0195).
//
// Both engines write Cucumber Messages, one envelope per line: Reqnroll's
// `message` formatter (REQNROLL_FORMATTERS, set by ci.yml's `test` job) and
// playwright-bdd's `cucumberReporter('message')` (tests/e2e/playwright.config.ts).
// A pickle carries its scenario's tags, so each execution is keyed by the
// claim's `@REQ-*` tag — no title matching, no test-name parsing. This joins
// those executions with .spec/claims.json and fails when a built claim (not
// `@ignore`) of an engine that ran has no passing execution, or any failing one.
//
// Per claim:
//   Passing     every pickle (one per Examples row of an outline) ran, and its
//               final attempt passed;
//   Failing     a pickle's final attempt failed (failed, ambiguous, undefined,
//               or pending step);
//   Unexecuted  no pickle ran, or one was skipped or never reached a step.
//
// Only an engine named on the command line or in the environment is judged.
// ci.yml names Reqnroll whenever `test` ran and playwright-bdd whenever `e2e`
// ran; an engine whose job the path filter skipped is reported, not judged.
// A named engine whose results file is missing fails every built claim of it.
//
// The results are per run, so nothing here is committed: the verdict goes to
// the log, the job summary, and `--out`, which ci.yml uploads as an artifact.
//
//   node tools/spec/check-claim-results.ts \
//     [--results Reqnroll=<ndjson>] [--results playwright-bdd=<ndjson>] [--out <json>]
//
// Environment, used when no --results is given: REQNROLL_RESULTS and
// PLAYWRIGHT_BDD_RESULTS, each a path or empty.
//
// The exit code is the contract.
import { existsSync, mkdirSync, readFileSync, writeFileSync } from 'node:fs'
import { dirname } from 'node:path'

import { type Env, annotation, appendSummary, isMain } from '../lib/actions.ts'
import { CLAIMS } from './spec-paths.ts'

export type Engine = 'Reqnroll' | 'playwright-bdd'
export const ENGINES: readonly Engine[] = ['Reqnroll', 'playwright-bdd']

/** The per-run status of one claim. `Planned` and `NotRun` are never failures. */
export type RunStatus = 'Passing' | 'Failing' | 'Unexecuted' | 'Planned' | 'NotRun'

/** What this tool reads of .spec/claims.json. */
export interface ClaimInput {
	id: string
	title: string
	file: string
	engine: Engine
	status: 'Built' | 'Planned'
}

/** One claim's verdict for this run. */
export interface ClaimResult {
	id: string
	title: string
	file: string
	engine: Engine
	status: RunStatus
	/** Each pickle's final outcome, in the order the run reported it. */
	rows: string[]
}

const CLAIM = /^@(REQ-[A-Z]+-\d{3})$/

// Cucumber's own ordering: a test case's status is its worst step's.
const SEVERITY = ['UNKNOWN', 'PASSED', 'SKIPPED', 'PENDING', 'UNDEFINED', 'AMBIGUOUS', 'FAILED'] as const
type StepStatus = (typeof SEVERITY)[number]
const FAILING: ReadonlySet<string> = new Set(['PENDING', 'UNDEFINED', 'AMBIGUOUS', 'FAILED'])

const worse = (a: StepStatus, b: string): StepStatus => {
	const index = SEVERITY.indexOf(b as StepStatus)
	return index > SEVERITY.indexOf(a) ? SEVERITY[index] : a
}

interface Envelope {
	pickle?: { id: string; tags: { name: string }[] }
	testCase?: { id: string; pickleId: string }
	testCaseStarted?: { id: string; testCaseId: string; attempt?: number }
	testStepFinished?: { testCaseStartedId: string; testStepResult: { status: string } }
	testCaseFinished?: { testCaseStartedId: string; willBeRetried?: boolean }
}

/**
 * Every claim's pickles in one Cucumber Messages stream, each with the final
 * outcome of its last attempt: `PASSED`, a failing status, or `SKIPPED` /
 * `UNKNOWN` / `NOT RUN` when it never got that far.
 */
export function readMessages(ndjson: string): { rows: Map<string, string[]>; problems: string[] } {
	const pickleClaims = new Map<string, string[]>()
	const casePickle = new Map<string, string>()
	// Each attempt: its test case, and its worst step so far.
	const attempts = new Map<string, { testCase: string; status: StepStatus }>()
	const retried = new Set<string>()
	const problems: string[] = []

	for (const [index, line] of ndjson.split('\n').entries()) {
		if (line.trim() === '') continue
		let envelope: Envelope
		try {
			envelope = JSON.parse(line) as Envelope
		} catch {
			problems.push(`line ${index + 1} is not JSON`)
			continue
		}
		if (envelope.pickle) {
			pickleClaims.set(
				envelope.pickle.id,
				envelope.pickle.tags.map((tag) => CLAIM.exec(tag.name)?.[1]).filter((id): id is string => id !== undefined),
			)
		} else if (envelope.testCase) {
			casePickle.set(envelope.testCase.id, envelope.testCase.pickleId)
		} else if (envelope.testCaseStarted) {
			attempts.set(envelope.testCaseStarted.id, { testCase: envelope.testCaseStarted.testCaseId, status: 'UNKNOWN' })
		} else if (envelope.testStepFinished) {
			const attempt = attempts.get(envelope.testStepFinished.testCaseStartedId)
			if (attempt) attempt.status = worse(attempt.status, envelope.testStepFinished.testStepResult.status)
		} else if (envelope.testCaseFinished?.willBeRetried) {
			retried.add(envelope.testCaseFinished.testCaseStartedId)
		}
	}

	// A pickle's outcome is its last attempt's: a retried attempt that a later
	// one replaced is not the verdict, as it is not Playwright's.
	const outcome = new Map<string, string>()
	for (const [started, { testCase, status }] of attempts) {
		if (retried.has(started)) continue
		const pickle = casePickle.get(testCase)
		if (pickle === undefined) continue
		const previous = outcome.get(pickle)
		// The same pickle run twice to completion (two projects, say): any
		// failure is the verdict, then any non-pass.
		if (previous === undefined || FAILING.has(status) || (previous === 'PASSED' && status !== 'PASSED')) outcome.set(pickle, status)
	}

	const rows = new Map<string, string[]>()
	for (const [pickle, claims] of pickleClaims) {
		for (const claim of claims) rows.set(claim, [...(rows.get(claim) ?? []), outcome.get(pickle) ?? 'NOT RUN'])
	}
	return { rows, problems }
}

/** A claim's status from its rows. */
export function statusOf(rows: readonly string[] | undefined): Extract<RunStatus, 'Passing' | 'Failing' | 'Unexecuted'> {
	if (!rows || rows.length === 0) return 'Unexecuted'
	if (rows.some((row) => FAILING.has(row))) return 'Failing'
	return rows.every((row) => row === 'PASSED') ? 'Passing' : 'Unexecuted'
}

/**
 * Every claim's verdict. `results` holds, for each engine judged in this run,
 * its message stream, or null when the file it named is missing.
 */
export function judge(claims: readonly ClaimInput[], results: ReadonlyMap<Engine, string | null>): { results: ClaimResult[]; problems: string[] } {
	const problems: string[] = []
	const rowsByEngine = new Map<Engine, Map<string, string[]>>()
	for (const [engine, ndjson] of results) {
		if (ndjson === null) continue
		const read = readMessages(ndjson)
		rowsByEngine.set(engine, read.rows)
		problems.push(...read.problems.map((problem) => `${engine} results: ${problem}`))
	}

	return {
		problems,
		results: claims.map((claim): ClaimResult => {
			const { id, title, file, engine } = claim
			if (claim.status === 'Planned') return { id, title, file, engine, status: 'Planned', rows: [] }
			if (!results.has(engine)) return { id, title, file, engine, status: 'NotRun', rows: [] }
			const rows = rowsByEngine.get(engine)?.get(id) ?? []
			return { id, title, file, engine, status: statusOf(rows), rows }
		}),
	}
}

const failed = (result: ClaimResult): boolean => result.status === 'Failing' || result.status === 'Unexecuted'

/** The job summary: totals per engine, every claim that fails the gate, then every claim. */
export function summary(results: readonly ClaimResult[], judged: ReadonlyMap<Engine, string | null>, { every = true }: { every?: boolean } = {}): string {
	const lines = ['## Claim results', '', '| Engine | Judged | Passing | Failing | Unexecuted | Planned |', '|---|---|---|---|---|---|']
	for (const engine of ENGINES) {
		const mine = results.filter((result) => result.engine === engine)
		const count = (status: RunStatus): number => mine.filter((result) => result.status === status).length
		const judgedText = !judged.has(engine) ? 'no — its job did not run' : judged.get(engine) === null ? 'yes — results missing' : 'yes'
		lines.push(`| ${engine} | ${judgedText} | ${count('Passing')} | ${count('Failing')} | ${count('Unexecuted')} | ${count('Planned')} |`)
	}

	const failures = results.filter(failed)
	if (failures.length > 0) {
		lines.push('', '### Built claims with no passing run', '', '| Claim | Engine | Status | Rows | Scenario |', '|---|---|---|---|---|')
		for (const result of failures) {
			lines.push(`| ${result.id} | ${result.engine} | ${result.status} | ${result.rows.join(', ') || '—'} | ${result.title.replace(/\|/g, '\\|')} |`)
		}
	}

	if (every) {
		lines.push('', '<details><summary>Every claim</summary>', '', '| Claim | Engine | Status |', '|---|---|---|')
		for (const result of results) lines.push(`| ${result.id} | ${result.engine} | ${result.status} |`)
		lines.push('', '</details>')
	}
	lines.push('')
	return lines.join('\n')
}

/** Which engines to judge, and where each one's results are. */
export function engines(argv: readonly string[], env: Env): Map<Engine, string> | string {
	const named = new Map<Engine, string>()
	for (const [index, arg] of argv.entries()) {
		if (arg !== '--results') continue
		const value = argv[index + 1] ?? ''
		const [engine, path] = [value.slice(0, value.indexOf('=')), value.slice(value.indexOf('=') + 1)]
		if (!ENGINES.includes(engine as Engine) || path === '' || !value.includes('=')) return `--results takes <engine>=<path>, engine one of ${ENGINES.join(', ')}; got "${value}"`
		named.set(engine as Engine, path)
	}
	if (named.size > 0) return named
	if (env.REQNROLL_RESULTS) named.set('Reqnroll', env.REQNROLL_RESULTS)
	if (env.PLAYWRIGHT_BDD_RESULTS) named.set('playwright-bdd', env.PLAYWRIGHT_BDD_RESULTS)
	return named
}

export interface MainOptions {
	argv?: readonly string[]
	env?: Env
	claims?: string
	read?: (path: string) => string | null
	write?: (path: string, text: string) => void
	log?: (line: string) => void
}

const readIfPresent = (path: string): string | null => (existsSync(path) ? readFileSync(path, 'utf8') : null)

export function main({
	argv = process.argv.slice(2),
	env = process.env,
	claims = readFileSync(CLAIMS, 'utf8'),
	read = readIfPresent,
	write = (path, text) => {
		mkdirSync(dirname(path), { recursive: true })
		writeFileSync(path, text)
	},
	log = console.log,
}: MainOptions = {}): number {
	const named = engines(argv, env)
	if (typeof named === 'string') {
		log(annotation('error', named))
		return 2
	}
	if (named.size === 0) {
		log(annotation('error', 'No engine named: pass --results <engine>=<path>, or set REQNROLL_RESULTS or PLAYWRIGHT_BDD_RESULTS. A gate that judges nothing passes nothing.'))
		return 2
	}

	const judged = new Map<Engine, string | null>()
	for (const [engine, path] of named) {
		const text = read(path)
		judged.set(engine, text)
		if (text === null) log(annotation('error', `${engine} was named but ${path} does not exist, so none of its built claims ran.`))
	}

	const input = (JSON.parse(claims) as { claims: ClaimInput[] }).claims
	const verdict = judge(input, judged)
	for (const problem of verdict.problems) log(annotation('error', problem))

	const failures = verdict.results.filter(failed)
	for (const result of failures) {
		const why = result.status === 'Failing' ? 'failed' : 'has no passing execution'
		log(annotation('error', `${result.id} (${result.engine}) ${why}: "${result.title}". Rows: ${result.rows.join(', ') || 'none ran'}.`, { file: result.file }))
	}

	// The job summary lists every claim; a log (act keeps no summary) only what failed.
	appendSummary(summary(verdict.results, judged, { every: Boolean(env.GITHUB_STEP_SUMMARY) }), env)
	const out = argv[argv.indexOf('--out') + 1]
	if (argv.includes('--out') && out) write(out, `${JSON.stringify({ engines: Object.fromEntries([...judged].map(([engine, ndjson]) => [engine, ndjson !== null])), claims: verdict.results }, null, '\t')}\n`)

	const passing = verdict.results.filter((result) => result.status === 'Passing').length
	const built = verdict.results.filter((result) => result.status !== 'Planned' && result.status !== 'NotRun').length
	log(`${passing} of ${built} built claims judged in this run passed (${[...judged.keys()].join(', ')}).`)
	return failures.length > 0 || verdict.problems.length > 0 ? 1 : 0
}

if (isMain(import.meta.url)) process.exit(main())
