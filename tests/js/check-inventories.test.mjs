import { describe, it } from 'node:test'
import assert from 'node:assert/strict'

import { inventoryEntries, inventoryProblems, main, sourceDirectories } from '../../tools/check-inventories.mjs'

const INVENTORY = [
	'| Directory | Holds |',
	'|---|---|',
	'| [`src/App`](../src/App/) | The project. |',
	'| [`Admin/`](../src/App/Admin/) | Admin endpoints. |',
	'| [`Features/Comments/`](../src/Core/Features/Comments/) | Comments. |',
	'',
	'Migrations are listed in [`Migrations/README.md`](../src/App/Migrations/README.md), not here.',
].join('\n')

const FILES = ['src/App/Program.cs', 'src/App/Admin/Endpoints.cs', 'src/Core/Features/Comments/Comment.cs', 'README.md']

/** Runs `main` with console output captured, restoring it afterwards even on failure. */
function runMain(input) {
	const output = { log: [], error: [] }
	const original = { log: console.log, error: console.error }
	console.log = (...args) => output.log.push(args.join(' '))
	console.error = (...args) => output.error.push(args.join(' '))
	try {
		return { code: main(input), output }
	} finally {
		console.log = original.log
		console.error = original.error
	}
}

describe('sourceDirectories', () => {
	it('lists each directory under src/ that holds a file, and nothing outside it', () => {
		assert.deepEqual([...sourceDirectories(FILES)].sort(), ['src/App', 'src/App/Admin', 'src/Core/Features/Comments'])
	})

	it('never counts build output or src/ itself', () => {
		const files = ['src/App/bin/Debug/App.dll', 'src/App/obj/project.assets.json', 'src/web/node_modules/a/index.js', 'src/README.md']

		assert.deepEqual([...sourceDirectories(files)], [])
	})
})

describe('inventoryEntries', () => {
	it('reads the directory each table row links, resolved from docs/', () => {
		assert.deepEqual(inventoryEntries(INVENTORY), ['src/App', 'src/App/Admin', 'src/Core/Features/Comments'])
	})

	it('ignores a link in prose, and a row that links outside src/', () => {
		assert.deepEqual(inventoryEntries('See [x](../src/App/Gone/).\n| [ADR](decisions/ADR-0001.md) | x |\n'), [])
	})
})

describe('inventoryProblems', () => {
	it('passes an inventory that lists every directory', () => {
		assert.deepEqual(inventoryProblems({ files: FILES, markdown: INVENTORY }), [])
	})

	it('names a directory with no entry', () => {
		const problems = inventoryProblems({ files: [...FILES, 'src/App/Reports/Submit.cs'], markdown: INVENTORY })

		assert.deepEqual(problems, ['src/App/Reports/ has no entry in docs/source-inventory.md.'])
	})

	it('names an entry whose directory no longer exists', () => {
		const problems = inventoryProblems({ files: FILES.filter((file) => !file.includes('Admin')), markdown: INVENTORY })

		assert.deepEqual(problems, ['docs/source-inventory.md lists src/App/Admin/, which no longer exists.'])
	})
})

describe('main', () => {
	it('notes a current inventory and exits 0', () => {
		const { code, output } = runMain({ files: FILES, markdown: INVENTORY })

		assert.equal(code, 0)
		assert.match(output.log.join('\n'), /::notice::/)
	})

	it('annotates each problem and exits 1', () => {
		const { code, output } = runMain({ files: [...FILES, 'src/App/Reports/Submit.cs'], markdown: INVENTORY })

		assert.equal(code, 1)
		assert.match(output.error.join('\n'), /::error file=docs\/source-inventory\.md::src\/App\/Reports\/ has no entry/)
	})
})
