import { describe, it } from 'node:test'
import assert from 'node:assert/strict'
import { mkdtempSync, mkdirSync, writeFileSync } from 'node:fs'
import { spawnSync } from 'node:child_process'
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

	it('reads engine and status from the scenario own tags, never a neighbour', () => {
		const source = `Feature: Media\n\n${scenario('REQ-MED-006', 'A browser thing', ['@ignore', '@ui'])}${scenario('REQ-MED-007', 'A server thing')}`
		const { claims } = readClaims('features/media/media.feature', source)

		assert.deepEqual(
			claims.map(({ id, engine, status }) => ({ id, engine, status })),
			[
				{ id: 'REQ-MED-006', engine: 'playwright-bdd', status: 'Planned' },
				{ id: 'REQ-MED-007', engine: 'Reqnroll', status: 'Covered' },
			],
		)
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

const claim = (id, area, status = 'Covered') => ({ id, area, scenario: `Scenario ${id}`, engine: 'Reqnroll', status })

/**
 * git's own three-way merge of one generated file, as a PR branch and main
 * would meet: `git merge-file` exits 0 on a clean merge and with the count of
 * conflicts otherwise.
 */
function mergeMatrices(base, ours, theirs) {
	const dir = mkdtempSync(join(tmpdir(), 'traceability-merge-'))
	const files = Object.entries({ base, ours, theirs }).map(([name, content]) => {
		writeFileSync(join(dir, name), content)
		return join(dir, name)
	})
	const result = spawnSync('git', ['merge-file', '-p', files[1], files[0], files[2]], { encoding: 'utf8' })
	return { conflicts: result.status, merged: result.stdout }
}

describe('render', () => {
	it('opens with frontmatter and says it is generated', () => {
		const matrix = render([], [])

		assert.ok(matrix.startsWith('---\n'))
		assert.match(matrix, /do not edit by hand/)
	})

	it('gives each claim its own heading and one data line', () => {
		const matrix = render([{ id: 'REQ-MED-001', area: 'media', scenario: 'A | B', engine: 'playwright-bdd', status: 'Planned' }], [])

		assert.match(matrix, /## Claims: media\n\n### REQ-MED-001\n\nA \| B — \*playwright-bdd, Planned\*\n/)
	})

	it('keeps a scenario name that opens with # from becoming a heading', () => {
		const matrix = render([{ ...claim('REQ-MED-001', 'media'), scenario: '# of files' }], [])

		assert.match(matrix, /^\\# of files — /m)
	})

	it('carries no count taken across the whole tree', () => {
		const matrix = render([claim('REQ-MED-001', 'media'), claim('REQ-MED-002', 'media', 'Planned')], [])

		assert.doesNotMatch(matrix, /\d+ claims|\d+ constraints|covered by a step definition/)
	})

	it('orders claims by area then ID, whatever order the feature files declare them in', () => {
		const matrix = render([claim('REQ-SUB-002', 'report-submission'), claim('REQ-MED-010', 'media'), claim('REQ-SUB-001', 'report-submission'), claim('REQ-MED-002', 'media')], [])

		assert.deepEqual(matrix.match(/(?<=^### )REQ-[A-Z]+-\d{3}/gm), ['REQ-MED-002', 'REQ-MED-010', 'REQ-SUB-001', 'REQ-SUB-002'])
	})

	it('orders constraints by ID and names what verifies each', () => {
		const matrix = render([], [
			{ id: 'CON-SO-002', page: 'docs/system-overview.md', verifiedBy: [], note: 'none — a reason' },
			{ id: 'CON-SO-001', page: 'docs/system-overview.md', verifiedBy: ['REQ-MED-001'], note: 'REQ-MED-001' },
		])

		assert.match(matrix, /### CON-SO-001\n\nsystem-overview.md — verified by `REQ-MED-001`\n\n### CON-SO-002\n\nsystem-overview.md — verified by none — a reason\n/)
	})
})

describe('render merges the way the tree does', () => {
	const base = [claim('REQ-DOM-001', 'domain-and-lifecycle', 'Planned'), claim('REQ-DOM-002', 'domain-and-lifecycle', 'Planned'), claim('REQ-MED-001', 'media')]
	const changed = (claims, id, change) => claims.map((each) => (each.id === id ? { ...each, ...change } : each))

	it('merges status changes to two neighbouring claims cleanly into the combined matrix', () => {
		const ours = changed(base, 'REQ-DOM-001', { status: 'Covered' })
		const theirs = changed(base, 'REQ-DOM-002', { status: 'Covered' })
		const combined = changed(ours, 'REQ-DOM-002', { status: 'Covered' })

		const { conflicts, merged } = mergeMatrices(render(base, []), render(ours, []), render(theirs, []))

		assert.equal(conflicts, 0)
		assert.equal(merged, render(combined, []))
	})

	it('merges new claims in two different areas cleanly into the combined matrix', () => {
		const ours = [...base, claim('REQ-DOM-003', 'domain-and-lifecycle')]
		const theirs = [...base, claim('REQ-MED-002', 'media')]

		const { conflicts, merged } = mergeMatrices(render(base, []), render(ours, []), render(theirs, []))

		assert.equal(conflicts, 0)
		assert.equal(merged, render([...ours, claim('REQ-MED-002', 'media')], []))
	})

	it('merges a new claim beside a neighbour whose status changed', () => {
		const ours = [...base, claim('REQ-DOM-003', 'domain-and-lifecycle')]
		const theirs = changed(base, 'REQ-DOM-002', { status: 'Covered' })

		const { conflicts, merged } = mergeMatrices(render(base, []), render(ours, []), render(theirs, []))

		assert.equal(conflicts, 0)
		assert.equal(merged, render([...changed(base, 'REQ-DOM-002', { status: 'Covered' }), claim('REQ-DOM-003', 'domain-and-lifecycle')], []))
	})

	it('still conflicts when two branches claim the same new ID', () => {
		const ours = [...base, { ...claim('REQ-DOM-003', 'domain-and-lifecycle'), scenario: 'One thing' }]
		const theirs = [...base, { ...claim('REQ-DOM-003', 'domain-and-lifecycle'), scenario: 'Another thing' }]

		const { conflicts } = mergeMatrices(render(base, []), render(ours, []), render(theirs, []))

		assert.ok(conflicts > 0)
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
		assert.match(output.log.join('\n'), /1 claims across 1 areas: 1 covered/)
	})

	it('fails and annotates an untagged scenario', () => {
		const { code, output } = runMain(repository({ feature: 'Scenario: Untagged\n  Given a thing\n' }))

		assert.equal(code, 1)
		assert.match(output.error.join('\n'), /::error::.*carries no claim ID/)
	})
})
