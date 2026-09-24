#!/usr/bin/env node
// A behavior change is covered by a scenario, or it cites the claims it
// preserves (ADR-0083, ADR-0090).
//
// CI cannot judge whether a change is behavioral — only a person or an agent
// can. What it can do is refuse to accept an unexamined assertion. The old
// escape was one free-text line that exempted a whole pull request and cost
// less than compliance; a rule whose escape hatch is cheaper than obeying it
// selects for the escape hatch.
//
// So an exemption is a citation, not a claim of innocence. A change that
// genuinely needs no new scenario preserves behavior some existing claim
// already states, and naming those claims is both the proof and the work:
//
//     No .feature scenario needed: refactor — extracted the ingest loop
//     Claims preserved: REQ-SUB-012, REQ-SUB-013
//
// The ids must exist in the generated matrix, the category must be one of a
// closed set, and the categories that can be checked against the diff are.
//
// The exit code is the contract.
import { readFileSync } from 'node:fs'

// Why a scenario might genuinely be unnecessary. Deliberately closed: a new
// category is a decision somebody argues for, not a word somebody types.
export const CATEGORIES = {
	refactor: 'behavior is unchanged; the code that produces it moved',
	styling: 'appearance only, with no change to what the page does',
	dependency: 'a package or lock version moved',
	'test-only': 'tests changed and no production code did',
	build: 'build, packaging, or tooling configuration',
	revert: 'an earlier change is being undone',
	docs: 'documentation only',
}

const EXEMPTION = /^No `?\.feature`? scenario needed:\s*([a-z-]+)\s*[—-]\s*(.+)$/im
// A body that reaches for the exemption without its shape — the old one-line
// form, or a category with no reason. Reported as a malformed exemption rather
// than as no exemption at all, so the author fixes the line they wrote instead
// of wondering why the one they wrote was ignored.
const ATTEMPTED = /^No `?\.feature`? scenario needed:/im
const PRESERVED = /^Claims preserved:\s*(.+)$/im
const CLAIM = /REQ-[A-Z]+-\d{3}/g

// A reason has to carry information. One word repeating the category is not a
// reason, it is the category typed twice.
const MINIMUM_REASON_WORDS = 4

const DEPENDENCY_MANIFESTS = [
	/(^|\/)[^/]+\.csproj$/,
	/(^|\/)Directory\.Packages\.props$/,
	/(^|\/)Directory\.Build\.props$/,
	/(^|\/)package(-lock)?\.json$/,
	/(^|\/)[^/]+\.lock(\.hcl)?$/,
	// A Dockerfile pins its base image, which Renovate moves (ADR-0120).
	/(^|\/)Dockerfile$/,
]

/** Every claim id the generated matrix declares. */
export function claimsInMatrix(matrix) {
	return new Set(matrix.match(CLAIM) ?? [])
}

/** The exemption a pull-request body declares, if it declares one. */
export function parseExemption(body) {
	const declared = body.match(EXEMPTION)
	if (!declared) return null

	const preserved = body.match(PRESERVED)
	return {
		category: declared[1].toLowerCase(),
		reason: declared[2].trim(),
		claims: preserved ? [...new Set(preserved[1].match(CLAIM) ?? [])] : [],
		citesClaims: preserved !== null,
	}
}

const isTest = (path) => path.startsWith('tests/')
const isMarkdown = (path) => path.endsWith('.md') || path.endsWith('.mdc')
const isDependencyManifest = (path) => DEPENDENCY_MANIFESTS.some((pattern) => pattern.test(path))

/**
 * Why this exemption is not acceptable, or an empty list. Checks the shape
 * first, then the categories whose honesty the diff can settle on its own.
 */
export function rejectExemption(exemption, changed, knownClaims) {
	const problems = []

	if (!Object.hasOwn(CATEGORIES, exemption.category)) {
		problems.push(
			`"${exemption.category}" is not a reason a scenario can be skipped. Use one of: ${Object.keys(CATEGORIES).join(', ')}.`,
		)
	}

	if (exemption.reason.split(/\s+/).filter(Boolean).length < MINIMUM_REASON_WORDS) {
		problems.push(`"${exemption.reason}" is not a reason — say what changed and why no behavior did.`)
	}

	if (!exemption.citesClaims) {
		problems.push(
			'No "Claims preserved:" line. A change that needs no new scenario preserves behavior an existing claim already states — name those claims.',
		)
	} else if (exemption.claims.length === 0) {
		problems.push('"Claims preserved:" names no claim id.')
	} else {
		for (const claim of exemption.claims) {
			if (!knownClaims.has(claim)) problems.push(`${claim} is not a claim any scenario declares.`)
		}
	}

	// A category the diff can contradict is checked against the diff. The rest
	// rest on the citation and on review.
	if (exemption.category === 'test-only') {
		const production = changed.filter((path) => !isTest(path))
		if (production.length > 0) problems.push(`"test-only" but these are not tests: ${production.join(', ')}.`)
	}

	if (exemption.category === 'docs') {
		const code = changed.filter((path) => !isMarkdown(path))
		if (code.length > 0) problems.push(`"docs" but these are not documentation: ${code.join(', ')}.`)
	}

	if (exemption.category === 'dependency') {
		const other = changed.filter((path) => !isDependencyManifest(path))
		if (other.length > 0) problems.push(`"dependency" but these are not dependency manifests: ${other.join(', ')}.`)
	}

	return problems
}

/**
 * The verdict for one pull request. `changed` is the behavior-bearing files it
 * touched, `features` the `.feature` files it touched.
 */
export function judge({ changed, features, body, knownClaims }) {
	if (changed.length === 0) return { ok: true, note: 'No behavior-bearing file changed.' }
	if (features.length > 0) return { ok: true, note: `Scenarios changed alongside: ${features.join(', ')}.` }

	const exemption = parseExemption(body)
	if (!exemption) {
		const malformed = ATTEMPTED.test(body)
			? ['The exemption line is not in the required shape: "No .feature scenario needed: <category> — <reason>", with a "Claims preserved:" line naming real claim ids.']
			: []
		return { ok: false, changed, problems: malformed }
	}

	const problems = rejectExemption(exemption, changed, knownClaims)
	if (problems.length > 0) return { ok: false, changed, exemption, problems }

	return { ok: true, exemption, note: `Exempt as "${exemption.category}", preserving ${exemption.claims.join(', ')}.` }
}

const lines = (value) => value.split('\n').map((line) => line.trim()).filter(Boolean)

/**
 * Reports the way the command line does, without exiting, so a test can
 * exercise every outcome in-process. Returns the exit code.
 */
export function main({ changed, features, body, matrix }) {
	const verdict = judge({ changed, features, body, knownClaims: claimsInMatrix(matrix) })

	if (verdict.ok) {
		console.log(`::notice::${verdict.note}`)
		return 0
	}

	if (verdict.problems.length > 0) {
		console.error('::error::This pull request claims an exemption from scenario coverage, and the claim does not hold.')
		for (const problem of verdict.problems) console.error(`::error::${problem}`)
	} else {
		console.error('::error::This pull request changes behavior under src/ or in an e2e spec and touches no features/**/*.feature file.')
	}

	console.error('')
	console.error('Changed files with no matching scenario:')
	for (const path of verdict.changed) console.error(`  ${path}`)
	console.error('')
	console.error('Write the scenario. The specification is authored before the')
	console.error('implementation, and a change that alters behavior updates it in the')
	console.error('same pull request (ADR-0083).')
	console.error('')
	console.error('If this change genuinely alters no behavior, say so in the pull-request')
	console.error('body and name the claims it leaves standing:')
	console.error('')
	console.error('    No .feature scenario needed: <category> — <what changed, and why no behavior did>')
	console.error('    Claims preserved: REQ-XXX-000, REQ-YYY-000')
	console.error('')
	console.error(`Categories: ${Object.entries(CATEGORIES).map(([name, meaning]) => `${name} (${meaning})`).join('; ')}.`)
	console.error('')
	console.error('An exemption is a citation, not an assertion — the claims are checked')
	console.error('against docs/traceability.md (ADR-0090).')
	return 1
}

const runAsCommand = String(process.argv[1]).endsWith('feature-coverage.mjs')
if (runAsCommand) {
	process.exit(
		main({
			changed: lines(process.env.CHANGED_BEHAVIOR ?? ''),
			features: lines(process.env.CHANGED_FEATURES ?? ''),
			body: process.env.PR_BODY ?? '',
			matrix: readFileSync('docs/traceability.md', 'utf8'),
		}),
	)
}
