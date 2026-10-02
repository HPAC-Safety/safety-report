// Which web files Vitest holds to 100% coverage (ADR-0188).
//
// The scope follows the files, never a list anyone edits, so the area pull
// requests of #756 never contend over one config:
//
//   - Foo.tsx that has a sibling Foo.view.tsx: a split component's logic;
//   - Foo.ts that has a colocated Foo.test.ts or Foo.test.tsx: a pure helper
//     someone chose to test.
//
// Views, tests, declaration files, main.tsx and routes.tsx are never in scope.
// src/web/vite.config.ts calls this, and tests/js/web-coverage-scope.test.mjs
// pins the rule.
import { readdirSync } from 'node:fs'
import { join, relative, sep } from 'node:path'

/** Nothing in scope yet: a glob that matches no file, so Vitest never falls back to everything. */
export const NOTHING_IN_SCOPE = 'src/__nothing_is_in_coverage_scope__'

const EXEMPT = new Set(['main.tsx', 'routes.tsx'])

const isTest = (name) => /\.test\.tsx?$/.test(name)
const isView = (name) => name.endsWith('.view.tsx')
const isDeclaration = (name) => name.endsWith('.d.ts')

function walk(dir) {
	const found = []
	for (const entry of readdirSync(dir, { withFileTypes: true })) {
		const path = join(dir, entry.name)
		if (entry.isDirectory()) found.push(...walk(path))
		else found.push(path)
	}
	return found
}

/**
 * The files under `<webRoot>/src` the 100% threshold covers, as paths relative
 * to `webRoot` with forward slashes, sorted.
 */
export function coverageScope(webRoot) {
	const files = walk(join(webRoot, 'src'))
	const present = new Set(files)
	const scope = []

	for (const file of files) {
		const name = file.slice(file.lastIndexOf(sep) + 1)
		if (isTest(name) || isView(name) || isDeclaration(name) || EXEMPT.has(name)) continue

		if (name.endsWith('.tsx')) {
			if (present.has(file.replace(/\.tsx$/, '.view.tsx'))) scope.push(file)
		} else if (name.endsWith('.ts')) {
			const stem = file.replace(/\.ts$/, '')
			if (present.has(`${stem}.test.ts`) || present.has(`${stem}.test.tsx`)) scope.push(file)
		}
	}

	const relativePaths = scope.map((file) => relative(webRoot, file).split(sep).join('/')).sort()
	return relativePaths.length > 0 ? relativePaths : [NOTHING_IN_SCOPE]
}
