// The two line shapes the specification's IDs live in (ADR-0084): a claim tag
// above each scenario, and a CON id in a constraint page with the claims that
// verify it. Every generator that needs a claim or a constraint reads it here,
// so each fact has exactly one definition.
//
// Dependency-free, like every other tool in this directory. The grammar it
// consumes is two line shapes, and tools/gherkin/verify.ts has already proved
// with the official parser that the files are valid Gherkin, so this never has
// to be the thing that discovers a syntax error.
import { posix } from 'node:path'

import { FEATURES } from './spec-paths.ts'

export const CLAIM_TAG = /^\s*@(REQ-[A-Z]+-\d{3})\s*$/
const SCENARIO = /^\s*(Scenario|Scenario Outline):\s*(.+?)\s*$/
// A Rule groups scenarios inside a feature (ADR-0184). Gherkin would let a tag
// on it reach every scenario beneath; this reader takes a claim's engine and
// status from the scenario's own tags only, so a Rule carries none.
const RULE = /^\s*Rule:/
const CONSTRAINT = /\*\*(CON-[A-Z]+-\d{3})\*\*/g
const VERIFIED_BY = /\*Verified by:\s*([^*]+)\*/

/** One scenario's stable claim. */
export interface Claim {
	id: string
	area: string
	scenario: string
	engine: 'playwright-bdd' | 'Reqnroll'
	status: 'Planned' | 'Built'
	/** The scenario's other tags, in file order: `@ui`, `@ignore`, and the like. */
	tags: string[]
}

/** One normative constraint and the claims that verify it. */
export interface Constraint {
	id: string
	page: string
	verifiedBy: string[]
	note: string
}

/** Every claim a feature file declares, in file order. */
export function readClaims(path: string, source: string): { claims: Claim[]; problems: string[] } {
	const lines = source.split('\n')
	const claims: Claim[] = []
	const problems: string[] = []
	let pending: string[] = []
	let tags: string[] = []

	for (const [index, line] of lines.entries()) {
		const tag = line.match(CLAIM_TAG)
		if (tag) {
			pending.push(tag[1])
			continue
		}

		const scenario = line.match(SCENARIO)
		if (!scenario) {
			// Any other tag line belongs to the scenario below it, like the
			// claim; anything else ends the tag block, so a neighbouring
			// scenario's @ui or @ignore never reaches this one.
			if (line.trim().startsWith('@')) {
				tags.push(line.trim())
				continue
			}
			if (line.trim() === '') continue
			if (RULE.test(line) && (pending.length > 0 || tags.length > 0)) {
				problems.push(`${path}:${index + 1}: tags on a Rule are not supported — a claim's engine and status come from its own scenario's tags`)
			}
			pending = []
			tags = []
			continue
		}

		if (pending.length === 0) {
			tags = []
			problems.push(`${path}:${index + 1}: "${scenario[2]}" carries no claim ID — every scenario names one claim (ADR-0084)`)
			continue
		}
		if (pending.length > 1) {
			problems.push(`${path}:${index + 1}: "${scenario[2]}" carries ${pending.length} claim IDs (${pending.join(', ')}) — a scenario is exactly one claim`)
		}

		const split = tags.flatMap((tag) => tag.split(/\s+/))
		claims.push({
			id: pending[0],
			area: posix.relative(FEATURES, path).split('/')[0],
			scenario: scenario[2],
			engine: split.includes('@ui') ? 'playwright-bdd' : 'Reqnroll',
			status: split.includes('@ignore') ? 'Planned' : 'Built',
			tags: split,
		})
		pending = []
		tags = []
	}

	return { claims, problems }
}

/** Every constraint a docs page declares, with the claims it names. */
export function readConstraints(path: string, source: string): { constraints: Constraint[]; problems: string[] } {
	const constraints: Constraint[] = []
	const problems: string[] = []
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

const BACKGROUND = /^\s*Background:/
const EXAMPLES = /^\s*(Examples|Scenarios):/
const FEATURE = /^\s*Feature:/

/**
 * Each claim's scenario as text that only a real change alters: its tags,
 * title, description, steps, tables, and examples — plus the Background steps
 * that run before it — with comments, blank lines, and whitespace dropped.
 * Two versions of a feature file whose texts for a claim are equal changed
 * nothing that claim says (CONV-001).
 */
export function scenarioTexts(source: string): Map<string, string> {
	const lines = source.split('\n').map((line) => line.trim().replace(/\s+/g, ' '))
	const texts = new Map<string, string>()
	let featureBackground: string[] = []
	let ruleBackground: string[] = []
	let inRule = false
	let collecting: string[] | null = null
	let claim: string | null = null
	let tags: string[] = []

	const close = (): void => {
		if (claim !== null && collecting !== null) texts.set(claim, [...featureBackground, ...ruleBackground, ...collecting].join('\n'))
		claim = null
		collecting = null
	}
	// A tag line above `Examples:` belongs to the scenario it sits in.
	const nextIsExamples = (from: number): boolean => {
		for (const line of lines.slice(from + 1)) {
			if (line === '' || line.startsWith('#') || line.startsWith('@')) continue
			return EXAMPLES.test(line)
		}
		return false
	}

	for (const [index, line] of lines.entries()) {
		if (line === '' || line.startsWith('#')) continue
		if (line.startsWith('@') && !(claim !== null && nextIsExamples(index))) {
			close()
			tags.push(line)
			continue
		}
		if (FEATURE.test(line)) {
			close()
			featureBackground = []
			ruleBackground = []
			inRule = false
			tags = []
			continue
		}
		if (RULE.test(line)) {
			close()
			ruleBackground = []
			inRule = true
			tags = []
			continue
		}
		if (BACKGROUND.test(line)) {
			close()
			// A Background before any Rule is the feature's; inside a Rule, the Rule's.
			collecting = []
			if (inRule) ruleBackground = collecting
			else featureBackground = collecting
			tags = []
			continue
		}
		const scenario = SCENARIO.exec(line)
		if (scenario) {
			close()
			claim = tags.flatMap((tag) => tag.split(' ')).map((tag) => CLAIM_TAG.exec(tag)?.[1]).find((id) => id !== undefined) ?? null
			collecting = [...tags, line]
			tags = []
			continue
		}
		if (collecting !== null) collecting.push(line)
	}
	close()
	return texts
}

/** The claims whose scenario text differs between two versions of a feature file, added and removed ones included. */
export function changedScenarios(before: string, after: string): string[] {
	const [old, current] = [scenarioTexts(before), scenarioTexts(after)]
	return [...new Set([...old.keys(), ...current.keys()])].filter((id) => old.get(id) !== current.get(id)).sort()
}
