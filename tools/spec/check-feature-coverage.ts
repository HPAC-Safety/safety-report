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
//     Claims preserved: REQ-SUB-013, REQ-SUB-042
//
// The ids must exist in the generated claims (.spec/claims.json), the category must be one of a
// closed set, and the categories that can be checked against the diff are.
//
// Both halves are judged for relevance (CONV-001). Every changed file maps to
// feature areas (.spec/area-paths.json, tools/spec/area-paths.ts): a scenario
// counts only when its text changed — not whitespace or a comment — and its
// area is one the changed code maps to; a cited claim counts only when its
// area is.
//
// The judgement only; tools/spec/check-feature-coverage-diff.ts reads the diff
// and is the command line.

// Why a scenario might genuinely be unnecessary. Deliberately closed: a new
// category is a decision somebody argues for, not a word somebody types.
export const CATEGORIES: Readonly<Record<string, string>> = {
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

/** Every claim .spec/claims.json declares, with its area: its claims, not every ID it mentions. */
export function declaredClaims(claimsJson: string): Map<string, string> {
	const { claims } = JSON.parse(claimsJson) as { claims: { id: string; area: string }[] }
	return new Map(claims.map((claim) => [claim.id, claim.area]))
}

// A line that starts something new rather than continuing the line above it.
const DIRECTIVE = /^(No `?\.feature`? scenario needed:|Claims preserved:|#|[-*|>]|\d+\.\s)/i

/**
 * The lines after `index` that continue it, up to a blank line or a new
 * directive. In the merge queue the check reads the squash commit's message,
 * which GitHub hard-wraps at about 72 columns, so one exemption line arrives
 * as several (#797).
 */
function continuation(lines: readonly string[], index: number): string[] {
	const following: string[] = []
	for (const line of lines.slice(index + 1)) {
		const text = line.trim()
		if (text === '' || DIRECTIVE.test(text)) break
		following.push(text)
	}
	return following
}

/** What a pull-request body's exemption line and its `Claims preserved:` line declare. */
export interface Exemption {
	category: string
	reason: string
	claims: string[]
	citesClaims: boolean
}

/** The exemption a pull-request body declares, if it declares one. */
export function parseExemption(body: string): Exemption | null {
	const lines = body.split(/\r?\n/)
	const declarations = lines.map((line) => EXEMPTION.exec(line))
	const at = declarations.findIndex((match) => match !== null)
	const declared = declarations[at]
	if (!declared) return null

	const claimsAt = lines.findIndex((line) => PRESERVED.test(line))
	const claimsText = claimsAt === -1 ? '' : [PRESERVED.exec(lines[claimsAt])?.[1] ?? '', ...continuation(lines, claimsAt)].join(' ')
	return {
		category: declared[1].toLowerCase(),
		reason: [declared[2].trim(), ...continuation(lines, at)].join(' '),
		claims: [...new Set(claimsText.match(CLAIM) ?? [])],
		citesClaims: claimsAt !== -1,
	}
}

// A web unit test sits beside the code it tests (ADR-0188), so it is a test
// even though it lives under src/.
const isTest = (path: string): boolean => path.startsWith('tests/') || /^src\/web\/.*\.test\.tsx?$/.test(path)
const isMarkdown = (path: string): boolean => path.endsWith('.md') || path.endsWith('.mdc')
const isDependencyManifest = (path: string): boolean => DEPENDENCY_MANIFESTS.some((pattern) => pattern.test(path))

/**
 * Why this exemption is not acceptable, or an empty list. Checks the shape
 * first, then the categories whose honesty the diff can settle on its own.
 */
export function rejectExemption(exemption: Exemption, changed: readonly string[], knownClaims: ReadonlyMap<string, string>, areas: ReadonlySet<string> = new Set(knownClaims.values())): string[] {
	const problems: string[] = []

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
			const area = knownClaims.get(claim)
			if (area === undefined) problems.push(`${claim} is not a claim any scenario declares.`)
			else if (!areas.has(area)) problems.push(`${claim} belongs to ${area}, which none of the changed files maps to (${list(areas)}). Cite a claim the change leaves standing in its own area.`)
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

const list = (areas: ReadonlySet<string>): string => (areas.size > 0 ? [...areas].sort().join(', ') : 'none')

/** A claim whose scenario text the diff changed, and its area. */
export interface ChangedScenario {
	id: string
	area: string
}

/** Whether a pull request passes, with the note or problems that say why. */
export type Verdict =
	| { ok: true; note: string; exemption?: Exemption }
	| { ok: false; changed: readonly string[]; problems: string[]; exemption?: Exemption }

/** What one pull request changed, as the verdict reads it. */
export interface Change {
	/** The behavior-bearing files it touched. */
	changed: readonly string[]
	/** The `.feature` files it touched. */
	features: readonly string[]
	/** The claims whose scenario text changed. */
	scenarios: readonly ChangedScenario[]
	/** The areas its behavior-bearing and step-definition files map to. */
	areas: ReadonlySet<string>
	body: string
	/** Every claim at HEAD, with its area. */
	knownClaims: ReadonlyMap<string, string>
}

/** Why the scenarios the diff did touch do not count, for the failure message. */
function unrelated({ features, scenarios, areas }: Change): string[] {
	if (features.length > 0 && scenarios.length === 0) {
		return [`${features.join(', ')} changed, but no scenario's text did (only whitespace, comments, or feature or Rule descriptions), so it covers nothing.`]
	}
	if (scenarios.length > 0) {
		return [`${scenarios.map((scenario) => `${scenario.id} (${scenario.area})`).join(', ')} changed, but the changed code maps to ${list(areas)}, so none of them covers it.`]
	}
	return []
}

/** The verdict for one pull request. */
export function judge(change: Change): Verdict {
	const { changed, scenarios, areas, body, knownClaims } = change
	if (changed.length === 0) return { ok: true, note: 'No behavior-bearing file changed.' }
	const relevant = scenarios.filter((scenario) => areas.has(scenario.area))
	if (relevant.length > 0) return { ok: true, note: `Scenarios changed alongside, in an area the code maps to: ${relevant.map((scenario) => `${scenario.id} (${scenario.area})`).join(', ')}.` }

	// A diff that touches nothing but dependency manifests needs no exemption
	// line at all — there is no author to write one on a Renovate pull request,
	// and the category already exists to describe exactly this diff shape
	// (ADR-0090; owner decision on #600).
	if (changed.every(isDependencyManifest)) {
		return { ok: true, note: `Dependency manifest${changed.length > 1 ? 's' : ''} only: ${changed.join(', ')}.` }
	}

	const exemption = parseExemption(body)
	if (!exemption) {
		const malformed = ATTEMPTED.test(body)
			? ['The exemption line is not in the required shape: "No .feature scenario needed: <category> — <reason>", with a "Claims preserved:" line naming real claim ids.']
			: []
		return { ok: false, changed, problems: [...unrelated(change), ...malformed] }
	}

	const problems = rejectExemption(exemption, changed, knownClaims, areas)
	if (problems.length > 0) return { ok: false, changed, exemption, problems }

	return { ok: true, exemption, note: `Exempt as "${exemption.category}", preserving ${exemption.claims.join(', ')}.` }
}

/**
 * Reports the way the command line does, without exiting, so a test can
 * exercise every outcome in-process. Returns the exit code.
 */
export function main({ claims, ...change }: Omit<Change, 'knownClaims'> & { claims: string }): number {
	const verdict = judge({ ...change, knownClaims: declaredClaims(claims) })

	if (verdict.ok) {
		console.log(`::notice::${verdict.note}`)
		return 0
	}

	if (verdict.exemption) {
		console.error('::error::This pull request claims an exemption from scenario coverage, and the claim does not hold.')
	} else {
		console.error('::error::This pull request changes behavior under src/ or in an e2e spec and changes no scenario in an area that code maps to.')
	}
	for (const problem of verdict.problems) console.error(`::error::${problem}`)

	console.error('')
	console.error(`Changed files with no matching scenario (their areas: ${list(change.areas)}):`)
	for (const path of verdict.changed) console.error(`  ${path}`)
	console.error('')
	console.error('Write the scenario, in an area the code maps to (.spec/area-paths.json).')
	console.error('The specification is authored before the implementation, and a change')
	console.error('that alters behavior updates it in the same pull request (ADR-0083).')
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
	console.error('against .spec/claims.json (ADR-0090), and each must belong to an area')
	console.error('the changed files map to (CONV-001).')
	return 1
}
