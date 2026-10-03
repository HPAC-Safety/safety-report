#!/usr/bin/env node
// Test code is never part of a release (ADR-0188).
//
// Run after `vite build`, over src/web/dist (or the directory named on the
// command line). Fails when a built file carries a marker only test code
// holds: the Vitest and Testing Library packages, a test's own vocabulary, or
// a *.test.* file name. The markers were chosen against a real build: each is
// absent from today's bundle, and none is a word React or the app's own code
// uses. tools/web/check-component-split.ts stops the import at the source; this
// is the backstop on the artifact that ships, so the rule holds in a local
// build, an IDE and CI alike (ADR-0073).
//
// Usage: node tools/web/check-web-bundle.ts [dist-dir]
import { readFileSync, readdirSync, statSync } from 'node:fs'
import { join } from 'node:path'
import { isMain } from '../lib/actions.ts'

export const DEFAULT_DIST = 'src/web/dist'

export const MARKERS = [
	'vitest',
	'@testing-library',
	'testing-library',
	'user-event',
	'jsdom',
	'renderHook',
	'describe(',
	'expect(',
	'.test.',
]

const TEXT = /\.(js|mjs|cjs|css|html|json|map|svg|txt)$/i

function walk(dir: string): string[] {
	const found: string[] = []
	for (const entry of readdirSync(dir)) {
		const path = join(dir, entry)
		if (statSync(path).isDirectory()) found.push(...walk(path))
		else found.push(path)
	}
	return found
}

/** Every marker a built file carries, as `{ file, line, marker }`, or `{ file, marker }` for a file name. */
export interface BundleFile {
	path: string
	content: string | null
}

export interface TestCodeHit {
	file: string
	line: number
	marker: string
}

export function findTestCode(files: readonly BundleFile[]): TestCodeHit[] {
	const hits: TestCodeHit[] = []
	for (const { path, content } of files) {
		const nameHit = MARKERS.find((marker) => path.includes(marker))
		if (nameHit) hits.push({ file: path, line: 0, marker: nameHit })
		if (content === null) continue
		content.split('\n').forEach((line, index) => {
			const marker = MARKERS.find((candidate) => line.includes(candidate))
			if (marker) hits.push({ file: path, line: index + 1, marker })
		})
	}
	return hits
}

/** Reads a built directory. A font or image is named, not read. */
export function readBundle(dir: string): BundleFile[] {
	return walk(dir).map((path) => ({ path, content: TEXT.test(path) ? readFileSync(path, 'utf8') : null }))
}

export function main(argv: readonly string[]): number {
	const dir = argv[0] ?? DEFAULT_DIST
	let files: BundleFile[]
	try {
		files = readBundle(dir)
	} catch {
		console.error(`check-web-bundle: ${dir} does not exist; build first with npm --prefix src/web run build`)
		return 1
	}
	if (files.length === 0) {
		console.error(`check-web-bundle: ${dir} is empty, so there is nothing to certify`)
		return 1
	}
	const hits = findTestCode(files)
	for (const hit of hits) {
		console.error(`${hit.file}${hit.line ? `:${hit.line}` : ''}: test code in the bundle (found "${hit.marker}")`)
	}
	if (hits.length > 0) {
		console.error(`check-web-bundle: ${hits.length} marker(s) of test code in ${dir}; tests are never part of a release`)
		return 1
	}
	console.log(`check-web-bundle: ${files.length} built file(s) in ${dir} carry no test code`)
	return 0
}

if (isMain(import.meta.url)) process.exit(main(process.argv.slice(2)))
