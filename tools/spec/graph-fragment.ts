#!/usr/bin/env node
// The specification as a graphify extraction, merged into the local graph
// without an LLM pass (ADR-0193).
//
// graphify never reads a .feature file, skips data-shaped JSON, and gives a
// Markdown page only its headings without an LLM pass, so a new claim, ADR, or
// lesson was missing from the graph until someone re-ran the semantic
// extraction. This builds one node per claim, constraint, decision, lesson, and
// feature area from .spec/claims.json, with typed edges between them and to the
// files graphify already indexes (step definitions, pages, skills), and writes
// them into graphify-out/graph.json. graphify is not forked (ADR-0088): the
// fragment is ordinary graph data.
//
// Every node and edge it adds is a semantic-tier item (`_origin: "semantic"`)
// whose source_file is the file it describes, which `graphify update` keeps:
// it re-extracts only the AST tier, and evicts a semantic item only once its
// source file is gone. Each one also carries `spec_fragment: true`, so a merge
// first removes the previous fragment and the result never accumulates stale
// claims.
//
//   node tools/spec/graph-fragment.ts            merge into graphify-out/graph.json
//   node tools/spec/graph-fragment.ts --graph <path>
//
// A missing graph is not an error: graphify is optional, so the merge says so
// and exits 0.
import { existsSync, readFileSync, renameSync, writeFileSync } from 'node:fs'
import { dirname, join, posix, resolve } from 'node:path'

import type { ClaimsData } from './generate-traceability.ts'
import { CLAIM_TAG } from './read-claims.ts'
import { CLAIMS, DECISIONS, FEATURES, LESSONS, SPEC_ROOT } from './spec-paths.ts'
import { isMain } from '../lib/actions.ts'

/** A node in graphify's extraction format. */
export interface GraphNode {
	id: string
	label: string
	file_type: 'document' | 'concept'
	source_file: string
	source_location: string | null
	[key: string]: unknown
}

/** An edge in graphify's extraction format. */
export interface GraphEdge {
	source: string
	target: string
	relation: string
	confidence: 'EXTRACTED'
	source_file: string
	[key: string]: unknown
}

/** A graphify extraction: what one source contributes to the graph. */
export interface Extraction {
	nodes: GraphNode[]
	edges: GraphEdge[]
}

/**
 * The ID graphify gives a file's own node: its path without the last
 * extension, lower-cased, every run of other characters one underscore
 * (graphify/ids.py). The paths here are ASCII, so the Unicode folding it also
 * does changes nothing.
 */
export function fileNodeId(path: string): string {
	return path
		.replace(/\.[^./]+$/, '')
		.toLowerCase()
		.replace(/[^a-z0-9_]+/g, '_')
		.replace(/_+/g, '_')
		.replace(/^_|_$/g, '')
}

const id = (kind: string, name: string): string => `spec_${kind}_${name.toLowerCase().replace(/[^a-z0-9]+/g, '_')}`
export const claimNode = (claim: string): string => id('claim', claim)
export const constraintNode = (constraint: string): string => id('constraint', constraint)
export const decisionNode = (adr: string): string => id('decision', adr)
export const lessonNode = (lesson: string): string => id('lesson', lesson)
export const areaNode = (area: string): string => id('area', area)

/** When two relations join the same pair, the stronger one is kept: graphify's graph holds one edge per pair. */
const STRENGTH = ['supersedes', 'amends', 'verified_by', 'bound_by', 'specified_in', 'updates', 'documented_in', 'defined_in', 'cites']

/** The extraction for the whole specification, in a stable order. */
export function fragment(data: ClaimsData, lines: ReadonlyMap<string, number> = new Map()): Extraction {
	const nodes: GraphNode[] = []
	const edges = new Map<string, GraphEdge>()
	const node = (item: GraphNode): void => {
		nodes.push({ ...item, norm_label: item.label.toLowerCase(), _origin: 'semantic', spec_fragment: true })
	}
	const edge = (source: string, relation: string, target: string, sourceFile: string): void => {
		if (source === target) return
		const key = [source, target].sort().join('\u0000')
		const existing = edges.get(key)
		if (existing && STRENGTH.indexOf(existing.relation) <= STRENGTH.indexOf(relation)) return
		edges.set(key, { source, target, relation, confidence: 'EXTRACTED', confidence_score: 1, weight: 1, source_file: sourceFile, _origin: 'semantic', spec_fragment: true })
	}
	const cite = (from: string, sourceFile: string, { claims, constraints, decisions }: { claims: string[]; constraints: string[]; decisions: string[] }): void => {
		for (const claim of claims) edge(from, 'cites', claimNode(claim), sourceFile)
		for (const constraint of constraints) edge(from, 'cites', constraintNode(constraint), sourceFile)
		for (const decision of decisions) edge(from, 'cites', decisionNode(decision), sourceFile)
	}

	const areas = new Map<string, string>()
	for (const claim of data.claims) areas.set(claim.area, claim.file)
	// Each area's own prefix first, then any retired one its claims kept (ADR-0194).
	const prefixes = new Map(data.areas.map((area) => [area.name, area.prefixes.join(', ')]))
	for (const [area, file] of [...areas].sort(([a], [b]) => a.localeCompare(b))) {
		const claimPrefixes = prefixes.get(area)
		node({ id: areaNode(area), label: area, file_type: 'document', source_file: file, source_location: 'L1', spec_kind: 'area', ...(claimPrefixes ? { claim_prefixes: claimPrefixes } : {}) })
		edge(areaNode(area), 'documented_in', fileNodeId(`${FEATURES}/${area}/README.md`), file)
	}

	for (const claim of data.claims) {
		const steps = claim.steps.map((step) => `${step.keyword} ${step.text}`)
		node({
			id: claimNode(claim.id),
			label: claim.id,
			file_type: 'concept',
			source_file: claim.file,
			source_location: lines.has(claim.id) ? `L${lines.get(claim.id)}` : null,
			spec_kind: 'claim',
			title: claim.title,
			rule: claim.rule,
			engine: claim.engine,
			status: claim.status,
			steps,
			rationale: [`${claim.id} ${claim.title}`, ...steps].join('\n'),
		})
		edge(claimNode(claim.id), 'specified_in', areaNode(claim.area), claim.file)
		for (const file of claim.stepFiles) edge(claimNode(claim.id), 'bound_by', fileNodeId(file), claim.file)
	}

	for (const constraint of data.constraints) {
		node({ id: constraintNode(constraint.id), label: constraint.id, file_type: 'concept', source_file: SPEC_ROOT, source_location: constraint.page, path: constraint.page, spec_kind: 'constraint', rationale: `${constraint.id} ${constraint.note}` })
		edge(constraintNode(constraint.id), 'defined_in', fileNodeId(constraint.page), SPEC_ROOT)
		for (const claim of constraint.verifiedBy) edge(constraintNode(constraint.id), 'verified_by', claimNode(claim), SPEC_ROOT)
	}

	for (const decision of data.decisions) {
		const from = decisionNode(decision.id)
		node({ id: from, label: decision.id, file_type: 'document', source_file: DECISIONS, source_location: decision.file, path: decision.file, spec_kind: 'decision', title: decision.title, status: decision.status, rationale: `${decision.id} ${decision.title}` })
		edge(from, 'documented_in', fileNodeId(decision.file), DECISIONS)
		cite(from, DECISIONS, decision)
		for (const target of decision.amends) edge(from, 'amends', decisionNode(target), DECISIONS)
		for (const target of decision.supersedes) edge(from, 'supersedes', decisionNode(target), DECISIONS)
	}

	for (const lesson of data.lessons) {
		const from = lessonNode(lesson.id)
		node({ id: from, label: `Lesson ${lesson.id}`, file_type: 'document', source_file: LESSONS, source_location: lesson.file, path: lesson.file, spec_kind: 'lesson', title: lesson.title, status: lesson.status, rationale: `Lesson ${lesson.id} ${lesson.title}` })
		edge(from, 'documented_in', fileNodeId(lesson.file), LESSONS)
		cite(from, LESSONS, lesson)
		for (const skill of lesson.skills) edge(from, 'updates', fileNodeId(posix.join('skills', skill, 'SKILL.md')), LESSONS)
	}

	return { nodes, edges: [...edges.values()] }
}

/**
 * Where each claim's scenario starts, read from the feature files themselves:
 * .spec/claims.json leaves line numbers out, so inserting a scenario changes
 * only its own lines (ADR-0193).
 */
export function scenarioLines(root: string, files: Iterable<string>): Map<string, number> {
	const found = new Map<string, number>()
	for (const file of new Set(files)) {
		const path = join(root, file)
		if (!existsSync(path)) continue
		let pending: string[] = []
		for (const [index, line] of readFileSync(path, 'utf8').split('\n').entries()) {
			const tag = line.match(CLAIM_TAG)
			if (tag) pending.push(tag[1])
			else if (/^\s*Scenario( Outline)?:/.test(line)) {
				for (const id of pending) found.set(id, index + 1)
				pending = []
			}
		}
	}
	return found
}

/** graph.json as graphify writes it: networkx node-link data. */
export interface GraphJson {
	nodes: Record<string, unknown>[]
	links?: Record<string, unknown>[]
	edges?: Record<string, unknown>[]
	[key: string]: unknown
}

/** What a merge did. */
export interface MergeResult {
	graph: GraphJson
	added: { nodes: number; edges: number }
	/** Edges whose far end the graph does not hold yet, such as a file graphify has not indexed. */
	dropped: number
}

/**
 * The graph with the previous fragment removed and this one added. A fragment
 * node whose ID graphify already uses is left to graphify; an edge is added
 * only when both of its ends are in the graph.
 */
export function merge(graph: GraphJson, extraction: Extraction): MergeResult {
	const key = graph.links ? 'links' : graph.edges ? 'edges' : 'links'
	const nodes = graph.nodes.filter((item) => item.spec_fragment !== true)
	const ids = new Set(nodes.map((item) => item.id))
	const added = extraction.nodes.filter((item) => !ids.has(item.id))
	for (const item of added) ids.add(item.id)

	const links = (graph[key] ?? []).filter((item) => item.spec_fragment !== true && ids.has(item.source) && ids.has(item.target))
	const kept = extraction.edges.filter((item) => ids.has(item.source) && ids.has(item.target))

	return {
		graph: { ...graph, nodes: [...nodes, ...added], [key]: [...links, ...kept] },
		added: { nodes: added.length, edges: kept.length },
		dropped: extraction.edges.length - kept.length,
	}
}

/** Merges the fragment into the graph file, replacing it in one rename. Returns the exit code. */
export function main({ root = process.cwd(), graphPath = join(process.env.GRAPHIFY_OUT ?? 'graphify-out', 'graph.json'), log = console.log }: { root?: string; graphPath?: string; log?: (line: string) => void } = {}): number {
	const target = resolve(root, graphPath)
	if (!existsSync(target)) {
		log(`No graph at ${graphPath}; nothing to merge. Build one with graphify first (see init-dev.sh).`)
		return 0
	}
	const data = JSON.parse(readFileSync(join(root, CLAIMS), 'utf8')) as ClaimsData
	const lines = scenarioLines(root, data.claims.map((claim) => claim.file))
	const result = merge(JSON.parse(readFileSync(target, 'utf8')) as GraphJson, fragment(data, lines))
	const temporary = join(dirname(target), '.graph.spec-fragment.tmp.json')
	writeFileSync(temporary, JSON.stringify(result.graph, null, 2))
	renameSync(temporary, target)
	log(`Merged the specification into ${graphPath}: ${result.added.nodes} nodes, ${result.added.edges} edges (${result.dropped} edges to files the graph does not hold yet).`)
	return 0
}

if (isMain(import.meta.url)) {
	const at = process.argv.indexOf('--graph')
	process.exit(main(at === -1 ? {} : { graphPath: process.argv[at + 1] }))
}
