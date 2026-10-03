import { describe, it } from 'node:test'
import assert from 'node:assert/strict'
import { mkdirSync, mkdtempSync, readFileSync, writeFileSync } from 'node:fs'
import { tmpdir } from 'node:os'
import { join } from 'node:path'

import type { ClaimRecord, ClaimsData } from '../../../tools/spec/generate-traceability.ts'
import { type GraphJson, areaNode, claimNode, constraintNode, decisionNode, fileNodeId, fragment, lessonNode, main, merge, scenarioLines } from '../../../tools/spec/graph-fragment.ts'
import { CLAIMS } from '../../../tools/spec/spec-paths.ts'

const claim: ClaimRecord = {
	id: 'REQ-WLD-049',
	area: 'web-localization-and-design',
	file: '.spec/features/web-localization-and-design/web-localization-and-design.feature',
	title: 'A page left open across a deploy keeps its interface text',
	rule: null,
	tags: ['@ui'],
	engine: 'playwright-bdd',
	status: 'Built',
	steps: [{ keyword: 'Given', text: 'a visitor has the page open', files: ['tests/e2e/steps/locale.steps.ts'], ambiguous: false }],
	stepFiles: ['tests/e2e/steps/locale.steps.ts'],
	staleIgnore: false,
	constraints: ['CON-INF-027'],
	citedBy: { decisions: ['ADR-0190'], lessons: ['0043'] },
}

const DATA: ClaimsData = {
	areas: [{ name: 'web-localization-and-design', prefix: 'REQ-WLD', prefixes: ['REQ-WLD'] }],
	claims: [claim],
	constraints: [{ id: 'CON-INF-027', page: '.spec/infrastructure-and-operations.md', verifiedBy: ['REQ-WLD-049'], note: 'REQ-WLD-049' }],
	decisions: [
		{ id: 'ADR-0021', file: '.spec/decisions/ADR-0021-x.md', title: 'X', status: 'partially-superseded', claims: [], constraints: [], decisions: [], supersedes: [], amends: [] },
		{ id: 'ADR-0190', file: '.spec/decisions/ADR-0190-y.md', title: 'Y', status: 'accepted', claims: ['REQ-WLD-049'], constraints: ['CON-INF-027'], decisions: ['ADR-0021'], supersedes: ['ADR-0021'], amends: [] },
	],
	lessons: [{ id: '0043', file: '.spec/lessons/0043-z.md', title: 'Z', status: 'accepted', claims: ['REQ-WLD-049'], constraints: [], decisions: ['ADR-0190'], skills: ['deliver-change'] }],
	ambiguousSteps: [],
	unusedStepDefinitions: [],
}

const edges = (data: ClaimsData = DATA): string[] => fragment(data).edges.map((edge) => `${edge.source} ${edge.relation} ${edge.target}`)

describe('fileNodeId', () => {
	it('gives a file the ID graphify gives it', () => {
		assert.equal(fileNodeId('tests/HpacSafety.Acceptance.Tests/AuditSteps.cs'), 'tests_hpacsafety_acceptance_tests_auditsteps')
		assert.equal(fileNodeId('tests/e2e/steps/public-reports.steps.ts'), 'tests_e2e_steps_public_reports_steps')
		assert.equal(fileNodeId('.spec/decisions/ADR-0184-a-map.md'), 'spec_decisions_adr_0184_a_map')
	})
})

describe('fragment', () => {
	const { nodes } = fragment(DATA, new Map([['REQ-WLD-049', 125]]))
	const byId = new Map(nodes.map((node) => [node.id, node]))

	it('makes one semantic-tier node per claim, constraint, decision, lesson, and area, labelled by its ID', () => {
		assert.deepEqual(
			nodes.map((node) => [node.label, node.spec_kind]),
			[
				['web-localization-and-design', 'area'],
				['REQ-WLD-049', 'claim'],
				['CON-INF-027', 'constraint'],
				['ADR-0021', 'decision'],
				['ADR-0190', 'decision'],
				['Lesson 0043', 'lesson'],
			],
		)
		for (const node of nodes) {
			assert.equal(node._origin, 'semantic')
			assert.equal(node.spec_fragment, true)
			assert.equal(node.norm_label, node.label.toLowerCase())
		}
	})

	it('never gives a node a Markdown source, so graphify keeps re-scanning every page for its headings', () => {
		// graphify treats a .md file carrying any non-AST node as already
		// extracted by its LLM pass and stops re-scanning it (ADR-0193).
		for (const node of nodes) assert.doesNotMatch(node.source_file, /\.md$/i, `${node.label} has source_file ${node.source_file}`)
		assert.equal(byId.get(decisionNode('ADR-0190'))?.path, '.spec/decisions/ADR-0190-y.md')
	})

	it('puts the scenario, its location, and its step text on the claim node', () => {
		const node = byId.get(claimNode('REQ-WLD-049'))

		assert.ok(node)
		assert.equal(node.source_file, claim.file)
		assert.equal(node.source_location, 'L125')
		assert.deepEqual(node.steps, ['Given a visitor has the page open'])
		assert.match(String(node.rationale), /keeps its interface text\nGiven a visitor has the page open$/)
	})

	it('puts the area\'s claim prefixes on its node, its own first', () => {
		assert.equal(byId.get(areaNode('web-localization-and-design'))?.claim_prefixes, 'REQ-WLD')
	})

	it('links each item by a typed edge', () => {
		assert.deepEqual(edges().sort(), [
			`${claimNode('REQ-WLD-049')} bound_by tests_e2e_steps_locale_steps`,
			`${claimNode('REQ-WLD-049')} specified_in spec_area_web_localization_and_design`,
			`${constraintNode('CON-INF-027')} defined_in spec_infrastructure_and_operations`,
			`${constraintNode('CON-INF-027')} verified_by ${claimNode('REQ-WLD-049')}`,
			`${decisionNode('ADR-0021')} documented_in spec_decisions_adr_0021_x`,
			`${decisionNode('ADR-0190')} cites ${claimNode('REQ-WLD-049')}`,
			`${decisionNode('ADR-0190')} cites ${constraintNode('CON-INF-027')}`,
			`${decisionNode('ADR-0190')} documented_in spec_decisions_adr_0190_y`,
			`${decisionNode('ADR-0190')} supersedes ${decisionNode('ADR-0021')}`,
			`${lessonNode('0043')} cites ${claimNode('REQ-WLD-049')}`,
			`${lessonNode('0043')} cites ${decisionNode('ADR-0190')}`,
			`${lessonNode('0043')} documented_in spec_lessons_0043_z`,
			`${lessonNode('0043')} updates skills_deliver_change_skill`,
			'spec_area_web_localization_and_design documented_in spec_features_web_localization_and_design_readme',
		].sort())
	})

	it('is the same for the same data', () => {
		assert.deepEqual(fragment(DATA), fragment(structuredClone(DATA)))
	})
})

describe('scenarioLines', () => {
	it('finds the line each claim\'s scenario starts on, from the feature file', () => {
		const root = mkdtempSync(join(tmpdir(), 'scenario-lines-'))
		mkdirSync(join(root, 'f'))
		writeFileSync(join(root, 'f/a.feature'), 'Feature: A\n\n@REQ-A-001\n@ui\nScenario: One\n  Given x\n\n@REQ-A-002\nScenario Outline: Two\n')

		assert.deepEqual([...scenarioLines(root, ['f/a.feature', 'f/a.feature', 'f/missing.feature'])], [['REQ-A-001', 5], ['REQ-A-002', 9]])
	})
})

describe('merge', () => {
	const graph = (): GraphJson => ({
		directed: false,
		nodes: [
			{ id: 'tests_e2e_steps_locale_steps', label: 'locale.steps.ts' },
			{ id: 'spec_claim_req_old_001', label: 'REQ-OLD-001', spec_fragment: true },
		],
		links: [{ source: 'spec_claim_req_old_001', target: 'tests_e2e_steps_locale_steps', relation: 'bound_by', spec_fragment: true }],
		built_at_commit: 'abc',
	})

	it('replaces the previous fragment, keeps the rest of the graph, and drops an edge to a file it lacks', () => {
		const { graph: merged, added, dropped } = merge(graph(), fragment(DATA))
		const ids = merged.nodes.map((node) => node.id)

		assert.equal(ids.includes('spec_claim_req_old_001'), false)
		assert.equal(ids.includes('tests_e2e_steps_locale_steps'), true)
		assert.equal(added.nodes, 6)
		assert.ok((merged.links ?? []).some((link) => link.source === claimNode('REQ-WLD-049') && link.target === 'tests_e2e_steps_locale_steps'))
		assert.equal(dropped, 6, 'the constraint page, three records, the skill, and the area README are not in this graph')
		assert.equal(merged.built_at_commit, 'abc')
	})

	it('gives the same graph when merged twice', () => {
		const once = merge(graph(), fragment(DATA)).graph
		assert.deepEqual(merge(once, fragment(DATA)).graph, once)
	})

	it('reads an older graph that keeps its edges under "edges"', () => {
		const old: GraphJson = { nodes: graph().nodes, edges: [] }
		const { graph: merged } = merge(old, fragment(DATA))

		assert.equal('links' in merged, false)
		assert.ok((merged.edges ?? []).length > 0)
	})
})

describe('main', () => {
	it('merges the committed claims into the graph file', () => {
		const root = mkdtempSync(join(tmpdir(), 'graph-fragment-'))
		mkdirSync(join(root, '.spec'))
		mkdirSync(join(root, 'out'))
		writeFileSync(join(root, CLAIMS), JSON.stringify(DATA))
		writeFileSync(join(root, 'out/graph.json'), JSON.stringify({ nodes: [], links: [] }))
		const log: string[] = []

		assert.equal(main({ root, graphPath: 'out/graph.json', log: (line) => log.push(line) }), 0)
		assert.match(log[0], /6 nodes/)
		assert.equal((JSON.parse(readFileSync(join(root, 'out/graph.json'), 'utf8')) as GraphJson).nodes.length, 6)
	})
})
