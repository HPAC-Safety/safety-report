import { describe, it } from 'node:test'
import assert from 'node:assert/strict'
import { mkdtempSync, mkdirSync, writeFileSync } from 'node:fs'
import { tmpdir } from 'node:os'
import { dirname, join } from 'node:path'

import { checkSkillName, checkText, instructionFiles, main, skillNames } from '../../../tools/docs/check-generic-instructions.ts'

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
	it('passes a file that references no record', () => {
		assert.deepEqual(checkText('skills/hpac-x/SKILL.md', '# Deliver a change\n\nRebase before every commit.\n'), [])
	})
})

describe('main', () => {
	it('passes when every listed file is clean', () => {
		const root = tree({ 'skills/hpac-x/SKILL.md': '# A\nPlain.\n' })

		assert.equal(runMain(root, ['skills/hpac-x/SKILL.md']).code, 0)
	})

	it('fails and annotates the line when a listed file references a record', () => {
		const root = tree({ 'skills/hpac-x/SKILL.md': '# A\nRead ADR-0001.\n' })

		const { code, errors } = runMain(root, ['skills/hpac-x/SKILL.md'])

		assert.equal(code, 1)
		assert.match(errors[0], /^::error file=skills\/hpac-x\/SKILL\.md,line=2::references a decision record by number/)
	})

	it('fails when a listed file no longer exists, so a rename updates the list', () => {
		const { code, errors } = runMain(tree({}), ['skills/gone/SKILL.md'])

		assert.equal(code, 1)
		assert.match(errors[0], /listed but does not exist/)
	})

	it('checks the real repository by default', () => {
		assert.ok(instructionFiles(process.cwd()).length > 0)
		assert.equal(runMain(process.cwd()).code, 0)
	})
})

describe('instructionFiles', () => {
	it('selects every text file of every skill, and nothing else', () => {
		const root = tree({
			'agents/a.md': '# A\n',
			'skills/hpac-x/SKILL.md': '# S\n',
			'skills/hpac-x/references/format.md': '# F\n',
			'skills/hpac-x/agents/openai.yaml': 'name: r\n',
			'skills/hpac-x/logo.png': 'binary',
		})

		assert.deepEqual(instructionFiles(root), ['skills/hpac-x/SKILL.md', 'skills/hpac-x/agents/openai.yaml', 'skills/hpac-x/references/format.md'])
	})

	it('fails a record reference in a skill\'s supporting file', () => {
		const root = tree({ 'skills/hpac-x/SKILL.md': '# R\n', 'skills/hpac-x/references/x.md': 'See ADR-0001.\n' })

		const { code, errors } = runMain(root)

		assert.equal(code, 1)
		assert.match(errors[0], /file=skills\/hpac-x\/references\/x\.md,line=1/)
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
		it(`refuses a skill line that ${why}`, () => {
			const problems = checkText('skills/hpac-x/SKILL.md', `# X\n${line}\n`)

			assert.ok(problems.length >= 1, line)
			assert.match(problems[0], new RegExp(`^skills/hpac-x/SKILL\\.md:2: ${why}`))
		})
	}

	it('lets an hpac skill name the product, tools, and source paths', () => {
		assert.deepEqual(checkText('skills/hpac-x/SKILL.md', 'Run node tools/spec/x.ts; edit src/HpacSafety.Worker; the pilot, the Worker.\n'), [])
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

	it('refuses a record reference in a nested skill file', () => {
		const root = tree({
			'skills/hpac-x/SKILL.md': '# X\n',
			'skills/hpac-x/agents/openai.yaml': 'description: see .spec/x\n',
		})

		const { code, errors } = runMain(root)

		assert.equal(code, 1)
		assert.equal(errors.length, 1)
		assert.ok(errors.some((e) => e.includes('file=skills/hpac-x/agents/openai.yaml,line=1')))
	})
})

describe('skill names', () => {
	it('passes a name that says hpac, anywhere in it', () => {
		for (const name of ['hpac-domain-model', 'test-hpac-safety', 'anonymize-hpac-reports']) assert.deepEqual(checkSkillName(name), [], name)
	})

	it('refuses a generic name, and a name that only contains the letters', () => {
		for (const name of ['coding-conventions', 'chpac', 'hpacs']) {
			const problems = checkSkillName(name)

			assert.equal(problems.length, 1, name)
			assert.match(problems[0], /skills\/.+: a skill here is project-specific, so its name says hpac; a generic skill belongs in agent-team/)
		}
	})

	it('fails main on a generic skill directory, even when its files are clean', () => {
		const root = tree({ 'skills/coding-conventions/SKILL.md': '# C\n', 'skills/hpac-x/SKILL.md': '# X\n' })

		const { code, errors } = runMain(root)

		assert.equal(code, 1)
		assert.equal(errors.length, 1)
		assert.match(errors[0], /file=skills\/coding-conventions,line=1::skills\/coding-conventions: a skill here is project-specific/)
	})

	it('lists only directories, so a stray file is not a skill', () => {
		const root = tree({ 'skills/README.md': '# R\n', 'skills/hpac-x/SKILL.md': '# X\n' })

		assert.deepEqual(skillNames(root), ['hpac-x'])
		assert.equal(runMain(root).code, 0)
	})
})
