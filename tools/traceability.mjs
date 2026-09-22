#!/usr/bin/env node
// The traceability matrix, generated from the artifacts (ADR-0084).
//
// Every scenario carries one stable claim ID as a Gherkin tag, and every
// normative constraint in a canonical docs page carries a CON id naming the
// claims that verify it. This reads both and writes docs/traceability.md, so
// the matrix is derived rather than maintained — the 1998 version of this
// document was a spreadsheet nobody updated.
//
// Dependency-free, like every other tool in this directory. The grammar it
// consumes is two line shapes, and tools/gherkin/verify.mjs has already
// proved with the official parser that the files are valid Gherkin, so this
// never has to be the thing that discovers a syntax error.
//
// The exit code is the contract: a duplicate, malformed, missing, or dangling
// id fails rather than producing a matrix with a hole in it.
import { readFileSync, readdirSync, statSync, writeFileSync } from 'node:fs'
import { join } from 'node:path'

const ROOT = process.cwd()
const OUTPUT = 'docs/traceability.md'

export const CLAIM_TAG = /^\s*@(REQ-[A-Z]+-\d{3})\s*$/
const SCENARIO = /^\s*(Scenario|Scenario Outline):\s*(.+?)\s*$/
const CONSTRAINT = /\*\*(CON-[A-Z]+-\d{3})\*\*/g
const VERIFIED_BY = /\*Verified by:\s*([^*]+)\*/

// The docs pages that carry normative constraints. A page not listed here is
// narrative: it describes, it does not require.
export const CONSTRAINT_PAGES = [
	'docs/system-overview.md',
	'docs/data-and-persistence.md',
	'docs/interfaces-and-data-flow.md',
	'docs/infrastructure-and-operations.md',
	'docs/testing-and-quality.md',
]

/** Every claim a feature file declares, in file order. */
export function readClaims(path, source) {
	const lines = source.split('\n')
	const claims = []
	const problems = []
	let pending = []

	for (const [index, line] of lines.entries()) {
		const tag = line.match(CLAIM_TAG)
		if (tag) {
			pending.push(tag[1])
			continue
		}

		const scenario = line.match(SCENARIO)
		if (!scenario) {
			// Any other tag line keeps the pending claim attached to the
			// scenario below it; anything else ends the tag block.
			if (line.trim().startsWith('@') || line.trim() === '') continue
			pending = []
			continue
		}

		if (pending.length === 0) {
			problems.push(`${path}:${index + 1}: "${scenario[2]}" carries no claim ID — every scenario names one claim (ADR-0084)`)
			continue
		}
		if (pending.length > 1) {
			problems.push(`${path}:${index + 1}: "${scenario[2]}" carries ${pending.length} claim IDs (${pending.join(', ')}) — a scenario is exactly one claim`)
		}

		const tags = lines.slice(Math.max(0, index - 8), index).map((candidate) => candidate.trim())
		claims.push({
			id: pending[0],
			area: path.split('/')[1],
			scenario: scenario[2],
			engine: tags.includes('@ui') ? 'playwright-bdd' : 'Reqnroll',
			status: tags.includes('@ignore') ? 'Planned' : 'Covered',
		})
		pending = []
	}

	return { claims, problems }
}

/** Every constraint a docs page declares, with the claims it names. */
export function readConstraints(path, source) {
	const constraints = []
	const problems = []
	const markers = [...source.matchAll(CONSTRAINT)]

	for (const [index, marker] of markers.entries()) {
		const until = index + 1 < markers.length ? markers[index + 1].index : source.length
		const body = source.slice(marker.index, until)
		const verified = body.match(VERIFIED_BY)

		if (!verified) {
			problems.push(`${path}: ${marker[1]} names nothing that verifies it — add "*Verified by: …*", or "none — <reason>" when no scenario can`)
			continue
		}

		const text = verified[1].trim()
		constraints.push({
			id: marker[1],
			page: path,
			verifiedBy: text.startsWith('none') ? [] : [...text.matchAll(/REQ-[A-Z]+-\d{3}/g)].map((match) => match[0]),
			note: text.replace(/\s+/g, ' ').replace(/\.$/, ''),
		})
	}

	return { constraints, problems }
}

function featureFiles(root) {
	const walk = (dir) =>
		readdirSync(dir).flatMap((entry) => {
			const path = join(dir, entry)
			return statSync(path).isDirectory() ? walk(path) : path.endsWith('.feature') ? [path] : []
		})
	return walk(join(root, 'features'))
		.map((path) => path.slice(root.length + 1))
		.sort()
}

const escape = (text) => text.replaceAll('|', '\\|')

export function render(claims, constraints) {
	const planned = claims.filter((claim) => claim.status === 'Planned').length
	const lines = [
		'---',
		'title: Traceability',
		'description: Generated matrix of every claim, the scenario that states it, and every constraint that names one.',
		'type: guide',
		'---',
		'',
		'# Traceability',
		'',
		'> **Generated file — do not edit by hand.**',
		'> Regenerate with `node tools/traceability.mjs`. CI fails on a difference',
		'> ([ADR-0084](decisions/ADR-0084-stable-claim-ids-and-a-generated-traceability-matrix.md)).',
		'',
		`${claims.length} claims across ${new Set(claims.map((claim) => claim.area)).size} areas: ` +
			`${claims.length - planned} covered by a step definition today, ${planned} still \`@ignore\`. ` +
			`${constraints.length} constraints.`,
		'',
		'## Claims',
		'',
		'| Claim | Area | Scenario | Engine | Status |',
		'|---|---|---|---|---|',
		...claims.map((claim) => `| \`${claim.id}\` | ${claim.area} | ${escape(claim.scenario)} | ${claim.engine} | ${claim.status} |`),
		'',
		'## Constraints',
		'',
		'A constraint states something the system must be true of; the claims beside',
		'it are the scenarios that prove it. `none` is an honest answer — an',
		'infrastructure or test-suite property is not observable from a scenario —',
		'and it carries its reason.',
		'',
		'| Constraint | Page | Verified by |',
		'|---|---|---|',
		...constraints.map(
			(constraint) =>
				`| \`${constraint.id}\` | ${constraint.page.replace('docs/', '')} | ` +
				`${constraint.verifiedBy.length > 0 ? constraint.verifiedBy.map((id) => `\`${id}\``).join(', ') : escape(constraint.note)} |`,
		),
		'',
	]
	return lines.join('\n')
}

export function build(root = ROOT) {
	const problems = []
	const claims = []
	const constraints = []

	for (const path of featureFiles(root)) {
		const result = readClaims(path, readFileSync(join(root, path), 'utf8'))
		claims.push(...result.claims)
		problems.push(...result.problems)
	}

	for (const path of CONSTRAINT_PAGES) {
		const result = readConstraints(path, readFileSync(join(root, path), 'utf8'))
		constraints.push(...result.constraints)
		problems.push(...result.problems)
	}

	const seen = new Map()
	for (const claim of claims) {
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

	return { claims, constraints, problems, matrix: render(claims, constraints) }
}

/**
 * Builds the matrix and writes it, reporting the way the command line does
 * without exiting, so a test can exercise both outcomes. Returns the exit code.
 */
export function main(root = ROOT, { write = true } = {}) {
	const { claims, constraints, problems, matrix } = build(root)

	if (problems.length > 0) {
		for (const problem of problems) console.error(`::error::${problem}`)
		return 1
	}

	if (write) writeFileSync(join(root, OUTPUT), matrix)
	console.log(`${claims.length} claim(s) and ${constraints.length} constraint(s) written to ${OUTPUT}.`)
	return 0
}

const runAsCommand = String(process.argv[1]).endsWith('traceability.mjs')
if (runAsCommand) process.exit(main())
