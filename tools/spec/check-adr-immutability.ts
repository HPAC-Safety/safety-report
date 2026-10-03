#!/usr/bin/env node
// An accepted ADR's body never changes (ADR-0192). A change to a decision is a
// new ADR; the record it changes takes only a new status.
//
// Compares every ADR that existed on the base with the same-numbered ADR now,
// and fails when:
//   - anything but its frontmatter `status:`, its **Status:** line, or a
//     link's target changed (a link's target may follow a moved file; its
//     text may not);
//   - it was deleted;
//   - it newly took the retired `partially-superseded`.
// A record still `proposed` on the base may change freely. An ADR the base
// does not have is new, and only check-records.ts judges it.
//
// The rule is in force from the first base that carries it: a base without
// .spec/decisions/TEMPLATE.md predates it, and nothing is compared. That is
// what let the pull request that wrote the rule normalize the old records.
//
//   BASE_SHA=<sha> node tools/spec/check-adr-immutability.ts
//       CI: the base is the pull request's or merge group's base; HEAD's tree
//       is compared. With no BASE_SHA (a push to main), nothing is compared.
//   node tools/spec/check-adr-immutability.ts --staged
//       pre-commit: the base is HEAD's merge base with origin/main, and the
//       index is compared, so the commit is judged as it will be made.
//
// The exit code is the contract.
import { type Env, type Exec, exec, isMain } from '../lib/actions.ts'
import { ADR_TEMPLATE, DECISIONS } from './spec-paths.ts'

const FILENAME = /^ADR-(\d{4})-[a-z0-9-]+\.md$/

/**
 * The part of a record that never changes: everything except the frontmatter
 * `status:` value, the **Status:** paragraph, and each link's target.
 */
export function immutablePart(text: string): string {
	return text
		.replace(/^status:.*$/m, 'status:')
		.replace(/^\*\*Status:\*\*[\s\S]*?(?=\n\s*\n|(?![\s\S]))/m, '**Status:**')
		.replace(/\]\([^)\s]*\)/g, ']()')
}

/** The frontmatter `status:` value, or ''. */
const statusOf = (text: string): string => text.match(/^status:\s*(.*?)\s*$/m)?.[1] ?? ''

/** One ADR file, by the number its name claims. */
const byNumber = (names: readonly string[]): Map<string, string> =>
	new Map(names.flatMap((name) => {
		const number = name.match(FILENAME)?.[1]
		return number ? [[number, name] as [string, string]] : []
	}))

/**
 * Every way the records now differ from the base where they may not. `base`
 * and `now` map each file name to its text.
 */
export function compare(base: ReadonlyMap<string, string>, now: ReadonlyMap<string, string>): string[] {
	const problems: string[] = []
	const current = byNumber([...now.keys()])

	for (const [name, before] of base) {
		const number = name.match(FILENAME)?.[1]
		if (number === undefined) continue
		const was = statusOf(before)
		if (was === 'proposed') continue

		const currentName = current.get(number)
		if (currentName === undefined) {
			problems.push(`${DECISIONS}/${name}: deleted — an ADR is never deleted; supersede or deprecate it with a new one (ADR-0192)`)
			continue
		}

		const after = now.get(currentName) ?? ''
		if (immutablePart(after) !== immutablePart(before)) {
			problems.push(`${DECISIONS}/${currentName}: its body changed — an accepted ADR changes only its status; write a new ADR that supersedes it (ADR-0192)`)
		}
		if (statusOf(after) === 'partially-superseded' && was !== 'partially-superseded') {
			problems.push(`${DECISIONS}/${currentName}: newly "partially-superseded", which is retired — the record a new ADR changes is superseded by it (ADR-0192)`)
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

	if (git('cat-file', '-e', `${base}:${ADR_TEMPLATE}`).status !== 0) {
		log(`The base ${base.slice(0, 12)} predates ADR-0192 (no ${ADR_TEMPLATE}); nothing to compare.`)
		return 0
	}

	const names = (listing: string): string[] =>
		listing
			.split('\n')
			.map((path) => path.split('/').pop() ?? '')
			.filter((name) => FILENAME.test(name))

	const baseNames = names(git('ls-tree', '--name-only', `${base}:${DECISIONS}`).stdout)
	const nowNames = staged ? names(git('ls-files', '--', `${DECISIONS}/`).stdout) : names(git('ls-tree', '--name-only', `HEAD:${DECISIONS}`).stdout)

	const show = (spec: string): string => git('show', spec).stdout
	const baseTexts = new Map(baseNames.map((name) => [name, show(`${base}:${DECISIONS}/${name}`)]))
	const nowTexts = new Map(nowNames.map((name) => [name, show(staged ? `:${DECISIONS}/${name}` : `HEAD:${DECISIONS}/${name}`)]))

	const problems = compare(baseTexts, nowTexts)
	if (problems.length > 0) {
		for (const problem of problems) {
			const [file, ...rest] = problem.split(': ')
			error(`::error file=${file}::${rest.join(': ')}`)
		}
		return 1
	}

	log(`${baseTexts.size} ADR(s) on ${base.slice(0, 12)} unchanged but for their status (ADR-0192).`)
	return 0
}

const runAsCommand = isMain(import.meta.url)
if (runAsCommand) process.exit(main({ argv: process.argv.slice(2) }))
