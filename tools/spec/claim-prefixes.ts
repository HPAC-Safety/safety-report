#!/usr/bin/env node
// Which prefix a claim ID takes (ADR-0194).
//
// Each feature area declares, in its README's frontmatter, the one prefix
// every new claim in it takes: `prefix: REQ-QAU`. An ID is never renumbered
// (ADR-0084), so a scenario keeps its ID when it moves to another area: an
// area may also hold claims under a retired prefix, the prefix of an area that
// was split. A retired prefix issues nothing new; its last number is recorded
// here, and a claim above it fails.
//
//   node tools/spec/claim-prefixes.ts --next <area>   print the next unused ID for <area>
//
// Dependency-free, like every other tool in this directory.
import { existsSync, readFileSync, readdirSync, statSync } from 'node:fs'
import { join } from 'node:path'

import { parseFrontmatter } from '../docs/check-frontmatter.ts'
import { readClaims } from './read-claims.ts'
import { FEATURES } from './spec-paths.ts'
import { isMain } from '../lib/actions.ts'

/**
 * Prefixes no area issues any more, each with the last number it issued. Their
 * claims keep their IDs in whichever area holds them now. A split adds its
 * area's prefix here, with that area's highest number.
 */
export const RETIRED_PREFIXES: Readonly<Record<string, number>> = {
	// moderation-authentication-and-publication, split by #814.
	'REQ-MOD': 211,
	// question-bank-and-form, split by #814.
	'REQ-QB': 268,
}

const PREFIX = /^REQ-[A-Z]+$/

/** `REQ-QB-001` → `REQ-QB`. */
export const prefixOf = (id: string): string => id.replace(/-\d{3}$/, '')

/** `REQ-QB-001` → 1. */
const numberOf = (id: string): number => Number(id.slice(-3))

/** Each area directory, with the prefix its README declares, if any. */
export function areaPrefixes(root: string): Map<string, string | undefined> {
	const directory = join(root, FEATURES)
	const prefixes = new Map<string, string | undefined>()
	if (!existsSync(directory)) return prefixes
	for (const name of readdirSync(directory).sort()) {
		if (!statSync(join(directory, name)).isDirectory()) continue
		const readme = join(directory, name, 'README.md')
		const { entries = [] } = existsSync(readme) ? parseFrontmatter(readFileSync(readme, 'utf8')) : {}
		prefixes.set(name, entries.find((entry) => entry.key === 'prefix')?.value || undefined)
	}
	return prefixes
}

/**
 * Every way the claims and the areas' prefixes disagree: an area with no
 * prefix or a malformed, shared, or retired one, and a claim whose prefix is
 * neither its area's nor a retired one issued before it retired.
 */
export function prefixProblems(
	claims: readonly { id: string; area: string }[],
	prefixes: ReadonlyMap<string, string | undefined>,
	retired: Readonly<Partial<Record<string, number>>> = RETIRED_PREFIXES,
): string[] {
	const problems: string[] = []
	const owners = new Map<string, string>()
	for (const [area, prefix] of prefixes) {
		const readme = `${FEATURES}/${area}/README.md`
		if (prefix === undefined) {
			problems.push(`${readme}: declares no "prefix:" — the claim prefix every new scenario in this area takes (ADR-0194)`)
			continue
		}
		if (!PREFIX.test(prefix)) problems.push(`${readme}: "prefix: ${prefix}" is not of the form REQ-<AREA>`)
		if (prefix in retired) problems.push(`${readme}: "prefix: ${prefix}" is retired — a new area takes a prefix no area has used (ADR-0194)`)
		const owner = owners.get(prefix)
		if (owner !== undefined) problems.push(`${readme}: "prefix: ${prefix}" is already ${owner}'s — each area has its own`)
		else owners.set(prefix, area)
	}

	for (const claim of claims) {
		const prefix = prefixOf(claim.id)
		const own = prefixes.get(claim.area)
		if (prefix === own || own === undefined) continue
		const last = retired[prefix]
		if (last === undefined) {
			problems.push(`${claim.id}: ${FEATURES}/${claim.area} takes ${own} for a new claim; ${prefix} is neither its prefix nor a retired one (ADR-0194)`)
		} else if (numberOf(claim.id) > last) {
			problems.push(`${claim.id}: ${prefix} retired at ${prefix}-${String(last).padStart(3, '0')}, so a new claim in ${FEATURES}/${claim.area} takes ${own} (ADR-0194)`)
		}
	}
	return problems
}

/** The next unused claim ID under `area`'s own prefix, wherever its claims now live. */
export function nextClaim(area: string, claims: readonly { id: string }[], prefixes: ReadonlyMap<string, string | undefined>): string {
	const prefix = prefixes.get(area)
	if (prefix === undefined) throw new Error(`${FEATURES}/${area} is not an area with a declared prefix`)
	const highest = Math.max(0, ...claims.filter((claim) => prefixOf(claim.id) === prefix).map((claim) => numberOf(claim.id)))
	return `${prefix}-${String(highest + 1).padStart(3, '0')}`
}

/** Every claim in every area's feature file. */
export function allClaims(root: string, areas: Iterable<string>): { id: string; area: string }[] {
	return [...areas].flatMap((area) => {
		const path = `${FEATURES}/${area}/${area}.feature`
		return existsSync(join(root, path)) ? readClaims(path, readFileSync(join(root, path), 'utf8')).claims : []
	})
}

/** Reports the way the command line does, without exiting. Returns the exit code. */
export function main(argv: readonly string[], root = process.cwd(), log: (line: string) => void = console.log, error: (line: string) => void = console.error): number {
	const at = argv.indexOf('--next')
	const area = at === -1 ? undefined : argv[at + 1]
	if (area === undefined) {
		error('usage: node tools/spec/claim-prefixes.ts --next <area>')
		return 2
	}
	const prefixes = areaPrefixes(root)
	if (prefixes.get(area) === undefined) {
		error(`${FEATURES}/${area} is not an area with a declared "prefix:" — one of: ${[...prefixes.keys()].join(', ')}`)
		return 1
	}
	log(nextClaim(area, allClaims(root, prefixes.keys()), prefixes))
	return 0
}

if (isMain(import.meta.url)) process.exit(main(process.argv.slice(2)))
