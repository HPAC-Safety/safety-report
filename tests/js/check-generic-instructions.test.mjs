import { describe, it } from 'node:test'
import assert from 'node:assert/strict'
import { mkdtempSync, mkdirSync, writeFileSync } from 'node:fs'
import { tmpdir } from 'node:os'
import { dirname, join } from 'node:path'

import { GENERIC_FILES, checkText, main } from '../../tools/check-generic-instructions.mjs'

/** Runs `main` with console output silenced, restoring it afterwards even on failure. */
function runMain(root, files) {
	const original = { log: console.log, error: console.error }
	const errors = []
	console.log = () => {}
	console.error = (...args) => errors.push(args.join(' '))
	try {
		return { code: main(root, files), errors }
	} finally {
		console.log = original.log
		console.error = original.error
	}
}

/** A throwaway tree, one file per entry (nested paths create subdirectories). */
function tree(files) {
	const root = mkdtempSync(join(tmpdir(), 'generic-'))
	for (const [path, contents] of Object.entries(files)) {
		const full = join(root, path)
		mkdirSync(dirname(full), { recursive: true })
		writeFileSync(full, contents)
	}
	return root
}

describe('checkText', () => {
	it('passes a file that names nothing specific to the repository', () => {
		assert.deepEqual(checkText('skills/x/SKILL.md', '# Deliver a change\n\nRebase before every commit.\n'), [])
	})

	for (const [line, why] of [
		['Deliver an HPAC Safety change', 'names the product'],
		['the pilot becomes a role', 'names the product domain'],
		['a SafetyOfficer reviews it', 'names a product role'],
		['run graphify query first', 'names a tool or provider this repository chose'],
		['see ADR-0083', 'cites a decision record by number'],
		['as lesson 0004 found', 'cites a lesson by number'],
		['proven by REQ-SUB-012', 'cites a claim by ID'],
		['run node tools/traceability.mjs', 'names a path in this repository'],
	]) {
		it(`refuses a line that ${why}`, () => {
			const problems = checkText('agents/a.md', `# A\n${line}\n`)

			assert.equal(problems.length, 1)
			assert.match(problems[0], new RegExp(`^agents/a\\.md:2: ${why}`))
		})
	}

	it('does not mistake a generic word for a claim or a path', () => {
		assert.deepEqual(checkText('agents/a.md', 'Use the project tools and the source tree; a requirement stands.\n'), [])
	})
})

describe('main', () => {
	it('passes when every listed file is clean', () => {
		const root = tree({ 'agents/a.md': '# A\nGeneric.\n' })

		assert.equal(runMain(root, ['agents/a.md']).code, 0)
	})

	it('fails and annotates the line when a listed file names the repository', () => {
		const root = tree({ 'agents/a.md': '# A\nRead ADR-0001.\n' })

		const { code, errors } = runMain(root, ['agents/a.md'])

		assert.equal(code, 1)
		assert.match(errors[0], /^::error file=agents\/a\.md,line=2::cites a decision record by number/)
	})

	it('fails when a listed file no longer exists, so a rename updates the list', () => {
		const { code, errors } = runMain(tree({}), ['skills/gone/SKILL.md'])

		assert.equal(code, 1)
		assert.match(errors[0], /listed as generic but does not exist/)
	})

	it('checks the real repository by default', () => {
		assert.ok(GENERIC_FILES.length > 0)
		assert.equal(runMain(process.cwd()).code, 0)
	})
})
