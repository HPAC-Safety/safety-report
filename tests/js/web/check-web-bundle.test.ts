import { test } from 'node:test'
import assert from 'node:assert/strict'
import { mkdtempSync, mkdirSync, writeFileSync, rmSync } from 'node:fs'
import { tmpdir } from 'node:os'
import { join } from 'node:path'
import { findTestCode, main, MARKERS, readBundle } from '../../../tools/web/check-web-bundle.ts'

function dist(files: Record<string, string>): string {
	const root = mkdtempSync(join(tmpdir(), 'web-bundle-'))
	for (const [name, content] of Object.entries(files)) {
		const path = join(root, name)
		mkdirSync(join(path, '..'), { recursive: true })
		writeFileSync(path, content)
	}
	return root
}

function quietly<T>(run: () => T): { status: T; lines: string[] } {
	const log = console.log
	const error = console.error
	const lines: string[] = []
	console.log = (...args) => lines.push(args.join(' '))
	console.error = (...args) => lines.push(args.join(' '))
	try {
		return { status: run(), lines }
	} finally {
		console.log = log
		console.error = error
	}
}

test('every marker is found, with its file and line', () => {
	for (const marker of MARKERS) {
		const hits = findTestCode([{ path: 'a.js', content: `var x = 1\nvar y = "${marker}"` }])
		assert.deepEqual(hits, [{ file: 'a.js', line: 2, marker }], marker)
	}
})

test('a file named like a test is found even when its content is clean', () => {
	assert.deepEqual(findTestCode([{ path: 'assets/Foo.test.js', content: 'x' }]), [{ file: 'assets/Foo.test.js', line: 0, marker: '.test.' }])
})

test('a binary file is judged by its name only', () => {
	assert.deepEqual(findTestCode([{ path: 'assets/font.woff2', content: null }]), [])
})

test('words React and application code use are not markers', () => {
	const content = 'if(/x/.test(a)){it("run");test(b)} aria-describedby="x" function describeNode(){}'
	assert.deepEqual(findTestCode([{ path: 'index.js', content }]), [])
})

test('a clean build passes and says how many files it read', () => {
	const root = dist({ 'index.html': '<html></html>', 'assets/index.js': 'console.log(1)', 'assets/f.woff2': 'binary' })
	try {
		const { status, lines } = quietly(() => main([root]))
		assert.equal(status, 0)
		assert.match(lines[0], /3 built file\(s\)/)
	} finally {
		rmSync(root, { recursive: true })
	}
})

test('a build holding Testing Library fails and names file and line', () => {
	const root = dist({ 'assets/index.js': 'a\nimport "@testing-library/react"' })
	try {
		const { status, lines } = quietly(() => main([root]))
		assert.equal(status, 1)
		assert.match(lines[0], /index\.js:2: test code in the bundle/)
		assert.match(lines.at(-1) ?? '', /tests are never part of a release/)
	} finally {
		rmSync(root, { recursive: true })
	}
})

test('a missing or empty directory fails rather than certifying nothing', () => {
	const empty = dist({})
	try {
		assert.equal(quietly(() => main([join(empty, 'missing')])).status, 1)
		assert.equal(quietly(() => main([empty])).status, 1)
		assert.deepEqual(readBundle(empty), [])
	} finally {
		rmSync(empty, { recursive: true })
	}
})
