import { describe, it } from 'node:test'
import assert from 'node:assert/strict'
import { mkdtempSync, mkdirSync, writeFileSync } from 'node:fs'
import { tmpdir } from 'node:os'
import { dirname, join } from 'node:path'

import { checkAdr, checkConvention, checkLesson, checkRecords, firstBlock, headings, main } from '../../../tools/spec/check-records.ts'

const front = (status = 'accepted'): string => `---\ntitle: A\ndescription: B.\ntype: adr\nstatus: ${status}\ndate: 2026-10-03\ndecision-makers: Someone\nkeywords: a\n---\n\n`

/** An ADR built from its status line and its sections, each `[heading, body]`. */
const adr = (number: string, statusLine: string, sections: readonly string[], status = 'accepted'): string =>
	`${front(status)}# ADR-${number} — A\n\n${statusLine}\n\n${sections.map((heading) => `## ${heading}\n\nText.\n`).join('\n')}`

const TEMPLATED = ['Context', 'Decision drivers', 'Considered options', 'Decision', 'Consequences', 'Related']

describe('headings and firstBlock', () => {
	it('ignores a heading inside a fenced code block', () => {
		assert.deepEqual(headings('## One\n\n```md\n## Not a heading\n```\n\n## Two\n'), ['One', 'Two'])
	})

	it('reads the first block under the title, skipping the frontmatter', () => {
		assert.equal(firstBlock(`${front()}# ADR-0001 — A\n\n**Status:** Accepted, and\nstill going.\n\n## Context\n`), '**Status:** Accepted, and\nstill going.')
		assert.equal(firstBlock('no heading at all'), '')
	})
})

describe('checkAdr, on a record older than the template', () => {
	const name = 'ADR-0042-old.md'

	it('passes a record with its status line, considered options, and its own sections', () => {
		assert.deepEqual(checkAdr(name, adr('0042', '**Status:** Accepted.', ['Context', 'Decision', 'Why this', 'Considered options', 'Consequences', 'Amendment (2026-09-30) — a later note'])), [])
	})

	it('fails a status that is not the first block, or is a section', () => {
		const problems = checkAdr(name, `${front()}# ADR-0042 — A\n\n## Status\n\nAccepted.\n\n## Considered options\n\nNone.\n`)

		assert.ok(problems.some((problem) => /first block under the heading is not its \*\*Status:\*\* line/.test(problem)))
		assert.ok(problems.some((problem) => /a "## Status" section/.test(problem)))
	})

	it('fails two status lines', () => {
		const text = adr('0042', '**Status:** Accepted.\n\n**Status:** Superseded.', ['Considered options'])

		assert.ok(checkAdr(name, text).some((problem) => /more than one \*\*Status:\*\* line/.test(problem)))
	})

	it('fails a record with no considered options, or two', () => {
		assert.match(checkAdr(name, adr('0042', '**Status:** Accepted.', ['Context', 'Decision']))[0], /no "## Considered options" section —/)
		assert.match(checkAdr(name, adr('0042', '**Status:** Accepted.', ['Considered options', 'Considered options']))[0], /2 "## Considered options" sections/)
	})

	it('fails an amendment heading in any other form', () => {
		for (const heading of ['Amendment', 'Amendment, 2026-09-22 — a note', 'Second amendment (2026-09-28)', 'Amendment (2026-09-30): a note']) {
			const problems = checkAdr(name, adr('0042', '**Status:** Accepted.', ['Considered options', heading]))
			assert.match(problems[0], /an amendment heading reads/, heading)
		}
	})

	it('accepts a retired partially-superseded status on an old record', () => {
		assert.deepEqual(checkAdr(name, adr('0042', '**Status:** Accepted, partially superseded by [ADR-0050](ADR-0050-x.md).', ['Considered options'], 'partially-superseded')), [])
	})
})

describe('checkAdr, on a templated record', () => {
	const name = 'ADR-0192-new.md'

	it('passes the template, with its optional sections or without them', () => {
		assert.deepEqual(checkAdr(name, adr('0192', '**Status:** Accepted. Decided today.', TEMPLATED)), [])
		assert.deepEqual(checkAdr(name, adr('0192', '**Status:** Accepted.', ['Context', 'Considered options', 'Decision', 'Consequences'])), [])
	})

	it('fails a section the template does not have, an amendment included', () => {
		const problems = checkAdr(name, adr('0192', '**Status:** Accepted.', [...TEMPLATED, 'Amendment (2026-10-04)']))

		assert.equal(problems.length, 1)
		assert.match(problems[0], /"## Amendment \(2026-10-04\)" — a record follows TEMPLATE\.md/)
	})

	it('fails a missing required section, and sections out of order', () => {
		const missing = checkAdr(name, adr('0192', '**Status:** Accepted.', ['Context', 'Considered options', 'Decision']))
		const reordered = checkAdr(name, adr('0192', '**Status:** Accepted.', ['Context', 'Decision', 'Considered options', 'Consequences']))

		assert.deepEqual(missing, [`.spec/decisions/${name}: no "## Consequences" section (TEMPLATE.md, ADR-0192)`])
		assert.match(reordered[0], /sections out of order/)
	})

	it('refuses partially-superseded, and a status line that does not open with its status', () => {
		const partial = checkAdr(name, adr('0192', '**Status:** Partially superseded by [ADR-0192](ADR-0192-x.md).', TEMPLATED, 'partially-superseded'))
		const disagreeing = checkAdr(name, adr('0192', '**Status:** Accepted.', TEMPLATED, 'superseded'))

		assert.match(partial[0], /is retired/)
		assert.match(disagreeing[0], /does not open with "Superseded"/)
		assert.deepEqual(checkAdr(name, adr('0192', '**Status:** Superseded by [ADR-0192](ADR-0192-x.md).', TEMPLATED, 'superseded')), [])
	})
})

describe('checkLesson', () => {
	const skills = new Set(['deliver-change', 'test-from-scenarios'])
	const conventions = new Set(['CONV-001'])
	const lesson = (kind: string, sections: Record<string, string>): string =>
		`---\ntitle: A\ndescription: B.\ntype: lesson\ndate: 2026-10-03\nissue: 1\nstatus: accepted\nkind: ${kind}\n---\n\n# Lesson 0001 — A\n\n${Object.entries(sections)
			.map(([heading, text]) => `## ${heading}\n\n${text}\n`)
			.join('\n')}`
	const full = (overrides: Record<string, string> = {}): Record<string, string> => ({
		Symptom: 'It broke.',
		'Root cause': 'Why.',
		'Spec delta': 'REQ-MED-001 now says so.',
		Scenario: 'REQ-MED-001.',
		Skill: 'None — the claim is the remedy.',
		...overrides,
	})
	const check = (text: string): string[] => checkLesson('0001-a.md', text, skills, conventions)

	it('passes a product lesson whose spec delta names a claim or constraint', () => {
		assert.deepEqual(check(lesson('product', full())), [])
		assert.deepEqual(check(lesson('product', full({ 'Spec delta': 'CON-INF-027 now orders the upload.' }))), [])
	})

	it('fails a product lesson whose spec delta names no claim', () => {
		assert.match(check(lesson('product', full({ 'Spec delta': 'An ADR.' })))[0], /names the REQ- or CON- claim/)
	})

	it('passes a process lesson naming a skill that exists, or a convention that exists', () => {
		assert.deepEqual(check(lesson('process', full({ Skill: '[`deliver-change`](x) now says so.' }))), [])
		assert.deepEqual(check(lesson('process', full({ Skill: 'CONV-001 now says so.' }))), [])
	})

	it('fails a process lesson naming no skill, or one that does not exist', () => {
		assert.match(check(lesson('process', full({ Skill: 'None.' })))[0], /names the skill or convention it changed/)
		assert.match(check(lesson('process', full({ Skill: '`no-such-skill` and CONV-009.' })))[0], /names the skill or convention it changed/)
	})

	it('needs only a symptom and a root cause for an incident', () => {
		assert.deepEqual(check(lesson('incident', { Symptom: 'It broke.', 'Root cause': 'Why.' })), [])
		assert.match(check(lesson('incident', { Symptom: 'It broke.' }))[0], /an incident lesson needs "## Root cause"/)
	})

	it('needs all five sections for a product or process lesson', () => {
		const sections = full()
		delete sections.Skill

		assert.match(check(lesson('product', sections))[0], /a product lesson needs "## Skill"/)
	})

	it('fails a kind outside the three, another section, a repeated one, and sections out of order', () => {
		assert.match(check(lesson('bug', full()))[0], /"kind: bug" is not one of product, process, incident/)
		assert.match(check(lesson('product', full({ Fix: 'Done.' })))[0], /"## Fix" — a lesson has only/)
		assert.ok(check(lesson('incident', { Symptom: 'a', 'Root cause': 'b' }).concat('\n## Symptom\n\nAgain.\n')).some((problem) => /"## Symptom" appears twice/.test(problem)))
		assert.match(check(lesson('incident', { 'Root cause': 'b', Symptom: 'a' }))[0], /sections out of order/)
	})
})

describe('checkConvention', () => {
	const convention = (number = '001', overrides = ''): string =>
		`---\ntitle: A rule\ndescription: B.\ntype: convention\nstatus: accepted\ndate: 2026-10-03\n${overrides}---\n\n# CONV-${number} — A rule\n\n## Rule\n\n- Do it.\n\n## Why\n\nBecause.\n`

	it('passes a well-formed convention', () => {
		assert.deepEqual(checkConvention('CONV-001-a-rule.md', convention()), [])
	})

	it('fails a bad name, a heading that disagrees, and missing sections', () => {
		assert.match(checkConvention('CONV-1-a-rule.md', convention())[0], /not named CONV-NNN-kebab-slug\.md/)
		assert.match(checkConvention('CONV-002-a-rule.md', convention('001'))[0], /the file says CONV-002 and the heading says CONV-001/)
		assert.match(checkConvention('CONV-001-a-rule.md', convention().replace('## Why\n\nBecause.\n', '## Notes\n\nx\n')).join('\n'), /"## Notes" — a convention has[\s\S]*no "## Why" section/)
	})

	it('fails a wrong type, status, or date', () => {
		const text = convention().replace('type: convention', 'type: guide').replace('status: accepted', 'status: proposed').replace('date: 2026-10-03', 'date: soon')

		assert.equal(checkConvention('CONV-001-a-rule.md', text).length, 3)
	})
})

describe('checkRecords and main', () => {
	/** A throwaway repository with the named files. */
	function tree(files: Record<string, string>): string {
		const root = mkdtempSync(join(tmpdir(), 'check-records-'))
		for (const [path, text] of Object.entries(files)) {
			mkdirSync(dirname(join(root, path)), { recursive: true })
			writeFileSync(join(root, path), text)
		}
		return root
	}

	const goodAdr = adr('0001', '**Status:** Accepted.', ['Considered options'])
	const convention = (number: string): string => `---\ntitle: A\ndescription: B.\ntype: convention\nstatus: accepted\ndate: 2026-10-03\n---\n\n# CONV-${number} — A\n\n## Rule\n\nx\n\n## Why\n\ny\n`

	it('passes a clean tree, counting each kind of record', () => {
		const root = tree({
			'.spec/decisions/ADR-0001-a.md': goodAdr,
			'.spec/decisions/README.md': 'not a record',
			'.spec/decisions/TEMPLATE.md': 'not a record',
			'.spec/lessons/README.md': 'not a lesson',
			'.spec/conventions/README.md': 'not a convention',
			'.spec/conventions/CONV-001-a.md': convention('001'),
			'skills/deliver-change/SKILL.md': 'x',
		})

		assert.deepEqual(checkRecords(root), { problems: [], counts: { adrs: 1, lessons: 0, conventions: 1 } })
	})

	it('passes a tree with no conventions directory', () => {
		assert.deepEqual(checkRecords(tree({ '.spec/decisions/ADR-0001-a.md': goodAdr })).problems, [])
	})

	it('fails two conventions sharing a number', () => {
		const root = tree({ '.spec/conventions/CONV-001-a.md': convention('001'), '.spec/conventions/CONV-001-b.md': convention('001') })

		assert.match(checkRecords(root).problems[0], /CONV-001 is already taken by CONV-001-a\.md/)
	})

	it('reports each problem as an annotation and exits 1, or exits 0 when clean', () => {
		const errors: string[] = []
		const logs: string[] = []
		const original = { error: console.error, log: console.log }
		console.error = (line: string) => errors.push(line)
		console.log = (line: string) => logs.push(line)
		try {
			assert.equal(main(tree({ '.spec/decisions/ADR-0001-a.md': adr('0001', '**Status:** Accepted.', ['Context']) })), 1)
			assert.equal(main(tree({ '.spec/decisions/ADR-0001-a.md': goodAdr })), 0)
		} finally {
			console.error = original.error
			console.log = original.log
		}

		assert.match(errors[0], /^::error file=\.spec\/decisions\/ADR-0001-a\.md::no "## Considered options"/)
		assert.match(logs[0], /1 decision record\(s\), 0 lesson\(s\), and 0 convention\(s\) keep their shape/)
	})
})
