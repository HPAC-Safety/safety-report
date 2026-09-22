import { describe, it } from 'node:test'
import assert from 'node:assert/strict'
import { mkdtempSync, mkdirSync, writeFileSync } from 'node:fs'
import { tmpdir } from 'node:os'
import { join } from 'node:path'

import { CONSTRAINT_PAGES, build, main, readClaims, readConstraints, render } from '../../tools/traceability.mjs'

/** Runs `main` with console output captured, restoring it afterwards even on failure. */
function runMain(root) {
	const output = { log: [], error: [] }
	const original = { log: console.log, error: console.error }
	console.log = (...args) => output.log.push(args.join(' '))
	console.error = (...args) => output.error.push(args.join(' '))
	try {
		return { code: main(root, { write: false }), output }
	} finally {
		console.log = original.log
		console.error = original.error
	}
}

const scenario = (id, name, tags = []) => [`@${id}`, ...tags, `Scenario: ${name}`, '  Given a thing', '  Then it holds', ''].join('\n')

/**
 * A throwaway repository: one feature file under the named area, plus every
 * constraint page the generator reads, so only the page under test carries
 * anything.
 */
function repository({ area = 'media', feature = '', pages = {} } = {}) {
	const root = mkdtempSync(join(tmpdir(), 'traceability-'))
	mkdirSync(join(root, 'features', area), { recursive: true })
	writeFileSync(join(root, 'features', area, `${area}.feature`), `Feature: ${area}\n\n${feature}`)
	mkdirSync(join(root, 'docs'), { recursive: true })
	for (const page of CONSTRAINT_PAGES) writeFileSync(join(root, page), pages[page] ?? '# A page\n')
	return root
}

describe('readClaims', () => {
	it('reads the claim, area, engine, and status of each scenario', () => {
		const { claims, problems } = readClaims('features/media/media.feature', `Feature: Media\n\n${scenario('REQ-MED-001', 'A thing happens')}`)

		assert.deepEqual(problems, [])
		assert.deepEqual(claims, [{ id: 'REQ-MED-001', area: 'media', scenario: 'A thing happens', engine: 'Reqnroll', status: 'Covered' }])
	})

	it('routes a @ui scenario to playwright-bdd and marks an @ignore one Planned', () => {
		const source = `Feature: Media\n\n${scenario('REQ-MED-002', 'A browser thing', ['@ignore', '@ui'])}`
		const { claims } = readClaims('features/media/media.feature', source)

		assert.equal(claims[0].engine, 'playwright-bdd')
		assert.equal(claims[0].status, 'Planned')
	})

	it('keeps the claim attached to a Scenario Outline', () => {
		const source = 'Feature: Media\n\n@REQ-MED-003\nScenario Outline: A table thing\n  Given <a>\n\nExamples:\n  | a |\n  | 1 |\n'
		const { claims } = readClaims('features/media/media.feature', source)

		assert.equal(claims.length, 1)
		assert.equal(claims[0].scenario, 'A table thing')
	})

	it('reports a scenario carrying no claim ID', () => {
		const { claims, problems } = readClaims('features/media/media.feature', 'Feature: Media\n\nScenario: An untagged thing\n  Given a thing\n')

		assert.deepEqual(claims, [])
		assert.match(problems[0], /carries no claim ID/)
	})

	it('reports a scenario carrying more than one', () => {
		const source = 'Feature: Media\n\n@REQ-MED-004\n@REQ-MED-005\nScenario: A greedy thing\n  Given a thing\n'
		const { problems } = readClaims('features/media/media.feature', source)

		assert.match(problems[0], /carries 2 claim IDs/)
	})
})

describe('readConstraints', () => {
	it('reads a constraint and the claims that verify it', () => {
		const source = '**CON-SO-001** A thing is true.\n*Verified by: REQ-MED-001, REQ-MED-002.*\n'
		const { constraints, problems } = readConstraints('docs/system-overview.md', source)

		assert.deepEqual(problems, [])
		assert.deepEqual(constraints[0].verifiedBy, ['REQ-MED-001', 'REQ-MED-002'])
	})

	it('accepts "none" with a reason and records no claims', () => {
		const source = '**CON-SO-002** A thing is true.\n*Verified by: none — nothing observes it.*\n'
		const { constraints } = readConstraints('docs/system-overview.md', source)

		assert.deepEqual(constraints[0].verifiedBy, [])
		assert.match(constraints[0].note, /nothing observes it/)
	})

	it('reports a constraint that names nothing at all', () => {
		const { problems } = readConstraints('docs/system-overview.md', '**CON-SO-003** A thing is true.\n')

		assert.match(problems[0], /names nothing that verifies it/)
	})

	it('stops one constraint from swallowing the next one reason', () => {
		const source = '**CON-SO-004** First.\n*Verified by: REQ-MED-001.*\n\n**CON-SO-005** Second.\n*Verified by: REQ-MED-002.*\n'
		const { constraints } = readConstraints('docs/system-overview.md', source)

		assert.deepEqual(constraints.map((constraint) => constraint.verifiedBy), [['REQ-MED-001'], ['REQ-MED-002']])
	})
})

describe('build', () => {
	it('fails on a claim ID used twice', () => {
		const feature = scenario('REQ-MED-001', 'First') + scenario('REQ-MED-001', 'Second')
		const { problems } = build(repository({ feature }))

		assert.match(problems.join('\n'), /REQ-MED-001 is used twice/)
	})

	it('fails on a constraint naming a claim no scenario declares', () => {
		const root = repository({
			feature: scenario('REQ-MED-001', 'First'),
			pages: { 'docs/system-overview.md': '**CON-SO-001** A thing.\n*Verified by: REQ-MED-999.*\n' },
		})

		assert.match(build(root).problems.join('\n'), /names REQ-MED-999, which no scenario declares/)
	})

	it('fails on a constraint declared twice', () => {
		const root = repository({
			feature: scenario('REQ-MED-001', 'First'),
			pages: { 'docs/system-overview.md': '**CON-SO-001** A.\n*Verified by: none — x.*\n\n**CON-SO-001** B.\n*Verified by: none — y.*\n' },
		})

		assert.match(build(root).problems.join('\n'), /CON-SO-001 is declared twice/)
	})
})

describe('render', () => {
	it('escapes a pipe so one scenario name cannot forge a table column', () => {
		const matrix = render([{ id: 'REQ-MED-001', area: 'media', scenario: 'A | B', engine: 'Reqnroll', status: 'Covered' }], [])

		assert.match(matrix, /A \\\| B/)
	})

	it('opens with frontmatter and says it is generated', () => {
		const matrix = render([], [])

		assert.ok(matrix.startsWith('---\n'))
		assert.match(matrix, /do not edit by hand/)
	})
})

describe('main', () => {
	it('passes a repository whose claims and constraints line up', () => {
		const root = repository({
			feature: scenario('REQ-MED-001', 'A thing happens'),
			pages: { 'docs/system-overview.md': '**CON-SO-001** A thing.\n*Verified by: REQ-MED-001.*\n' },
		})

		const { code, output } = runMain(root)

		assert.equal(code, 0)
		assert.match(output.log.join('\n'), /1 claim\(s\) and 1 constraint\(s\)/)
	})

	it('fails and annotates an untagged scenario', () => {
		const { code, output } = runMain(repository({ feature: 'Scenario: Untagged\n  Given a thing\n' }))

		assert.equal(code, 1)
		assert.match(output.error.join('\n'), /::error::.*carries no claim ID/)
	})
})
