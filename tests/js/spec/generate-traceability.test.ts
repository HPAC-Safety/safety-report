import { describe, it } from 'node:test'
import assert from 'node:assert/strict'
import { copyFileSync, existsSync, mkdtempSync, mkdirSync, readFileSync, writeFileSync } from 'node:fs'
import { tmpdir } from 'node:os'
import { dirname, join } from 'node:path'
import { fileURLToPath } from 'node:url'

import {
	type ClaimRecord,
	type ClaimsData,
	type ConstraintRecord,
	CONSTRAINT_PAGES,
	build,
	difference,
	main,
	readClaims,
	readConstraints,
	render,
	serialize,
} from '../../../tools/spec/generate-traceability.ts'
import { type Schema, validate } from '../../../tools/spec/json-schema.ts'
import { CLAIMS, CLAIMS_SCHEMA, TRACEABILITY } from '../../../tools/spec/spec-paths.ts'
import { mergeFile } from '../helpers/merge-file.ts'

const REPO = join(dirname(fileURLToPath(import.meta.url)), '..', '..', '..')

/** Runs `main` with console output captured, restoring it afterwards even on failure. */
function runMain(root: string, options?: Parameters<typeof main>[1]): { code: number; output: { log: string[]; error: string[] } } {
	const output: { log: string[]; error: string[] } = { log: [], error: [] }
	const original = { log: console.log, error: console.error }
	console.log = (...args: unknown[]) => output.log.push(args.join(' '))
	console.error = (...args: unknown[]) => output.error.push(args.join(' '))
	try {
		return { code: main(root, options), output }
	} finally {
		console.log = original.log
		console.error = original.error
	}
}

const scenario = (id: string, name: string, tags: string[] = []): string => [`@${id}`, ...tags, `Scenario: ${name}`, '  Given a thing', '  Then it holds', ''].join('\n')

/**
 * A throwaway repository: one feature file under the named area, plus every
 * constraint page the generator reads, so only the page under test carries
 * anything.
 */
function repository({
	area = 'media',
	feature = '',
	pages = {},
	files = {},
}: { area?: string; feature?: string; pages?: Partial<Record<string, string>>; files?: Record<string, string> } = {}): string {
	const root = mkdtempSync(join(tmpdir(), 'traceability-'))
	mkdirSync(join(root, '.spec/features', area), { recursive: true })
	writeFileSync(join(root, '.spec/features', area, `${area}.feature`), `Feature: ${area}\n\n${feature}`)
	for (const page of CONSTRAINT_PAGES) writeFileSync(join(root, page), pages[page] ?? '# A page\n')
	copyFileSync(join(REPO, CLAIMS_SCHEMA), join(root, CLAIMS_SCHEMA))
	for (const [path, text] of Object.entries(files)) {
		mkdirSync(dirname(join(root, path)), { recursive: true })
		writeFileSync(join(root, path), text)
	}
	return root
}

/** A step definition binding every step `scenario` writes. */
const STEPS = { 'tests/HpacSafety.Acceptance.Tests/ThingSteps.cs': '[Binding]\npublic class ThingSteps\n{\n\t[Given(@"a thing")]\n\tpublic void A() { }\n\t[Then(@"it holds")]\n\tpublic void B() { }\n}\n' }

describe('readClaims', () => {
	it('reads the claim, area, engine, and status of each scenario', () => {
		const { claims, problems } = readClaims('.spec/features/media/media.feature', `Feature: Media\n\n${scenario('REQ-MED-001', 'A thing happens')}`)

		assert.deepEqual(problems, [])
		assert.deepEqual(claims, [{ id: 'REQ-MED-001', area: 'media', scenario: 'A thing happens', engine: 'Reqnroll', status: 'Covered', tags: [] }])
	})

	it('routes a @ui scenario to playwright-bdd and marks an @ignore one Planned', () => {
		const source = `Feature: Media\n\n${scenario('REQ-MED-002', 'A browser thing', ['@ignore', '@ui'])}`
		const { claims } = readClaims('.spec/features/media/media.feature', source)

		assert.equal(claims[0].engine, 'playwright-bdd')
		assert.equal(claims[0].status, 'Planned')
	})

	it('reads engine and status from the scenario own tags, never a neighbour', () => {
		const source = `Feature: Media\n\n${scenario('REQ-MED-006', 'A browser thing', ['@ignore', '@ui'])}${scenario('REQ-MED-007', 'A server thing')}`
		const { claims } = readClaims('.spec/features/media/media.feature', source)

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
		const { claims } = readClaims('.spec/features/media/media.feature', source)

		assert.equal(claims.length, 1)
		assert.equal(claims[0].scenario, 'A table thing')
	})

	it('reports a scenario carrying no claim ID', () => {
		const { claims, problems } = readClaims('.spec/features/media/media.feature', 'Feature: Media\n\nScenario: An untagged thing\n  Given a thing\n')

		assert.deepEqual(claims, [])
		assert.match(problems[0], /carries no claim ID/)
	})

	it('reports a scenario carrying more than one', () => {
		const source = 'Feature: Media\n\n@REQ-MED-004\n@REQ-MED-005\nScenario: A greedy thing\n  Given a thing\n'
		const { problems } = readClaims('.spec/features/media/media.feature', source)

		assert.match(problems[0], /carries 2 claim IDs/)
	})
})

describe('readClaims with Rule blocks', () => {
	it('reads claims inside Rules exactly as outside them', () => {
		const source = `Feature: Media\n\nRule: Uploads\n\n${scenario('REQ-MED-001', 'A thing happens', ['@ui'])}Rule: Review\n\n${scenario('REQ-MED-002', 'Another')}`
		const { claims, problems } = readClaims('.spec/features/media/media.feature', source)

		assert.deepEqual(problems, [])
		assert.deepEqual(claims.map((each) => [each.id, each.engine]), [['REQ-MED-001', 'playwright-bdd'], ['REQ-MED-002', 'Reqnroll']])
	})

	it('refuses a tag on a Rule, which would reach every scenario beneath it', () => {
		const source = `Feature: Media\n\n@ui\nRule: Uploads\n\n${scenario('REQ-MED-001', 'A thing happens')}`
		const { claims, problems } = readClaims('.spec/features/media/media.feature', source)

		assert.match(problems[0], /:4: tags on a Rule are not supported/)
		assert.equal(claims[0].engine, 'Reqnroll')
	})
})

describe('readConstraints', () => {
	it('reads a constraint and the claims that verify it', () => {
		const source = '**CON-SO-001** A thing is true.\n*Verified by: REQ-MED-001, REQ-MED-002.*\n'
		const { constraints, problems } = readConstraints('.spec/system-overview.md', source)

		assert.deepEqual(problems, [])
		assert.deepEqual(constraints[0].verifiedBy, ['REQ-MED-001', 'REQ-MED-002'])
	})

	it('accepts "none" with a reason and records no claims', () => {
		const source = '**CON-SO-002** A thing is true.\n*Verified by: none — nothing observes it.*\n'
		const { constraints } = readConstraints('.spec/system-overview.md', source)

		assert.deepEqual(constraints[0].verifiedBy, [])
		assert.match(constraints[0].note, /nothing observes it/)
	})

	it('reports a constraint that names nothing at all', () => {
		const { problems } = readConstraints('.spec/system-overview.md', '**CON-SO-003** A thing is true.\n')

		assert.match(problems[0], /names nothing that verifies it/)
	})

	it('stops one constraint from swallowing the next one reason', () => {
		const source = '**CON-SO-004** First.\n*Verified by: REQ-MED-001.*\n\n**CON-SO-005** Second.\n*Verified by: REQ-MED-002.*\n'
		const { constraints } = readConstraints('.spec/system-overview.md', source)

		assert.deepEqual(constraints.map((constraint) => constraint.verifiedBy), [['REQ-MED-001'], ['REQ-MED-002']])
	})
})


describe('build', () => {
	it('fails on a claim ID used twice', () => {
		const feature = scenario('REQ-MED-001', 'First') + scenario('REQ-MED-001', 'Second')
		const { problems } = build(repository({ feature, files: STEPS }))

		assert.match(problems.join('\n'), /REQ-MED-001 is used twice/)
	})

	it('fails on a constraint naming a claim no scenario declares', () => {
		const root = repository({
			feature: scenario('REQ-MED-001', 'First'),
			pages: { '.spec/system-overview.md': '**CON-SO-001** A thing.\n*Verified by: REQ-MED-999.*\n' },
			files: STEPS,
		})

		assert.match(build(root).problems.join('\n'), /names REQ-MED-999, which no scenario declares/)
	})

	it('fails on a constraint declared twice', () => {
		const root = repository({
			feature: scenario('REQ-MED-001', 'First'),
			pages: { '.spec/system-overview.md': '**CON-SO-001** A.\n*Verified by: none — x.*\n\n**CON-SO-001** B.\n*Verified by: none — y.*\n' },
			files: STEPS,
		})

		assert.match(build(root).problems.join('\n'), /CON-SO-001 is declared twice/)
	})

	it('fails when the claims break the schema, or the schema is missing', () => {
		const root = repository({ feature: scenario('REQ-MED-001', 'First'), files: STEPS })
		writeFileSync(join(root, CLAIMS_SCHEMA), JSON.stringify({ type: 'object', properties: { claims: { type: 'array', items: { type: 'string' } } } }))

		assert.match(build(root).problems.join('\n'), /breaks \.spec\/claims\.schema\.json: \$\.claims\[0\]: expected string, got object/)

		const bare = repository({ feature: scenario('REQ-MED-001', 'First'), files: STEPS })
		writeFileSync(join(bare, CLAIMS_SCHEMA), '{}')
		assert.deepEqual(build(bare).problems, [])
	})

	it('records each claim with its steps, bindings, constraints, and the records that cite it', () => {
		const root = repository({
			feature: `Rule: Uploads\n\n${scenario('REQ-MED-001', 'A thing happens', ['@ui'])}${scenario('REQ-MED-002', 'Not built yet', ['@ignore'])}`,
			pages: { '.spec/system-overview.md': '**CON-SO-001** A thing.\n*Verified by: REQ-MED-001.*\n' },
			files: {
				'tests/e2e/steps/thing.steps.ts': 'const { Given, Then } = createBdd()\nGiven("a thing", async () => {})\nThen("it holds", async () => {})\nThen("it holds", async () => {})\n',
				'.spec/decisions/ADR-0002-two.md': '---\ntitle: Two\nstatus: accepted\n---\n\n# ADR-0002 — Two\n\n**Status:** Accepted. Supersedes [ADR-0001](ADR-0001-one.md).\n\nREQ-MED-001 and CON-SO-001.\n',
				'.spec/decisions/ADR-0001-one.md': '---\ntitle: One\nstatus: superseded\n---\n\n# ADR-0001 — One\n',
				'.spec/lessons/0001-a-lesson.md': '---\ntitle: A lesson\nstatus: accepted\n---\n\n## Scenario\n\nREQ-MED-001, after [ADR-0002](../decisions/ADR-0002-two.md).\n\n## Skill\n\n`deliver-change`\n',
			},
		})

		const { data, problems, gaps } = build(root)

		assert.deepEqual(problems, [])
		assert.deepEqual(gaps, [])
		assert.deepEqual(data.claims[0], {
			id: 'REQ-MED-001',
			area: 'media',
			file: '.spec/features/media/media.feature',
			title: 'A thing happens',
			rule: 'Uploads',
			tags: ['@ui'],
			engine: 'playwright-bdd',
			status: 'Covered',
			steps: [
				{ keyword: 'Given', text: 'a thing', files: ['tests/e2e/steps/thing.steps.ts'], ambiguous: false },
				{ keyword: 'Then', text: 'it holds', files: ['tests/e2e/steps/thing.steps.ts'], ambiguous: true },
			],
			stepFiles: ['tests/e2e/steps/thing.steps.ts'],
			staleIgnore: false,
			constraints: ['CON-SO-001'],
			citedBy: { decisions: ['ADR-0002'], lessons: ['0001'] },
		})
		assert.equal(data.claims[1].staleIgnore, false, 'a Reqnroll claim nothing binds is not stale')
		assert.deepEqual(data.ambiguousSteps, [{ step: 'Then it holds', files: ['tests/e2e/steps/thing.steps.ts'] }])
		assert.deepEqual(data.decisions.map(({ id, supersedes, amends }) => ({ id, supersedes, amends })), [
			{ id: 'ADR-0001', supersedes: [], amends: [] },
			{ id: 'ADR-0002', supersedes: ['ADR-0001'], amends: [] },
		])
		assert.deepEqual(data.lessons[0], { id: '0001', file: '.spec/lessons/0001-a-lesson.md', title: 'A lesson', status: 'accepted', claims: ['REQ-MED-001'], constraints: [], decisions: ['ADR-0002'], skills: ['deliver-change'] })
	})

	it('records a fully bound @ignore claim as stale and a step definition nothing uses', () => {
		const root = repository({
			feature: scenario('REQ-MED-001', 'Built already', ['@ignore']),
			files: { ...STEPS, 'tests/HpacSafety.Acceptance.Tests/SpareSteps.cs': '[Binding]\npublic class SpareSteps\n{\n\t[When(@"nobody asks")]\n\tpublic void A() { }\n}\n' },
		})

		const { data, problems } = build(root)

		assert.deepEqual(problems, [])
		assert.equal(data.claims[0].status, 'Planned')
		assert.equal(data.claims[0].staleIgnore, true)
		assert.deepEqual(data.unusedStepDefinitions, [{ engine: 'Reqnroll', keyword: 'When', pattern: 'nobody asks', file: 'tests/HpacSafety.Acceptance.Tests/SpareSteps.cs' }])
	})
})

const claim = (id: string, area: string, status: ClaimRecord['status'] = 'Covered', stepFiles: string[] = []): ClaimRecord => ({
	id,
	area,
	file: `.spec/features/${area}/${area}.feature`,
	title: `Scenario ${id}`,
	rule: null,
	tags: [],
	engine: 'Reqnroll',
	status,
	steps: [{ keyword: 'Given', text: 'a thing', files: stepFiles, ambiguous: false }],
	stepFiles,
	staleIgnore: false,
	constraints: [],
	citedBy: { decisions: [], lessons: [] },
})

const data = (claims: ClaimRecord[], constraints: ConstraintRecord[] = []): ClaimsData => ({ claims, constraints, decisions: [], lessons: [], ambiguousSteps: [], unusedStepDefinitions: [] })

describe('render', () => {
	it('opens with frontmatter and says it is generated', () => {
		const matrix = render(data([]))

		assert.ok(matrix.startsWith('---\ntitle: Traceability\n'))
		assert.match(matrix, /do not edit by hand/)
	})

	it('gives each claim one table row, its step files linked from the specification root', () => {
		const matrix = render(data([{ ...claim('REQ-MED-001', 'media', 'Planned', ['tests/e2e/steps/a.steps.ts', 'tests/HpacSafety.Acceptance.Tests/B.cs']), title: 'A | B', engine: 'playwright-bdd' }]))

		assert.match(matrix, /^\| REQ-MED-001 \| A \\\| B \| media \| playwright-bdd \| Planned \| \[a\.steps\.ts\]\(\.\.\/tests\/e2e\/steps\/a\.steps\.ts\), \[B\.cs\]\(\.\.\/tests\/HpacSafety\.Acceptance\.Tests\/B\.cs\) \|$/m)
	})

	it('shows a dash for a claim nothing binds', () => {
		assert.match(render(data([claim('REQ-MED-001', 'media', 'Planned')])), /\| Planned \| — \|$/m)
	})

	it('carries no count taken across the whole tree', () => {
		const matrix = render(data([claim('REQ-MED-001', 'media'), claim('REQ-MED-002', 'media', 'Planned')]))

		assert.doesNotMatch(matrix, /\d+ claims|\d+ constraints/)
	})

	it('orders claims by ID, whatever order the feature files declare them in', () => {
		const matrix = render(data([claim('REQ-SUB-002', 'report-submission'), claim('REQ-MED-010', 'media'), claim('REQ-SUB-001', 'report-submission'), claim('REQ-MED-002', 'media')]))

		assert.deepEqual(matrix.match(/(?<=^\| )REQ-[A-Z]+-\d{3}/gm), ['REQ-MED-002', 'REQ-MED-010', 'REQ-SUB-001', 'REQ-SUB-002'])
	})

	it('orders constraints by ID and names what verifies each', () => {
		const matrix = render(
			data(
				[],
				[
					{ id: 'CON-SO-002', page: '.spec/system-overview.md', verifiedBy: [], note: 'none — a reason' },
					{ id: 'CON-SO-001', page: '.spec/system-overview.md', verifiedBy: ['REQ-MED-001', 'REQ-MED-002'], note: 'REQ-MED-001' },
				],
			),
		)

		assert.match(matrix, /\| CON-SO-001 \| \[system-overview\.md\]\(system-overview\.md\) \| REQ-MED-001, REQ-MED-002 \|\n\| CON-SO-002 \| \[system-overview\.md\]\(system-overview\.md\) \| none — a reason \|\n/)
	})
})

describe('serialize', () => {
	it('puts each claim and each step on lines of their own, and parses back to the same data', () => {
		const value = data([claim('REQ-MED-001', 'media', 'Covered', ['A.cs'])], [{ id: 'CON-SO-001', page: '.spec/system-overview.md', verifiedBy: ['REQ-MED-001'], note: 'REQ-MED-001' }])
		const json = serialize(value)

		assert.deepEqual(JSON.parse(json), value)
		assert.match(json, /^\t\t\t"id": "REQ-MED-001",$/m)
		assert.match(json, /^\t\t\t\t\{"keyword":"Given","text":"a thing","files":\["A\.cs"\],"ambiguous":false\}$/m)
		assert.match(json, /^\t\t\t"citedBy": \{"decisions":\[\],"lessons":\[\]\}$/m)
		assert.ok(json.endsWith('}\n'))
	})
})

describe('the generated files merge the way the tree does', () => {
	const base = [claim('REQ-DOM-001', 'domain-and-lifecycle', 'Planned'), claim('REQ-DOM-002', 'domain-and-lifecycle', 'Planned'), claim('REQ-DOM-003', 'domain-and-lifecycle', 'Planned'), claim('REQ-MED-001', 'media')]
	const changed = (claims: ClaimRecord[], id: string, change: Partial<ClaimRecord>): ClaimRecord[] => claims.map((each) => (each.id === id ? { ...each, ...change } : each))
	const both = (render: (claims: ClaimRecord[]) => string, ours: ClaimRecord[], theirs: ClaimRecord[], combined: ClaimRecord[]): void => {
		const { conflicts, merged } = mergeFile(render(base), render(ours), render(theirs))
		assert.equal(conflicts, 0)
		assert.equal(merged, render(combined))
	}
	const json = (claims: ClaimRecord[]): string => serialize(data(claims))
	const matrix = (claims: ClaimRecord[]): string => render(data(claims))

	it('merges status changes to two neighbouring claims cleanly in claims.json', () => {
		const ours = changed(base, 'REQ-DOM-001', { status: 'Covered' })
		const theirs = changed(base, 'REQ-DOM-002', { status: 'Covered' })
		both(json, ours, theirs, changed(ours, 'REQ-DOM-002', { status: 'Covered' }))
	})

	it('merges status changes to two claims a row apart cleanly in the matrix', () => {
		const ours = changed(base, 'REQ-DOM-001', { status: 'Covered' })
		const theirs = changed(base, 'REQ-DOM-003', { status: 'Covered' })
		both(matrix, ours, theirs, changed(ours, 'REQ-DOM-003', { status: 'Covered' }))
	})

	it('merges new claims in two different areas cleanly in both files', () => {
		const ours = [...base.slice(0, 3), claim('REQ-DOM-004', 'domain-and-lifecycle'), base[3]]
		const theirs = [...base, claim('REQ-MED-002', 'media')]
		const combined = [...ours, claim('REQ-MED-002', 'media')]
		both(json, ours, theirs, combined)
		both(matrix, ours, theirs, combined)
	})

	it('merges claims.json from two branches that change the same feature file, one inserting a scenario above the other', () => {
		const feature = (scenarios: string[]): Record<string, string> => ({ ...STEPS, 'tests/HpacSafety.Acceptance.Tests/MoreSteps.cs': '[Binding]\npublic class MoreSteps\n{\n\t[Then(@"it also holds")]\n\tpublic void A() { }\n}\n', '.spec/features/media/media.feature': `Feature: media\n\n${scenarios.join('')}` })
		const first = scenario('REQ-MED-001', 'First')
		const second = scenario('REQ-MED-002', 'Second')
		const generate = (files: Record<string, string>): string => build(repository({ files })).json

		const base = generate(feature([first, second]))
		const ours = generate(feature([first, scenario('REQ-MED-003', 'Inserted'), second]))
		const theirs = generate(feature([first, second.replace('  Then it holds\n', '  Then it holds\n  Then it also holds\n')]))
		const combined = generate(feature([first, scenario('REQ-MED-003', 'Inserted'), second.replace('  Then it holds\n', '  Then it holds\n  Then it also holds\n')]))

		const { conflicts, merged } = mergeFile(base, ours, theirs)
		assert.equal(conflicts, 0)
		assert.equal(merged, combined)
	})

	it('still conflicts when two branches claim the same new ID', () => {
		const ours = [...base, { ...claim('REQ-MED-002', 'media'), title: 'One thing' }]
		const theirs = [...base, { ...claim('REQ-MED-002', 'media'), title: 'Another thing' }]

		for (const write of [json, matrix]) {
			const { conflicts } = mergeFile(write(base), write(ours), write(theirs))
			assert.ok(typeof conflicts === 'number' && conflicts > 0)
		}
	})
})

describe('difference', () => {
	it('names the first differing line and the claims whose lines differ', () => {
		const before = serialize(data([claim('REQ-MED-001', 'media'), claim('REQ-MED-002', 'media')]))
		const after = serialize(data([claim('REQ-MED-001', 'media'), claim('REQ-MED-002', 'media', 'Planned')]))

		const lines = difference(before, after)

		assert.match(lines[0], /first difference at line \d+/)
		assert.match(lines[1], /"status": "Covered"/)
		assert.match(lines[2], /"status": "Planned"/)
		assert.equal(lines[3], '  claims that differ: REQ-MED-002')
		assert.deepEqual(difference('a\n', 'a\nb\n').slice(1, 3), ['  - ', '  + b'])
	})
})

describe('main', () => {
	const passing = (): string =>
		repository({
			feature: scenario('REQ-MED-001', 'A thing happens'),
			pages: { '.spec/system-overview.md': '**CON-SO-001** A thing.\n*Verified by: REQ-MED-001.*\n' },
			files: STEPS,
		})

	it('writes both files, passes --check, writes the fragment, and adds totals to the job summary', () => {
		const root = passing()
		const summary = join(root, 'summary.md')
		process.env.GITHUB_STEP_SUMMARY = summary
		let written: ReturnType<typeof runMain>
		try {
			written = runMain(root, { fragmentPath: join(root, 'fragment.json') })
		} finally {
			delete process.env.GITHUB_STEP_SUMMARY
		}

		assert.equal(written.code, 0)
		assert.match(written.output.log.join('\n'), /1 claims across 1 areas: 1 built, 0 still `@ignore`, 0 with an unbound step/)
		assert.match(readFileSync(join(root, TRACEABILITY), 'utf8'), /^\| REQ-MED-001 \| A thing happens \|/m)
		assert.equal((JSON.parse(readFileSync(join(root, CLAIMS), 'utf8')) as ClaimsData).claims[0].id, 'REQ-MED-001')
		assert.match(readFileSync(join(root, 'fragment.json'), 'utf8'), /"label": "REQ-MED-001"/)
		assert.match(readFileSync(summary, 'utf8'), /\*\*Traceability:\*\* 1 claims/)
		assert.equal(runMain(root, { check: true }).code, 0)
	})

	it('fails --check on a missing or stale file, naming it', () => {
		const root = passing()
		runMain(root)
		writeFileSync(join(root, TRACEABILITY), 'stale\n')

		const { code, output } = runMain(root, { check: true })

		assert.equal(code, 1)
		assert.match(output.error.join('\n'), /::error file=\.spec\/traceability\.md::Out of date/)
		assert.equal(runMain(repository({ feature: scenario('REQ-MED-001', 'A'), files: STEPS }), { check: true }).code, 1)
	})

	it('fails a built claim with an unbound step, naming the claim, the step, and its line', () => {
		const root = repository({ feature: scenario('REQ-MED-001', 'A thing happens').replace('it holds', 'it breaks'), files: STEPS })

		const { code, output } = runMain(root)

		assert.equal(code, 1)
		assert.match(output.error.join('\n'), /::error file=\.spec\/features\/media\/media\.feature,line=6::REQ-MED-001 step "Then it breaks" matches no Reqnroll step definition/)
		assert.ok(existsSync(join(root, CLAIMS)), 'the files are still written')
	})

	it('never fails with fail: false', () => {
		assert.equal(runMain(repository({ feature: 'Scenario: Untagged\n  Given a thing\n', files: STEPS }), { fail: false }).code, 0)
	})

	it('fails and annotates an untagged scenario', () => {
		const { code, output } = runMain(repository({ feature: 'Scenario: Untagged\n  Given a thing\n', files: STEPS }))

		assert.equal(code, 1)
		assert.match(output.error.join('\n'), /::error::.*carries no claim ID/)
	})
})

describe('the committed files', () => {
	const committed = JSON.parse(readFileSync(join(REPO, CLAIMS), 'utf8')) as ClaimsData

	it('conform to the schema', () => {
		assert.deepEqual(validate(committed, JSON.parse(readFileSync(join(REPO, CLAIMS_SCHEMA), 'utf8')) as Schema), [])
	})

	it('carry no top-level $schema key, so graphify reads claims.json as data and skips it', () => {
		assert.equal('$schema' in committed, false)
	})
})
