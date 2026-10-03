import { test } from 'node:test'
import assert from 'node:assert/strict'
import { mkdtempSync, mkdirSync, writeFileSync, rmSync } from 'node:fs'
import { tmpdir } from 'node:os'
import { join } from 'node:path'
import { coverageScope, NOTHING_IN_SCOPE } from '../../../tools/web/web-coverage-scope.ts'

function tree(files: readonly string[]): string {
	const root = mkdtempSync(join(tmpdir(), 'web-scope-'))
	for (const file of files) {
		const path = join(root, file)
		mkdirSync(join(path, '..'), { recursive: true })
		writeFileSync(path, '')
	}
	return root
}

test('a component with a sibling view is in scope, the view and its test are not', () => {
	const root = tree(['src/dialogs/Foo.tsx', 'src/dialogs/Foo.view.tsx', 'src/dialogs/Foo.test.tsx'])
	try {
		assert.deepEqual(coverageScope(root), ['src/dialogs/Foo.tsx'])
	} finally {
		rmSync(root, { recursive: true })
	}
})

test('a component with no view yet is not in scope', () => {
	const root = tree(['src/Bar.tsx', 'src/Bar.test.tsx', 'src/Baz.tsx', 'src/Baz.view.tsx'])
	try {
		assert.deepEqual(coverageScope(root), ['src/Baz.tsx'])
	} finally {
		rmSync(root, { recursive: true })
	}
})

test('a helper is in scope only when it has a colocated test, .ts or .tsx', () => {
	const root = tree([
		'src/lib/tested.ts',
		'src/lib/tested.test.ts',
		'src/lib/hooky.ts',
		'src/lib/hooky.test.tsx',
		'src/lib/untested.ts',
		'src/lib/types.d.ts',
		'src/lib/types.test.ts',
	])
	try {
		assert.deepEqual(coverageScope(root), ['src/lib/hooky.ts', 'src/lib/tested.ts'])
	} finally {
		rmSync(root, { recursive: true })
	}
})

test('main.tsx and routes.tsx are never in scope, even beside a view', () => {
	const root = tree(['src/main.tsx', 'src/main.view.tsx', 'src/routes.tsx', 'src/routes.view.tsx'])
	try {
		assert.deepEqual(coverageScope(root), [NOTHING_IN_SCOPE])
	} finally {
		rmSync(root, { recursive: true })
	}
})

test('an empty scope is a glob that matches nothing, never an empty list', () => {
	const root = tree(['src/Only.tsx'])
	try {
		assert.deepEqual(coverageScope(root), [NOTHING_IN_SCOPE])
	} finally {
		rmSync(root, { recursive: true })
	}
})
