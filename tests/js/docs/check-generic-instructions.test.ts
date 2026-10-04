import { describe, it } from 'node:test'
import assert from 'node:assert/strict'
import { mkdtempSync, mkdirSync, writeFileSync } from 'node:fs'
import { tmpdir } from 'node:os'
import { dirname, join } from 'node:path'

import { checkText, genericFiles, instructionFiles, main } from '../../../tools/docs/check-generic-instructions.ts'

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
		assert.match(errors[0], /listed but does not exist/)
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

describe('record references', () => {
	for (const [line, why] of [
		['see [the ADR](../../.spec/decisions/ADR-0001-x.md)', 'references a specification path'],
		['read .spec/claims.json', 'references a specification path'],
		['see adr-0001', 'references a decision record by number'],
		['as Lesson 0003 found', 'references a lesson by number'],
		['rule CONV-009', 'references a convention by number'],
		['proven by REQ-SUB-012', 'references a claim by ID'],
		['in docs/lessons/x.md', 'references a record directory'],
	]) {
		it(`refuses an hpac skill line that ${why}`, () => {
			const problems = checkText('skills/hpac-x/SKILL.md', `# X\n${line}\n`)

			assert.ok(problems.length >= 1, line)
			assert.match(problems[0], new RegExp(`^skills/hpac-x/SKILL\\.md:2: ${why}`))
		})
	}

	it('lets an hpac skill name the product, tools, and source paths', () => {
		assert.deepEqual(checkText('skills/hpac-x/SKILL.md', 'Run node tools/spec/x.ts; edit src/HpacSafety.Worker; the pilot, the Worker.\n'), [])
	})

	it('still fails a generic skill that names the product', () => {
		assert.equal(checkText('skills/review-work/SKILL.md', 'This names HPAC.\n').length, 1)
	})

	it('reports the same match once when both lists match', () => {
		assert.equal(checkText('skills/review-work/SKILL.md', 'see ADR-0001 and .spec/x.md\n').length, 2)
		assert.equal(checkText('skills/review-work/SKILL.md', 'see ADR-0001\n').length, 1)
	})

	it('scans hpac skills in main, and never scans AGENTS.md', () => {
		const root = tree({ 'AGENTS.md': 'See ADR-0001 and .spec/x.\n', 'skills/hpac-x/SKILL.md': '# X\nSee ADR-0001.\n' })

		assert.deepEqual(instructionFiles(root), ['skills/hpac-x/SKILL.md'])
		const { code, errors } = runMain(root)
		assert.equal(code, 1)
		assert.equal(errors.length, 1)
		assert.match(errors[0], /file=skills\/hpac-x\/SKILL\.md,line=2::references a decision record by number/)
	})
})

describe('record reference variants', () => {
	const hpac = 'skills/hpac-x/SKILL.md'

	for (const line of [
		'ADR 0147', 'ADR0147', 'ADR\u20110147', 'ADR\u20130147', 'ADR\u20100147', 'REQ\u2011SUB\u2011012', 'lesson  0003', 'lesson-0003',
		'lesson #3', 'CONV009', 'conv 009', 'lessons/0003-x.md', '.spec', 'see claims.json', 'claims.schema.json', 'traceability.md', 'area-paths.json',
	]) {
		it(`refuses "${line}"`, () => {
			assert.ok(checkText(hpac, `${line}\n`).length >= 1)
			assert.ok(checkText('skills/review-work/SKILL.md', `${line}\n`).length >= 1)
		})
	}

	it('lets a docs guide named like a specification page through, and refuses the page itself', () => {
		assert.deepEqual(checkText(hpac, 'see docs/issue-traceability.md\n'), [])
		assert.equal(checkText(hpac, 'see traceability.md\n').length, 1)
		assert.ok(checkText(hpac, 'see .spec/traceability.md\n').length >= 1)
	})

	it('refuses a claim ID in lower case', () => {
		assert.ok(checkText(hpac, 'see req-sub-012\n').length >= 1)
	})

	it('lets test files and plain prose through', () => {
		for (const line of ['foo.spec.ts', 'foo.spec.tsx', 'foo.spec.js', 'foo.spec.mjs', 'foo.spec.cjs', 'foo.spec.jsx', 'a lesson 2 days old', 'lessons 2 and 3', 'the pros and con a 5 times']) {
			assert.deepEqual(checkText(hpac, `${line}\n`), [], line)
		}
	})

	it('catches a reference wrapped across a line, once, at its first line', () => {
		for (const text of ['see lesson\n0003 here\n', 'see ADR-\n0001 here\n', 'REQ-SUB-\n012\n']) {
			const problems = checkText(hpac, text)

			assert.equal(problems.length, 1, text)
			assert.match(problems[0], /^skills\/hpac-x\/SKILL\.md:1:/)
		}
	})

	it('refuses a record reference in an agent and in a nested skill file', () => {
		const root = tree({
			'agents/a.md': '# A\nSee ADR-0001.\n',
			'skills/hpac-x/SKILL.md': '# X\n',
			'skills/hpac-x/agents/openai.yaml': 'description: see .spec/x\n',
		})

		const { code, errors } = runMain(root)

		assert.equal(code, 1)
		assert.equal(errors.length, 2)
		assert.ok(errors.some((e) => e.includes('file=agents/a.md,line=2')))
		assert.ok(errors.some((e) => e.includes('file=skills/hpac-x/agents/openai.yaml,line=1')))
	})
})
