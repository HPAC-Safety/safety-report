// Which feature areas a changed path belongs to (CONV-001).
//
// `.spec/area-paths.json` maps every behavior-bearing path — everything under
// src/ and every e2e file that is not a step definition — to the areas whose
// scenarios describe it. `every` lists the paths that belong to every area:
// the composition roots, shared primitives, persistence, and build files a
// change anywhere may touch. A step definition is not mapped here: its areas
// are the areas of the claims it binds, read from .spec/claims.json, so the
// two never disagree. A helper in a step directory that binds no claim
// belongs to every area its engine serves.
//
// Pure: callers pass the map, the claims, and the file list. Globs are Node's
// path.matchesGlob, so `*` stays inside one directory and `**` crosses them.
import { matchesGlob, posix } from 'node:path'

import { PLAYWRIGHT_STEPS, REQNROLL_STEPS } from './spec-paths.ts'

/** The shape of .spec/area-paths.json. */
export interface AreaPaths {
	every: string[]
	areas: Record<string, string[]>
}

/** What this module reads of .spec/claims.json. */
export interface AreaClaim {
	id: string
	area: string
	engine: 'Reqnroll' | 'playwright-bdd'
	stepFiles: string[]
}

/** The map, or why it is not one. */
export function parseAreaPaths(json: string): AreaPaths | string {
	let value: unknown
	try {
		value = JSON.parse(json)
	} catch (error) {
		return `is not JSON: ${(error as Error).message}`
	}
	const isGlobs = (globs: unknown): globs is string[] => Array.isArray(globs) && globs.every((glob) => typeof glob === 'string' && glob !== '')
	if (typeof value !== 'object' || value === null || Array.isArray(value)) return 'is not an object'
	const { every, areas, ...rest } = value as Record<string, unknown>
	if (Object.keys(rest).length > 0) return `has keys it does not define: ${Object.keys(rest).join(', ')}`
	if (!isGlobs(every)) return '"every" is not a list of globs'
	if (typeof areas !== 'object' || areas === null || Array.isArray(areas)) return '"areas" is not an object'
	for (const [area, globs] of Object.entries(areas)) if (!isGlobs(globs)) return `"areas.${area}" is not a list of globs`
	return { every, areas: areas as Record<string, string[]> }
}

const isStepFile = (path: string): boolean => path.startsWith(`${REQNROLL_STEPS}/`) || path.startsWith(`${PLAYWRIGHT_STEPS}/`)

/**
 * The areas `path` belongs to: by the map for a behavior file, by the claims
 * it binds for a step definition. Empty when nothing maps it.
 */
export function areasOf(path: string, map: AreaPaths, claims: readonly AreaClaim[]): Set<string> {
	const all = new Set([...Object.keys(map.areas), ...claims.map((claim) => claim.area)])
	if (isStepFile(path)) {
		const bound = claims.filter((claim) => claim.stepFiles.includes(path))
		if (bound.length > 0) return new Set(bound.map((claim) => claim.area))
		const engine = path.startsWith(`${PLAYWRIGHT_STEPS}/`) ? 'playwright-bdd' : 'Reqnroll'
		return new Set(claims.filter((claim) => claim.engine === engine).map((claim) => claim.area))
	}
	if (map.every.some((glob) => matchesGlob(path, glob))) return all
	return new Set(Object.entries(map.areas).filter(([, globs]) => globs.some((glob) => matchesGlob(path, glob))).map(([area]) => area))
}

/**
 * What is wrong with the map against the tree: an area with no feature file, a
 * glob that matches no file, and a behavior file no glob maps.
 */
export function validate(map: AreaPaths, { files, areas }: { files: readonly string[]; areas: readonly string[] }): string[] {
	const problems: string[] = []
	for (const area of Object.keys(map.areas)) {
		if (!areas.includes(area)) problems.push(`"${area}" is not a feature area; the areas are ${areas.join(', ')}`)
	}
	const globs = [...map.every.map((glob) => ['every', glob] as const), ...Object.entries(map.areas).flatMap(([area, list]) => list.map((glob) => [area, glob] as const))]
	for (const [where, glob] of globs) {
		if (!files.some((file) => matchesGlob(file, glob))) problems.push(`${where}: "${glob}" matches no behavior-bearing file`)
	}
	for (const file of files) {
		if (areasOf(file, map, []).size === 0) problems.push(`${file} belongs to no area; add it to the area whose scenarios describe it, or to "every"`)
	}
	return problems
}

/** The feature area a feature file belongs to. */
export const areaOfFeature = (features: string, path: string): string => posix.relative(features, path).split('/')[0]
