import { describe, it } from 'node:test'
import assert from 'node:assert/strict'
import { mkdtempSync, mkdirSync, symlinkSync, writeFileSync } from 'node:fs'
import { tmpdir } from 'node:os'
import { dirname, join } from 'node:path'

import { checkText, index, main, resolve } from '../../tools/check-links.mjs'

/** A throwaway directory holding `files`; not a git repository, so the walk lists them. */
function tree(files) {
	const root = mkdtempSync(join(tmpdir(), 'check-links-'))
	for (const [path, text] of Object.entries(files)) {
		mkdirSync(dirname(join(root, path)), { recursive: true })
		writeFileSync(join(root, path), text)
	}
	return root
}

/** Runs `main` with console output captured, restoring it afterwards even on failure. */
function runMain(root, paths) {
	const output = { log: [], error: [] }
	const original = { log: console.log, error: console.error }
	console.log = (...args) => output.log.push(args.join(' '))
	console.error = (...args) => output.error.push(args.join(' '))
	try {
		return { code: main(root, paths), output }
	} finally {
		console.log = original.log
		console.error = original.error
	}
}

describe('resolve', () => {
	it('resolves relative to the file, or to the root for a leading slash', () => {
		assert.equal(resolve('docs/a.md', '../.spec/features/README.md'), '.spec/features/README.md')
		assert.equal(resolve('docs/a.md', '/.spec/features/'), '.spec/features')
		assert.equal(resolve('docs/a.md', 'b%20c.md'), 'docs/b c.md')
	})

	it('refuses a link that climbs out of the repository', () => {
		assert.equal(resolve('a.md', '../outside.md'), null)
	})

	it('checks a malformed percent escape as written', () => {
		assert.equal(resolve('docs/a.md', 'b%E0%A4%A.md'), 'docs/b%E0%A4%A.md')
	})
})

describe('checkText', () => {
	const lookup = index(['README.md', 'docs/guide.md', 'tests/e2e/steps/a.ts'])
	const read = (path) => (path === 'docs/guide.md' ? '# Guide\n## Verify and publish\n' : '# Readme\n')

	it('accepts a tracked file, a directory that holds one, a good anchor, and an external URL', () => {
		const text = '[a](docs/guide.md) [b](tests/e2e/steps) [c](docs/guide.md#verify-and-publish) [d](https://example.com/x.md)'

		assert.deepEqual(checkText('README.md', text, lookup, read), [])
	})

	it('reports a missing file and a missing anchor, each with its line', () => {
		const problems = checkText('README.md', 'ok\n[a](docs/gone.md)\n[b](docs/guide.md#nowhere)', lookup, read)

		assert.deepEqual(problems.map((problem) => problem.line), [2, 3])
		assert.match(problems[0].message, /docs\/gone.md is not a tracked file/)
		assert.match(problems[1].message, /#nowhere names no heading/)
	})

	it('reports a link that climbs out of the repository', () => {
		const problems = checkText('README.md', '[up](../elsewhere/README.md)', lookup, read)

		assert.match(problems[0].message, /climbs out of the repository/)
	})

	it('checks a bare anchor against the file itself', () => {
		const problems = checkText('docs/guide.md', '# Guide\n[a](#guide) [b](#missing)', lookup, read)

		assert.equal(problems.length, 1)
		assert.match(problems[0].message, /#missing/)
	})
})

describe('main', () => {
	it('passes a tree whose links all resolve, and skips a symlink', () => {
		const root = tree({ 'AGENTS.md': '[guide](docs/guide.md)', 'docs/guide.md': '# Guide\n' })
		symlinkSync('AGENTS.md', join(root, 'CLAUDE.md'))

		const { code, output } = runMain(root, [])

		assert.equal(code, 0)
		assert.match(output.log.join('\n'), /2 file\(s\) checked/)
	})

	it('fails a broken link and names the file and line for the annotation', () => {
		const root = tree({ 'README.md': '# R\n\n[gone](docs/gone.md)\n' })

		const { code, output } = runMain(root, [])

		assert.equal(code, 1)
		assert.match(output.error.join('\n'), /::error file=README.md,line=3::docs\/gone.md does not resolve/)
	})

	it('checks a C# doc-comment href', () => {
		const root = tree({ 'src/A/B.cs': '/// <see href="../../docs/missing.md">x</see>\n', 'docs/guide.md': '# Guide\n' })

		const { code } = runMain(root, [])

		assert.equal(code, 1)
	})
})
