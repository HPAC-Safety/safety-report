import { describe, it } from 'node:test'
import assert from 'node:assert/strict'
import { mkdirSync, mkdtempSync, writeFileSync } from 'node:fs'
import { tmpdir } from 'node:os'
import { join } from 'node:path'

import { RETIRED_PREFIXES, areaPrefixes, main, nextClaim, prefixOf, prefixProblems } from '../../../tools/spec/claim-prefixes.ts'

const RETIRED = { 'REQ-QB': 268 }
const prefixes = (entries: Record<string, string | undefined>): Map<string, string | undefined> => new Map(Object.entries(entries))

/** A throwaway specification with one area per entry: its README's prefix, and its scenarios' IDs. */
function tree(areas: Record<string, { prefix?: string; claims: string[] }>): string {
	const root = mkdtempSync(join(tmpdir(), 'claim-prefixes-'))
	for (const [area, { prefix, claims }] of Object.entries(areas)) {
		mkdirSync(join(root, '.spec/features', area), { recursive: true })
		writeFileSync(join(root, '.spec/features', area, 'README.md'), `---\ntitle: ${area}\ndescription: An area.\ntype: spec\narea: ${area}\n${prefix ? `prefix: ${prefix}\n` : ''}---\n`)
		writeFileSync(join(root, '.spec/features', area, `${area}.feature`), `Feature: ${area}\n\n${claims.map((id) => `@${id}\nScenario: ${id}\n  Given a thing\n`).join('\n')}`)
	}
	return root
}

describe('prefixOf', () => {
	it('drops the number', () => {
		assert.equal(prefixOf('REQ-QB-001'), 'REQ-QB')
		assert.equal(prefixOf('REQ-AUTH-012'), 'REQ-AUTH')
	})
})

describe('RETIRED_PREFIXES', () => {
	it('retires the two areas #814 split, at their last numbers', () => {
		assert.deepEqual(RETIRED_PREFIXES, { 'REQ-MOD': 211, 'REQ-QB': 268 })
	})
})

describe('prefixProblems', () => {
	it('passes a claim under its area\'s prefix, a moved claim under another area\'s, and one under a retired prefix', () => {
		const claims = [
			{ id: 'REQ-QAU-001', area: 'question-authoring' },
			{ id: 'REQ-RFM-001', area: 'question-authoring' },
			{ id: 'REQ-QB-001', area: 'question-authoring' },
			{ id: 'REQ-QB-268', area: 'report-form' },
		]

		assert.deepEqual(prefixProblems(claims, prefixes({ 'question-authoring': 'REQ-QAU', 'report-form': 'REQ-RFM' }), RETIRED), [])
	})

	it('fails a new number under a retired prefix, and a prefix no area declares', () => {
		const claims = [
			{ id: 'REQ-QB-269', area: 'question-authoring' },
			{ id: 'REQ-ZZZ-001', area: 'question-authoring' },
		]

		const problems = prefixProblems(claims, prefixes({ 'question-authoring': 'REQ-QAU', 'report-form': 'REQ-RFM' }), RETIRED)

		assert.match(problems[0], /REQ-QB-269: REQ-QB retired at REQ-QB-268, so a new claim in \.spec\/features\/question-authoring takes REQ-QAU/)
		assert.match(problems[1], /REQ-ZZZ-001: no area declares REQ-ZZZ and it is not retired; a new claim in \.spec\/features\/question-authoring takes REQ-QAU/)
		assert.equal(problems.length, 2)
	})

	it('fails an area with no prefix, a malformed one, a shared one, and a retired one', () => {
		const problems = prefixProblems([], prefixes({ a: undefined, b: 'QB', c: 'REQ-X', d: 'REQ-X', e: 'REQ-QB' }), RETIRED)

		assert.match(problems[0], /features\/a\/README\.md: declares no "prefix:"/)
		assert.match(problems[1], /features\/b\/README\.md: "prefix: QB" is not of the form REQ-<AREA>/)
		assert.match(problems[2], /features\/d\/README\.md: "prefix: REQ-X" is already c's/)
		assert.match(problems[3], /features\/e\/README\.md: "prefix: REQ-QB" is retired/)
		assert.equal(problems.length, 4)
	})
})

describe('nextClaim', () => {
	it('takes the next number under the area\'s own prefix, wherever those claims live, and starts a new area at 001', () => {
		const ids = ['REQ-QAU-001', 'REQ-QAU-007', 'REQ-QB-268']
		const declared = prefixes({ 'question-authoring': 'REQ-QAU', 'report-form': 'REQ-RFM' })

		assert.equal(nextClaim('question-authoring', ids, declared), 'REQ-QAU-008')
		assert.equal(nextClaim('report-form', ids, declared), 'REQ-RFM-001')
		assert.throws(() => nextClaim('nowhere', ids, declared), /not an area with a declared prefix/)
	})
})

describe('areaPrefixes and main', () => {
	it('reads each area\'s prefix from its README, and prints the next ID above any a decision still cites', () => {
		const root = tree({
			'question-authoring': { prefix: 'REQ-QAU', claims: ['REQ-QB-001', 'REQ-QAU-002'] },
			unprefixed: { claims: [] },
		})
		mkdirSync(join(root, '.spec/decisions'), { recursive: true })
		writeFileSync(join(root, '.spec/decisions/ADR-0001-a.md'), '---\ntitle: A\nstatus: accepted\n---\n\n# ADR-0001 — A\n\nREQ-QAU-002 and the deleted REQ-QAU-003.\n')
		const out: string[] = []
		const err: string[] = []

		assert.deepEqual([...areaPrefixes(root)], [
			['question-authoring', 'REQ-QAU'],
			['unprefixed', undefined],
		])
		assert.equal(main(['--next', 'question-authoring'], root, (line) => out.push(line), (line) => err.push(line)), 0)
		assert.deepEqual(out, ['REQ-QAU-004'])
		assert.equal(main(['--next', 'unprefixed'], root, (line) => out.push(line), (line) => err.push(line)), 1)
		assert.equal(main([], root, (line) => out.push(line), (line) => err.push(line)), 2)
		assert.match(err.join('\n'), /features\/unprefixed is not an area with a declared "prefix:"[\s\S]*usage:/)
	})
})
