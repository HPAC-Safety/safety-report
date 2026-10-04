#!/usr/bin/env node
// Every behavior-bearing path belongs to a feature area (CONV-001).
//
// feature-coverage judges whether a pull request's scenarios relate to its
// code by the areas its changed files map to in .spec/area-paths.json
// (tools/spec/area-paths.ts). A file the map misses would make every scenario
// unrelated to it, so this fails, in the `docs` job:
//
//   - a behavior-bearing file — tracked under src/, or an e2e .ts that is not a
//     step definition, the files feature-coverage judges — that no glob maps;
//   - a glob that matches no such file, which a move left behind;
//   - an area key with no .spec/features/<area>/<area>.feature.
//
//   node tools/spec/check-area-paths.ts
//
// The exit code is the contract.
import { existsSync, readFileSync, readdirSync } from 'node:fs'
import { join } from 'node:path'

import { type Exec, exec as realExec, isMain } from '../lib/actions.ts'
import { parseAreaPaths, validate } from './area-paths.ts'
import { BEHAVIOR_PATHSPECS, isBehavior } from './check-feature-coverage-diff.ts'
import { AREA_PATHS, FEATURES } from './spec-paths.ts'

/** Every feature area: a directory under .spec/features holding its own feature file. */
export function featureAreas(root: string): string[] {
	return readdirSync(join(root, FEATURES), { withFileTypes: true })
		.filter((entry) => entry.isDirectory() && existsSync(join(root, FEATURES, entry.name, `${entry.name}.feature`)))
		.map((entry) => entry.name)
		.sort()
}

export interface MainOptions {
	root?: string
	exec?: Exec
	log?: (line: string) => void
}

export function main({ root = process.cwd(), exec = realExec, log = console.log }: MainOptions = {}): number {
	const map = parseAreaPaths(readFileSync(join(root, AREA_PATHS), 'utf8'))
	if (typeof map === 'string') {
		log(`::error file=${AREA_PATHS}::${AREA_PATHS} ${map}`)
		return 1
	}
	const files = exec('git', ['ls-files', '--', ...BEHAVIOR_PATHSPECS], { cwd: root, check: true })
		.stdout.split('\n')
		.filter((line) => line !== '' && isBehavior(line))
	const problems = validate(map, { files, areas: featureAreas(root) })
	for (const problem of problems) log(`::error file=${AREA_PATHS}::${problem}`)
	if (problems.length > 0) return 1
	log(`${AREA_PATHS}: ${files.length} behavior-bearing files, each in at least one of ${Object.keys(map.areas).length} areas.`)
	return 0
}

if (isMain(import.meta.url)) process.exit(main())
