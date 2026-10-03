#!/usr/bin/env node
// An accepted ADR's body never changes (ADR-0192). A change to a decision is a
// new ADR; the record it changes takes only a new status.
//
// Compares every ADR the diff touches that existed on the base with the
// same-numbered ADR now, and fails when:
//   - anything changed but its frontmatter `status:`, the status word that
//     opens its **Status:** line, a "Superseded by …" or "Deprecated …"
//     sentence in that line, or the target of a link whose old target is gone
//     (a link may follow a moved or deleted file; its text may not);
//   - it was deleted;
//   - its status moved in a way the lifecycle forbids: an accepted record
//     moves only to superseded or deprecated, and superseded, deprecated, and
//     rejected are terminal.
// A record still `proposed` on the base may change freely. An ADR the base
// does not have is new, and only check-records.ts judges it.
//
// The rule is in force from the first base that carries this record,
// ADR-0192: a base without it predates the rule, and nothing is compared. That
// is what let the pull request that wrote the rule normalize the old records.
// A base that is not a commit at all fails: a check that cannot see its base
// has checked nothing.
//
//   BASE_SHA=<sha> node tools/spec/check-adr-immutability.ts
//       CI: the base is the pull request's or merge group's base; HEAD's tree
//       is compared. With no BASE_SHA (a push to main), nothing is compared.
//   node tools/spec/check-adr-immutability.ts --staged
//       pre-commit: the base is HEAD's merge base with origin/main, and the
//       index is compared, so the commit is judged as it will be made.
//
// The exit code is the contract.
import { posix } from 'node:path'

import { type Env, type Exec, exec, isMain } from '../lib/actions.ts'
import { DECISIONS } from './spec-paths.ts'

const FILENAME = /^ADR-(\d{4})-[a-z0-9-]+\.md$/

/** The record that writes this rule; its presence on a base puts the rule in force. */
export const RULE_ADR = '0192'

/** Where each status may move to. A status not listed here is terminal. */
export const TRANSITIONS: Readonly<Partial<Record<string, readonly string[]>>> = {
	accepted: ['superseded', 'deprecated'],
	'partially-superseded': ['superseded', 'deprecated'],
}

/** A link: `[text](target)`. */
const LINK = /\]\(([^)\s]*)\)/g

/** The status paragraph, from `**Status:**` to the first blank line. */
const STATUS_PARAGRAPH = /^\*\*Status:\*\*[\s\S]*?(?=\n\s*\n|(?![\s\S]))/m

/**
 * What of a status paragraph may not change: everything but its opening
 * status word and any "Superseded by …" or "Deprecated …" sentence.
 */
export function statusRemainder(paragraph: string): string {
	return paragraph
		.replace(/^\*\*Status:\*\*/, '')
		.replace(/\b(?:Superseded|Deprecated)\b[\s\S]*?(?:\.(?=\s|$)|$)/g, ' ')
		.replace(/^\s*(?:Accepted|Proposed|Rejected|Superseded|Deprecated)\b[.,;:]?/, '')
		.replace(/\s+/g, ' ')
		.trim()
}

/** A record split into what may not change and the link targets it holds. */
export function immutableParts(text: string): { body: string; status: string; targets: string[] } {
	const status = statusRemainder(text.match(STATUS_PARAGRAPH)?.[0] ?? '')
	const rest = text.replace(/^status:.*$/m, 'status:').replace(STATUS_PARAGRAPH, '**Status:**')
	const targets = [...rest.matchAll(LINK)].map((match) => match[1])
	return { body: rest.replace(LINK, ']()'), status, targets }
}

/** The frontmatter `status:` value, or ''. */
const statusOf = (text: string): string => text.match(/^status:\s*(.*?)\s*$/m)?.[1] ?? ''

/** The repository path a link in a decision record points at, or null for a URL or an anchor. */
export function linkedPath(target: string): string | null {
	if (target === '' || target.startsWith('#') || /^[a-z][a-z0-9+.-]*:/i.test(target)) return null
	return posix.normalize(posix.join(DECISIONS, target.replace(/[#?].*$/, '')))
}

/**
 * Every way the records now differ from the base where they may not. `base`
 * maps each base file name to its text; `now` maps each current file name to
 * its text, for at least the numbers in `base`; `exists` says whether a
 * repository path exists now.
 */
export function compare(base: ReadonlyMap<string, string>, now: ReadonlyMap<string, string>, exists: (path: string) => boolean = () => true): string[] {
	const problems: string[] = []
	const current = new Map([...now.keys()].flatMap((name) => {
		const number = name.match(FILENAME)?.[1]
		return number ? [[number, name] as [string, string]] : []
	}))

	for (const [name, before] of base) {
		const number = name.match(FILENAME)?.[1]
		if (number === undefined) continue
		const was = statusOf(before)
		if (was === 'proposed') continue

		const currentName = current.get(number)
		if (currentName === undefined) {
			problems.push(`${DECISIONS}/${name}: deleted — an ADR is never deleted; supersede or deprecate it with a new record (ADR-0192)`)
			continue
		}

		const after = now.get(currentName) ?? ''
		const is = statusOf(after)
		const allowed = TRANSITIONS[was]
		if (is !== was && !(allowed ?? []).includes(is)) {
			problems.push(`${DECISIONS}/${currentName}: "status: ${was}" may not become "status: ${is}" — ${allowed ? `an ${was} record moves only to ${allowed.join(' or ')}` : `${was} is terminal`} (ADR-0192)`)
		}

		const old = immutableParts(before)
		const fresh = immutableParts(after)
		if (old.body !== fresh.body || old.status !== fresh.status || old.targets.length !== fresh.targets.length) {
			problems.push(`${DECISIONS}/${currentName}: its body changed — an accepted ADR changes only its status; write a new ADR that supersedes it (ADR-0192)`)
			continue
		}
		for (const [index, target] of old.targets.entries()) {
			if (fresh.targets[index] === target) continue
			const path = linkedPath(target)
			if (path === null || exists(path)) {
				problems.push(`${DECISIONS}/${currentName}: a link's target changed from ${target}, which still exists — a link may follow only a moved or deleted file (ADR-0192)`)
			}
		}
	}

	return problems
}

interface MainOptions {
	argv?: readonly string[]
	env?: Env
	exec?: Exec
	cwd?: string
	log?: (line: string) => void
	error?: (line: string) => void
}

/** Reports the way the command line does, without exiting. Returns the exit code. */
export function main({ argv = [], env = process.env, exec: run = exec, cwd, log = console.log, error = console.error }: MainOptions = {}): number {
	const git = (...args: string[]): { status: number; stdout: string } => run('git', args, { cwd })
	const staged = argv.includes('--staged')

	let base: string
	if (staged) {
		const mergeBase = git('merge-base', 'HEAD', 'origin/main')
		if (mergeBase.status !== 0 || mergeBase.stdout === '') {
			log('No origin/main to compare with; ADR immutability is checked in CI.')
			return 0
		}
		base = mergeBase.stdout
	} else {
		const sha = env.BASE_SHA ?? ''
		if (sha === '') {
			log('No BASE_SHA: nothing to compare (a push to main was compared on its pull request).')
			return 0
		}
		base = sha
	}

	if (git('cat-file', '-e', `${base}^{commit}`).status !== 0) {
		error(`::error::The base ${base} is not a commit in this clone; fetch it (actions/checkout fetch-depth: 0) so the ADRs can be compared (ADR-0192).`)
		return 1
	}

	const names = (listing: string): string[] =>
		listing
			.split('\n')
			.map((path) => path.split('/').pop() ?? '')
			.filter((name) => FILENAME.test(name))

	const baseAll = names(git('ls-tree', '--name-only', `${base}:${DECISIONS}`).stdout)
	if (!baseAll.some((name) => name.startsWith(`ADR-${RULE_ADR}-`))) {
		log(`The base ${base.slice(0, 12)} predates ADR-${RULE_ADR}; nothing to compare.`)
		return 0
	}

	// Only the records the diff touches. --no-renames lists a moved file under
	// its old name too, so its base text is read and its number followed.
	const diff = staged ? git('diff', '--cached', '--no-renames', '--name-only', base, '--', `${DECISIONS}/`) : git('diff', '--no-renames', '--name-only', base, 'HEAD', '--', `${DECISIONS}/`)
	const changed = new Set(names(diff.stdout))
	const baseNames = baseAll.filter((name) => changed.has(name))
	const numbers = new Set(baseNames.map((name) => name.slice(4, 8)))

	const nowAll = staged ? names(git('ls-files', '--', `${DECISIONS}/`).stdout) : names(git('ls-tree', '--name-only', `HEAD:${DECISIONS}`).stdout)
	const nowNames = nowAll.filter((name) => numbers.has(name.slice(4, 8)))

	const show = (spec: string): string => git('show', spec).stdout
	const now = staged ? ':' : 'HEAD:'
	const baseTexts = new Map(baseNames.map((name) => [name, show(`${base}:${DECISIONS}/${name}`)]))
	const nowTexts = new Map(nowNames.map((name) => [name, show(`${now}${DECISIONS}/${name}`)]))
	const exists = (path: string): boolean => git('cat-file', '-e', `${now}${path}`).status === 0

	const problems = compare(baseTexts, nowTexts, exists)
	if (problems.length > 0) {
		for (const problem of problems) {
			const [file, ...rest] = problem.split(': ')
			error(`::error file=${file}::${rest.join(': ')}`)
		}
		return 1
	}

	log(`${baseTexts.size} changed ADR(s) since ${base.slice(0, 12)}, each changed only in its status (ADR-0192).`)
	return 0
}

const runAsCommand = isMain(import.meta.url)
if (runAsCommand) process.exit(main({ argv: process.argv.slice(2) }))
