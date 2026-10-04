#!/usr/bin/env node
// Finds the behavior-bearing files a diff changed and hands them to
// tools/spec/check-feature-coverage.ts, for a pull request or for a merge
// group (ADR-0083, ADR-0090, ADR-0147).
//
// Pull request: one check over the merge base of `$BASE_SHA` and HEAD, to
// HEAD, with the pull request body.
//
// What it hands over (CONV-001): the behavior-bearing files; the claims whose
// scenario text changed, read from both versions of each changed feature file,
// so a whitespace or comment edit changes none; and the areas the changed
// behavior-bearing and step-definition files map to (.spec/area-paths.json).
//
// Merge group: the merge queue runs this on the tree that will become main
// (ADR-0147). A merge group has no pull request body, but each pull request in
// it is one squash commit whose message is that body (PR_BODY), so the check
// runs once per queued commit: that commit's own diff, its own message, and the
// merged tree's claims. That is where a claim one pull request removes and
// another's exemption cites is caught. An empty range would check nothing, so
// it fails rather than passes.
//
// Environment: EVENT_NAME, PR_BODY, BASE_SHA.
import { readFileSync } from 'node:fs'

import { type Env, type Exec, exec, isMain, required } from '../lib/actions.ts'
import { type AreaClaim, type AreaPaths, areaOfFeature, areasOfChange, parseAreaPaths } from './area-paths.ts'
import { main as checkCoverage } from './check-feature-coverage.ts'
import { changedScenarios } from './read-claims.ts'
import { AREA_PATHS, CLAIMS, FEATURES, PLAYWRIGHT_STEPS, REQNROLL_STEPS } from './spec-paths.ts'

// ':(glob)' magic is required for '**' to match — git's default pathspec
// matching (fnmatch, no FNM_PATHNAME) treats a bare '**' as a single '*' and
// silently fails to match nested paths.
export const BEHAVIOR_PATHSPECS = [':(glob)src/**', ':(glob)tests/e2e/**/*.ts']
export const FEATURE_PATHSPECS = [`:(glob)${FEATURES}/**/*.feature`]
export const STEP_PATHSPECS = [`:(glob)${REQNROLL_STEPS}/**`, `:(glob)${PLAYWRIGHT_STEPS}/**`]

/**
 * Exclusions, each for a stated reason: tests/e2e/steps/**\/*.ts are the
 * execution half of a scenario (ADR-0053), not a second place requiring one;
 * .features-gen is generated; a README documents a component and cannot change
 * its behavior.
 */
export function isBehavior(path: string): boolean {
	return !path.includes('/.features-gen/') && !path.startsWith('tests/e2e/steps/') && !path.endsWith('/README.md')
}

const lines = (text: string): string[] => text.split('\n').filter((line) => line !== '')

/** The pull request number a squash commit's subject ends with, or undefined. */
export function pullRequestOf(subject: string): string | undefined {
	return subject.match(/\(#(\d+)\)$/)?.[1]
}

interface RangeContext {
	exec: Exec
	claims: string
	areaPaths: AreaPaths
	cwd?: string | undefined
}

/**
 * Checks what changed from `from` to `to`: `git diff` failures count as no
 * files, and a file missing on one side reads as empty there.
 */
export function checkRange(from: string, to: string, body: string, { exec, claims, areaPaths, cwd }: RangeContext): number {
	const diff = (pathspecs: readonly string[]): string[] => lines(exec('git', ['diff', '--name-only', `${from}..${to}`, '--', ...pathspecs], { cwd }).stdout)
	const show = (rev: string, path: string): string => {
		const result = exec('git', ['show', `${rev}:${path}`], { cwd })
		return result.status === 0 ? result.stdout : ''
	}

	const changed = diff(BEHAVIOR_PATHSPECS).filter(isBehavior)
	const features = diff(FEATURE_PATHSPECS)
	const scenarios = features.flatMap((path) => changedScenarios(show(from, path), show(to, path)).map((id) => ({ id, area: areaOfFeature(FEATURES, path) })))
	const known = (JSON.parse(claims) as { claims: AreaClaim[] }).claims
	const areas = areasOfChange([...changed, ...diff(STEP_PATHSPECS)], areaPaths, known)
	return checkCoverage({ changed, features, scenarios, areas, body, claims })
}

interface MainOptions {
	env?: Env
	exec?: Exec
	log?: (line: string) => void
	claims?: string
	areaPaths?: string
	cwd?: string
}

export function main({ env = process.env, exec: run = exec, log = console.log, claims, areaPaths, cwd }: MainOptions = {}): number {
	const base = required(env, 'BASE_SHA')
	const body = env.PR_BODY ?? ''
	const map = parseAreaPaths(areaPaths ?? readFileSync(AREA_PATHS, 'utf8'))
	if (typeof map === 'string') {
		console.error(`::error file=${AREA_PATHS}::${AREA_PATHS} ${map}`)
		return 1
	}
	const context = { exec: run, claims: claims ?? readFileSync(CLAIMS, 'utf8'), areaPaths: map, cwd }

	if (env.EVENT_NAME !== 'merge_group') {
		const mergeBase = run('git', ['merge-base', base, 'HEAD'], { cwd }).stdout.trim() || base
		return checkRange(mergeBase, 'HEAD', body, context)
	}

	const commits = lines(run('git', ['rev-list', '--reverse', `${base}..HEAD`], { cwd }).stdout)
	if (commits.length === 0) {
		console.error(`::error::The merge group holds no commit between ${base} and HEAD, so nothing was checked.`)
		return 1
	}

	let failed = false
	for (const commit of commits) {
		const subject = run('git', ['log', '-1', '--format=%s', commit], { cwd }).stdout
		log(`::group::${subject}`)
		const message = run('git', ['log', '-1', '--format=%B', commit], { cwd }).stdout
		if (checkRange(`${commit}^`, commit, message, context) !== 0) {
			console.error(
				`::error::Queued commit ${commit} (pull request #${pullRequestOf(subject) ?? 'unknown'}) fails the check above. Its message is the pull request body only while the repository's squash_merge_commit_message is PR_BODY; if the message above is not the body, check that setting (ADR-0147).`,
			)
			failed = true
		}
		log('::endgroup::')
	}
	return failed ? 1 : 0
}

if (isMain(import.meta.url)) process.exit(main())
