import { describe, it } from 'node:test'
import assert from 'node:assert/strict'
import { mkdtempSync, mkdirSync, writeFileSync } from 'node:fs'
import { tmpdir } from 'node:os'
import { dirname, join } from 'node:path'

import { checkText, genericFiles, main } from '../../../tools/docs/check-generic-instructions.ts'

/** Runs `main` with console output silenced, restoring it afterwards even on failure. */
function runMain(root: string, files?: readonly string[]) {
	const original = { log: console.log, error: console.error }
	const errors: string[] = []
	console.log = () => {}
	console.error = (...args: unknown[]) => errors.push(args.join(' '))
	try {
		return { code: main(root, files), errors }
	} finally {
		console.log = original.log
		console.error = original.error
	}
}

/** A throwaway tree, one file per entry (nested paths create subdirectories). */
function tree(files: Record<string, string>): string {
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
		['the rule in CONV-007', 'cites a convention by number'],
		['the Worker runs it', "names this product's background service"],
		['The Worker runs it', "names this product's background service"],
		['send it to the reporter', 'names a product role'],
		['file an occurrence report', 'names the product domain'],
		['run node tools/spec/generate-traceability.ts', 'names a path in this repository'],
	]) {
		it(`refuses a line that ${why}`, () => {
			const problems = checkText('agents/a.md', `# A\n${line}\n`)

			assert.equal(problems.length, 1)
			assert.match(problems[0], new RegExp(`^agents/a\\.md:2: ${why}`))
		})
	}

	it('lets a plain worker through, because only the capitalized service names this product', () => {
		assert.equal(checkText('agents/a.md', 'The background worker retries; a web worker is not the Worker.\n').length, 1)
		assert.deepEqual(checkText('agents/a.md', 'A background worker retries.\n'), [])
	})

	it('does not mistake a generic word for a claim or a path', () => {
		assert.deepEqual(checkText('agents/a.md', 'Use the project tools and the source tree; a requirement stands.\n'), [])
	})

	it('lets generic uses of words the product also uses through', () => {
		for (const line of ['Configure the test reporter.', 'Remove each occurrence of the string.', 'Code lives in src/.', 'Put step files under features/steps.']) {
			assert.deepEqual(checkText('agents/a.md', `${line}\n`), [], line)
		}
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
		assert.ok(genericFiles(process.cwd()).length > 0)
		assert.equal(runMain(process.cwd()).code, 0)
	})
})

describe('genericFiles', () => {
	it('selects every agent and every text file of each skill whose directory has no hpac, in any case', () => {
		const root = tree({
			'agents/a.md': '# A\n',
			'agents/B.MD': '# B\n',
			'agents/nested/c.md': '# C\n',
			'agents/notes.txt': 'not an agent\n',
			'skills/review-work/SKILL.md': '# R\n',
			'skills/review-work/references/format.md': '# F\n',
			'skills/review-work/agents/openai.yaml': 'name: r\n',
			'skills/review-work/logo.png': 'binary',
			'skills/deliver-hpac-change/SKILL.md': '# D\n',
			'skills/HPAC-upper/SKILL.md': '# U\n',
			'skills/hpac-domain-model/SKILL.md': '# H\n',
		})

		assert.deepEqual(genericFiles(root), [
			'agents/B.MD',
			'agents/a.md',
			'agents/nested/c.md',
			'skills/review-work/SKILL.md',
			'skills/review-work/agents/openai.yaml',
			'skills/review-work/references/format.md',
		])
	})

	it('fails a project term in a generic skill\'s supporting file', () => {
		const root = tree({ 'skills/review-work/SKILL.md': '# R\n', 'skills/review-work/references/x.md': 'See ADR-0001.\n' })

		const { code, errors } = runMain(root)

		assert.equal(code, 1)
		assert.match(errors[0], /file=skills\/review-work\/references\/x\.md,line=1/)
	})

	it('fails a generic skill directory without a file named exactly SKILL.md', () => {
		const root = tree({ 'skills/misnamed/skill.md': '# M\n' })

		const { code, errors } = runMain(root)

		assert.equal(code, 1)
		assert.match(errors[0], /skills\/misnamed\/SKILL\.md.*needs a file named exactly SKILL\.md/)
	})

	it('picks up a new generic skill with no list to update', () => {
		const root = tree({ 'skills/brand-new/SKILL.md': '# New\nSee ADR-0001.\n' })

		const { code, errors } = runMain(root)

		assert.equal(code, 1)
		assert.match(errors[0], /file=skills\/brand-new\/SKILL\.md,line=2::cites a decision record by number/)
	})

	it('fails a project term planted in a generic skill', () => {
		const root = tree({ 'skills/review-work/SKILL.md': '# R\nThis names HPAC.\n' })

		assert.equal(runMain(root).code, 1)
	})
})
