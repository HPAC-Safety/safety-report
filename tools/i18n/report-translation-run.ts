#!/usr/bin/env node
// Reports a translation run's outcome to every open issue labelled
// `verify:translation-run` (i18n-translate.yml; ADR-0021).
//
// Some issues can only be settled by a run that actually calls the provider
// (e.g. #614: does Gemini follow the term list?). Nothing has to poll for a run
// nobody can schedule:
//
//   success  The run exercised the provider (`translated`, not `keys`, says so:
//            `keys` also counts glossary pins, hand-edits recorded as human,
//            and removals, none of which reach a provider). It comments with
//            what happened and closes the issue if the i18n check passed on the
//            result; when the check failed it says so and leaves the issue open.
//   failure  A failed run is exactly what some of those issues wait to hear
//            about — a provider rejecting a request shape answers with a status
//            line the tool prints ("The translation provider answered 400 …").
//            It quotes that line (or the log's last five lines), names the
//            failed step, and leaves the issue open for a person.
//
//   node tools/i18n/report-translation-run.ts success|failure
//
// Environment, both modes: GH_TOKEN, GITHUB_REPOSITORY, GITHUB_EVENT_NAME,
// GITHUB_REF_NAME, RUN_URL, PR_NUMBER, RUNNER_TEMP.
//   success: TRANSLATED, TRANSLATED_KEYS, CHECK_OUTCOME, PROVIDER.
//   failure: OUTCOMES (`step=outcome` pairs).
import { existsSync, mkdtempSync, readFileSync, writeFileSync } from 'node:fs'
import { tmpdir } from 'node:os'
import path from 'node:path'

import { exec, isMain, required, type Env, type Exec } from '../lib/actions.ts'

const POSTED_BY = '_Posted by `.github/workflows/i18n-translate.yml` to every open issue labelled `verify:translation-run`._\n'

/** The run's trigger, in words. */
export function describeEvent(env: Env): string {
	return env.PR_NUMBER ? `pull request #${env.PR_NUMBER}` : `${env.GITHUB_EVENT_NAME} on ${env.GITHUB_REF_NAME}`
}

/** The keys sent to the provider, one backticked bullet each. */
export function keyList(translatedKeys: string): string {
	if (!translatedKeys) return ''
	return translatedKeys.split(',').map((key) => `- \`${key}\``).join('\n')
}

/** What the i18n check's outcome means for the issue. */
export function verdictFor(checkOutcome: string | undefined): string {
	return checkOutcome === 'success'
		? '`translate-locale.ts --check` passed on the result, including the term-list rule in `locales/terms.json`.'
		: '`translate-locale.ts --check` **failed** on the result — see the run log. This issue stays open.'
}

export function successReport({
	provider,
	runUrl,
	event,
	translated,
	keys,
	verdict,
}: {
	provider: string
	runUrl: string
	event: string
	translated: string
	keys: string
	verdict: string
}): string {
	return (
		`A translation run called the provider (\`${provider}\`) and succeeded.\n\n` +
		`- Run: ${runUrl}\n- Event: ${event}\n- Keys sent to the provider: ${translated}\n\n` +
		`${keys}\n\n${verdict}\n\n` +
		POSTED_BY
	)
}

/** The names of the steps whose outcome was `failure`, comma-separated. */
export function failedSteps(outcomes: string): string {
	return outcomes
		.split(/\s+/)
		.filter(Boolean)
		.map((pair) => pair.split('='))
		.filter(([, outcome]) => outcome === 'failure')
		.map(([name]) => name)
		.join(',')
}

/** The line of the log to quote: the provider's status line, else its last five lines. */
export function errorFromLog(log: string): string {
	const status = log.split('\n').find((line) => /The translation provider answered [0-9]+/.test(line))
	if (status !== undefined) return status
	return log.replace(/\n+$/, '').split('\n').slice(-5).join('\n').replace(/\n+$/, '')
}

export function failureReport({ runUrl, event, failed, error }: { runUrl: string; event: string; failed: string; error: string }): string {
	return (
		'A translation run **failed**.\n\n' +
		`- Run: ${runUrl}\n- Event: ${event}\n- Failed step(s): ${failed || 'a step before translation'}\n\n` +
		(error ? `\`\`\`\n${error}\n\`\`\`\n\n` : '') +
		'This issue stays open.\n\n' +
		POSTED_BY
	)
}

export interface MainDeps {
	argv?: readonly string[]
	env?: Env
	exec?: Exec
	log?: (line: string) => void
	read?: (path: string, encoding: 'utf8') => string
	exists?: (path: string) => boolean
}

export function main({ argv = process.argv.slice(2), env = process.env, exec: run = exec, log = console.log, read = readFileSync, exists = existsSync }: MainDeps = {}): number {
	const [mode] = argv
	if (mode !== 'success' && mode !== 'failure') {
		log('::error::Usage: node tools/i18n/report-translation-run.ts success|failure')
		return 1
	}
	const repository = required(env, 'GITHUB_REPOSITORY')
	const event = describeEvent(env)

	const listing = run('gh', ['issue', 'list', '--repo', repository, '--label', 'verify:translation-run', '--state', 'open', '--json', 'number', '--jq', '.[].number'], { check: true })
	const issues = listing.stdout.split('\n').filter(Boolean)
	if (issues.length === 0) {
		log('No open issue is waiting on a translation run.')
		return 0
	}

	let report: string
	if (mode === 'success') {
		report = successReport({
			provider: env.PROVIDER ?? '',
			runUrl: env.RUN_URL ?? '',
			event,
			translated: env.TRANSLATED ?? '',
			keys: keyList(env.TRANSLATED_KEYS ?? ''),
			verdict: verdictFor(env.CHECK_OUTCOME),
		})
	} else {
		const logFile = path.join(env.RUNNER_TEMP ?? tmpdir(), 'translate.log')
		report = failureReport({
			runUrl: env.RUN_URL ?? '',
			event,
			failed: failedSteps(env.OUTCOMES ?? ''),
			error: exists(logFile) ? errorFromLog(read(logFile, 'utf8')) : '',
		})
	}

	const file = path.join(env.RUNNER_TEMP ?? mkdtempSync(path.join(tmpdir(), 'translation-report-')), 'report.md')
	writeFileSync(file, report)

	for (const issue of issues) {
		run('gh', ['issue', 'comment', issue, '--repo', repository, '--body-file', file], { check: true })
		if (mode === 'success' && env.CHECK_OUTCOME === 'success') {
			run('gh', ['issue', 'close', issue, '--repo', repository, '--reason', 'completed'], { check: true })
		}
	}
	return 0
}

if (isMain(import.meta.url)) process.exit(main())
