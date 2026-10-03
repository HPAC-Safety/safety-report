#!/usr/bin/env node
// The specification's machine data and its human matrix, generated from the
// artifacts (ADR-0084, ADR-0184, ADR-0193).
//
// Every scenario carries one stable claim ID as a Gherkin tag, and every
// normative constraint in a canonical specification page carries a CON id
// naming the claims that verify it. This reads both, resolves every step to
// the step definition its runner would bind (tools/spec/step-bindings.ts), reads
// which decisions and lessons cite each claim (tools/spec/read-records.ts), and
// writes two files:
//
//   .spec/claims.json      the canonical data, for tools and CI, conforming to
//                          .spec/claims.schema.json;
//   .spec/traceability.md  one table row per claim and per constraint, for people.
//
// The same model feeds the graphify fragment (tools/spec/graph-fragment.ts),
// which `--fragment <path>` writes too.
//
//   node tools/spec/generate-traceability.ts             write both; exit 1 on a problem or gap
//   node tools/spec/generate-traceability.ts --check     fail when a committed file differs
//   node tools/spec/generate-traceability.ts --no-fail   write both, never fail (hooks and the bot)
//
// The exit code is the contract: a duplicate, malformed, missing, or dangling
// id fails, and so does a built claim with a step no definition binds.
import { appendFileSync, existsSync, readFileSync, writeFileSync } from 'node:fs'
import { join, posix } from 'node:path'

import { areaPrefixes, prefixOf, prefixProblems } from './claim-prefixes.ts'
import { fragment } from './graph-fragment.ts'
import { type Schema, validate } from './json-schema.ts'
import { type Constraint, readConstraints } from './read-claims.ts'
import { type DecisionRecord, type LessonRecord, readDecisions, readLessons } from './read-records.ts'
import { CLAIMS, CLAIMS_SCHEMA, CONSTRAINT_PAGES, SPEC_ROOT, TRACEABILITY } from './spec-paths.ts'
import { type Resolution, collectBindings, display } from './step-bindings.ts'
import { isMain } from '../lib/actions.ts'

// Kept for the tools that read claims through this module.
export { CLAIM_TAG, type Claim, type Constraint, readClaims, readConstraints } from './read-claims.ts'
export { CONSTRAINT_PAGES }

const ROOT = process.cwd()

/** One step as its runner executes it, with the files whose definitions bind it. */
export interface ClaimStep {
	keyword: string
	text: string
	files: string[]
	ambiguous: boolean
}

/** One claim, as .spec/claims.json records it. */
export interface ClaimRecord {
	id: string
	area: string
	file: string
	title: string
	rule: string | null
	tags: string[]
	engine: 'playwright-bdd' | 'Reqnroll'
	status: 'Planned' | 'Covered'
	steps: ClaimStep[]
	stepFiles: string[]
	/** `@ignore`, yet every step is already bound. */
	staleIgnore: boolean
	constraints: string[]
	citedBy: { decisions: string[]; lessons: string[] }
}

/** One constraint, as .spec/claims.json records it. */
export interface ConstraintRecord {
	id: string
	page: string
	verifiedBy: string[]
	note: string
}

/** One feature area, with the prefix a new claim in it takes and every prefix its claims carry (ADR-0194). */
export interface AreaRecord {
	name: string
	prefix: string
	prefixes: string[]
}

/** The whole of .spec/claims.json. */
export interface ClaimsData {
	areas: AreaRecord[]
	claims: ClaimRecord[]
	constraints: ConstraintRecord[]
	decisions: DecisionRecord[]
	lessons: LessonRecord[]
	ambiguousSteps: { step: string; files: string[] }[]
	unusedStepDefinitions: { engine: string; keyword: string; pattern: string; file: string }[]
}

const byId = (a: { id: string }, b: { id: string }): number => Number(a.id > b.id) - Number(a.id < b.id)

/** The model behind both files, from what the readers found. */
export function model(
	resolution: Pick<Resolution, 'claims' | 'unused' | 'ambiguous'>,
	constraints: readonly Constraint[],
	decisions: readonly DecisionRecord[],
	lessons: readonly LessonRecord[],
	prefixes: ReadonlyMap<string, string | undefined> = new Map(),
): ClaimsData {
	const citing = (records: readonly { id: string; claims: string[] }[], claim: string): string[] => records.filter((record) => record.claims.includes(claim)).map((record) => record.id)
	const carried = (area: string, own: string): string[] => [own, ...[...new Set(resolution.claims.filter((claim) => claim.area === area).map((claim) => prefixOf(claim.id)))].filter((prefix) => prefix !== own).sort()]
	return {
		areas: [...prefixes]
			.filter((entry): entry is [string, string] => entry[1] !== undefined)
			.map(([name, prefix]) => ({ name, prefix, prefixes: carried(name, prefix) })),
		claims: resolution.claims
			.map(
				(claim): ClaimRecord => ({
					id: claim.id,
					area: claim.area,
					file: claim.path,
					title: claim.scenario,
					rule: claim.rule,
					tags: claim.tags,
					engine: claim.engine,
					status: claim.status,
					steps: claim.bound.map(({ keyword, text, files, ambiguous }) => ({ keyword, text, files, ambiguous })),
					stepFiles: claim.files,
					staleIgnore: claim.status === 'Planned' && claim.unbound.length === 0 && claim.steps.length > 0,
					constraints: constraints.filter((constraint) => constraint.verifiedBy.includes(claim.id)).map((constraint) => constraint.id).sort(),
					citedBy: { decisions: citing(decisions, claim.id), lessons: citing(lessons, claim.id) },
				}),
			)
			.sort(byId),
		constraints: constraints.map(({ id, page, verifiedBy, note }) => ({ id, page, verifiedBy, note })).sort(byId),
		decisions: [...decisions],
		lessons: [...lessons],
		ambiguousSteps: [...resolution.ambiguous].map(([step, files]) => ({ step, files: [...files].sort() })).sort((a, b) => Number(a.step > b.step) - Number(a.step < b.step)),
		unusedStepDefinitions: resolution.unused.map((binding) => ({ engine: binding.engine, keyword: binding.keyword, pattern: display(binding), file: binding.file })),
	}
}

/**
 * The JSON, laid out so git merges it (ADR-0106): every claim, constraint,
 * record, and step on lines of its own, everything inside them on one line,
 * keys in a fixed order, and no totals. A change to one claim touches only its
 * own lines.
 */
export function serialize(data: ClaimsData): string {
	const indent = (depth: number): string => '\t'.repeat(depth)
	const write = (value: unknown, depth: number): string => {
		if (Array.isArray(value) && value.length > 0 && value.every((item) => typeof item === 'object' && item !== null && !Array.isArray(item)) && depth <= 3) {
			return `[\n${value.map((item) => indent(depth + 1) + write(item, depth + 1)).join(',\n')}\n${indent(depth)}]`
		}
		if (typeof value === 'object' && value !== null && !Array.isArray(value) && depth <= 2) {
			const entries = Object.entries(value).map(([key, child]) => `${indent(depth + 1)}${JSON.stringify(key)}: ${write(child, depth + 1)}`)
			return `{\n${entries.join(',\n')}\n${indent(depth)}}`
		}
		return JSON.stringify(value)
	}
	return `${write(data, 0)}\n`
}

/** A value made safe for one table cell. */
const cell = (text: string): string => text.replace(/\s+/g, ' ').replace(/\|/g, '\\|').trim()

/** A link from the matrix, which lives in the specification root. */
const link = (file: string): string => `[${posix.basename(file)}](${posix.relative(SPEC_ROOT, file)})`

/**
 * The matrix: one table row per claim and per constraint, sorted by ID, and no
 * totals (ADR-0106). Every row derives from one item, so a change to one claim
 * changes one line.
 */
export function render(data: Pick<ClaimsData, 'claims' | 'constraints'>): string {
	const lines = [
		'---',
		'title: Traceability',
		'description: Generated matrix of every claim — its scenario, area, engine, status, and the step-definition files that bind it — and every constraint with the claims that verify it.',
		'type: guide',
		'---',
		'',
		'# Traceability',
		'',
		'> **Generated file — do not edit by hand.**',
		'> Regenerate with `node tools/spec/generate-traceability.ts`. CI fails on a difference',
		'> ([ADR-0193](decisions/ADR-0193-the-claims-are-generated-as-json-a-graph-fragment-and-one-slim-matrix.md)).',
		'> The same data with every step, binding, and citation is [`claims.json`](claims.json).',
		'',
		'A `Planned` claim is still `@ignore`. Reqnroll runs a claim without `@ui`;',
		'playwright-bdd runs one with it.',
		'',
		'## Claims',
		'',
		'| Claim | Scenario | Area | Engine | Status | Step definitions |',
		'|---|---|---|---|---|---|',
	]
	for (const claim of [...data.claims].sort(byId)) {
		const files = claim.stepFiles.length > 0 ? claim.stepFiles.map(link).join(', ') : '—'
		lines.push(`| ${claim.id} | ${cell(claim.title)} | ${claim.area} | ${claim.engine} | ${claim.status} | ${files} |`)
	}

	lines.push(
		'',
		'## Constraints',
		'',
		'A constraint states something that must be true of the system; the claims',
		'beside it are the scenarios that prove it. `none` carries its reason.',
		'',
		'| Constraint | Page | Verified by |',
		'|---|---|---|',
	)
	for (const constraint of [...data.constraints].sort(byId)) {
		const verifiedBy = constraint.verifiedBy.length > 0 ? constraint.verifiedBy.join(', ') : cell(constraint.note)
		lines.push(`| ${constraint.id} | ${link(constraint.page)} | ${verifiedBy} |`)
	}

	lines.push('')
	return lines.join('\n')
}

/** The whole-tree counts, reported rather than committed (ADR-0106). */
export function totals(data: ClaimsData): string {
	const planned = data.claims.filter((claim) => claim.status === 'Planned').length
	const unbound = data.claims.filter((claim) => claim.steps.some((step) => step.files.length === 0)).length
	return (
		`${data.claims.length} claims across ${new Set(data.claims.map((claim) => claim.area)).size} areas: ` +
		`${data.claims.length - planned} built, ${planned} still \`@ignore\`, ${unbound} with an unbound step, ` +
		`${data.claims.filter((claim) => claim.staleIgnore).length} stale @ignore. ${data.constraints.length} constraints. ` +
		`${data.ambiguousSteps.length} ambiguous steps, ${data.unusedStepDefinitions.length} unused step definitions.`
	)
}

/** Everything the generator found: the model, both files, and what is wrong. */
export interface Build {
	data: ClaimsData
	json: string
	matrix: string
	problems: string[]
	/** Where each claim's scenario starts, for the graph fragment only. */
	lines: Map<string, number>
	/** Every step of a built claim that no definition binds. */
	gaps: { id: string; file: string; line: number; engine: string; step: string }[]
}

export function build(root = ROOT): Build {
	const resolution = collectBindings(root)
	const problems = [...resolution.problems]

	const constraints: Constraint[] = []
	for (const path of CONSTRAINT_PAGES) {
		const result = readConstraints(path, readFileSync(join(root, path), 'utf8'))
		constraints.push(...result.constraints)
		problems.push(...result.problems)
	}

	const seen = new Map<string, string>()
	for (const claim of resolution.claims) {
		if (seen.has(claim.id)) problems.push(`${claim.id} is used twice: "${seen.get(claim.id)}" and "${claim.scenario}" — a claim ID is never reused`)
		seen.set(claim.id, claim.scenario)
	}

	const constraintIds = new Set()
	for (const constraint of constraints) {
		if (constraintIds.has(constraint.id)) problems.push(`${constraint.id} is declared twice`)
		constraintIds.add(constraint.id)
		for (const id of constraint.verifiedBy) {
			if (!seen.has(id)) problems.push(`${constraint.page}: ${constraint.id} names ${id}, which no scenario declares`)
		}
	}

	const prefixes = areaPrefixes(root)
	problems.push(...prefixProblems(resolution.claims, prefixes))

	const data = model(resolution, constraints, readDecisions(root), readLessons(root), prefixes)

	const schemaPath = join(root, CLAIMS_SCHEMA)
	if (existsSync(schemaPath)) {
		for (const problem of validate(data, JSON.parse(readFileSync(schemaPath, 'utf8')) as Schema)) problems.push(`${CLAIMS} breaks ${CLAIMS_SCHEMA}: ${problem}`)
	} else {
		problems.push(`${CLAIMS_SCHEMA} is missing`)
	}

	const gaps = resolution.claims
		.filter((claim) => claim.status === 'Covered')
		.flatMap((claim) => claim.unbound.map((step) => ({ id: claim.id, file: claim.path, line: step.line, engine: claim.engine, step: `${step.keyword} ${step.text}` })))
	return { data, json: serialize(data), matrix: render(data), problems, gaps, lines: new Map(resolution.claims.map((claim) => [claim.id, claim.line])) }
}

/**
 * Where a committed file and the generated one part: the first line that
 * differs, and the claims whose lines differ, so a stale file says what moved.
 */
export function difference(committed: string, generated: string, limit = 10): string[] {
	const ours = committed.split('\n')
	const theirs = generated.split('\n')
	const at = ours.findIndex((line, index) => index >= theirs.length || line !== theirs[index])
	const first = at === -1 ? ours.length : at
	const shown = (lines: string[]): string => (first < lines.length ? lines[first] : '(end of file)')

	// Each claim's lines, keyed by the ID that opens its object in the JSON or
	// its row in the matrix.
	const blocks = (lines: string[]): Map<string, string> => {
		const found = new Map<string, string>()
		let id = ''
		for (const line of lines) {
			const row = /^\| (REQ-[A-Z]+-\d{3}) \|/.exec(line)
			if (row) found.set(row[1], line)
			const opens = /^\t\t\t"id": "(REQ-[A-Z]+-\d{3})"/.exec(line)
			if (opens) id = opens[1]
			if (id) found.set(id, `${found.get(id) ?? ''}${line}\n`)
			if (/^\t\t\},?$/.test(line)) id = ''
		}
		return found
	}
	const [before, after] = [blocks(ours), blocks(theirs)]
	const changed = [...new Set([...before.keys(), ...after.keys()])].filter((id) => before.get(id) !== after.get(id)).sort()

	return [
		`  first difference at line ${first + 1}:`,
		`  - ${shown(ours)}`,
		`  + ${shown(theirs)}`,
		...(changed.length > 0 ? [`  claims that differ: ${changed.slice(0, limit).join(', ')}${changed.length > limit ? `, and ${changed.length - limit} more` : ''}`] : []),
	]
}

/**
 * Builds both files and writes or checks them, reporting the way the command
 * line does without exiting, so a test can exercise every outcome. Returns the
 * exit code.
 */
export function main(root = ROOT, { check = false, fail = true, fragmentPath }: { check?: boolean; fail?: boolean; fragmentPath?: string } = {}): number {
	const result = build(root)

	for (const problem of result.problems) console.error(`::error::${problem}`)
	for (const gap of result.gaps) console.error(`::error file=${gap.file},line=${gap.line}::${gap.id} step "${gap.step}" matches no ${gap.engine} step definition`)

	let stale = false
	for (const [file, text] of [
		[CLAIMS, result.json],
		[TRACEABILITY, result.matrix],
	] as const) {
		const target = join(root, file)
		if (!check) {
			writeFileSync(target, text)
			continue
		}
		const committed = existsSync(target) ? readFileSync(target, 'utf8') : ''
		if (committed !== text) {
			console.error(`::error file=${file}::Out of date. Run 'node tools/spec/generate-traceability.ts' and commit the result; on a same-repo pull request traceability.yml commits it for you (ADR-0101).`)
			for (const line of difference(committed, text)) console.error(line)
			stale = true
		}
	}
	if (fragmentPath) writeFileSync(fragmentPath, `${JSON.stringify(fragment(result.data, result.lines), null, '\t')}\n`)

	console.log(`${CLAIMS} and ${TRACEABILITY} ${check ? 'checked' : 'written'}. ${totals(result.data)}`)
	if (!check && process.env.GITHUB_STEP_SUMMARY) appendFileSync(process.env.GITHUB_STEP_SUMMARY, `**Traceability:** ${totals(result.data)}\n`)

	if (stale) return 1
	if (!fail) return 0
	return result.problems.length > 0 || result.gaps.length > 0 ? 1 : 0
}

if (isMain(import.meta.url)) {
	const at = process.argv.indexOf('--fragment')
	process.exit(
		main(ROOT, {
			check: process.argv.includes('--check'),
			fail: !process.argv.includes('--no-fail'),
			fragmentPath: at === -1 ? undefined : process.argv[at + 1],
		}),
	)
}
