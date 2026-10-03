import { describe, it } from 'node:test'
import assert from 'node:assert/strict'
import { readFileSync } from 'node:fs'

import { CATEGORIES, claimsInMatrix, judge, main, parseExemption, rejectExemption } from '../../../tools/spec/check-feature-coverage.ts'
import { render } from '../../../tools/spec/generate-traceability.ts'

// Rendered by the real generator, so this fixture follows the matrix format.
const MATRIX = render(
	[
		{ id: 'REQ-SUB-012', area: 'report-submission', scenario: 'Attachments stream', engine: 'Reqnroll', status: 'Covered' },
		{ id: 'REQ-SUB-013', area: 'report-submission', scenario: 'Persisted atomically', engine: 'Reqnroll', status: 'Covered' },
		{ id: 'REQ-WLD-008', area: 'web-localization-and-design', scenario: 'Theme toggle', engine: 'playwright-bdd', status: 'Covered' },
	],
	[],
)

const KNOWN = claimsInMatrix(MATRIX)

const exempt = (category: string, reason: string, claims: string): string =>
	`## Why\n\nCloses #1\n\nNo .feature scenario needed: ${category} — ${reason}\nClaims preserved: ${claims}\n`

/** Runs `main` with console output captured, restoring it afterwards even on failure. */
function runMain(input: { changed: string[]; features: string[]; body: string }): { code: number; output: { log: string[]; error: string[] } } {
	const output: { log: string[]; error: string[] } = { log: [], error: [] }
	const original = { log: console.log, error: console.error }
	console.log = (...args: unknown[]) => output.log.push(args.join(' '))
	console.error = (...args: unknown[]) => output.error.push(args.join(' '))
	try {
		return { code: main({ matrix: MATRIX, ...input }), output }
	} finally {
		console.log = original.log
		console.error = original.error
	}
}

describe('claimsInMatrix', () => {
	it('reads every claim id the matrix declares', () => {
		assert.deepEqual([...KNOWN].sort(), ['REQ-SUB-012', 'REQ-SUB-013', 'REQ-WLD-008'])
	})
})

describe('parseExemption', () => {
	it('finds nothing in a body that claims nothing', () => {
		assert.equal(parseExemption('## What changed\n\nAdded a thing.\n'), null)
	})

	it('reads the category, the reason, and the claims', () => {
		const exemption = parseExemption(exempt('refactor', 'extracted the ingest loop, same behavior', 'REQ-SUB-012, REQ-SUB-013'))!

		assert.equal(exemption.category, 'refactor')
		assert.match(exemption.reason, /extracted the ingest loop/)
		assert.deepEqual(exemption.claims, ['REQ-SUB-012', 'REQ-SUB-013'])
	})

	it('accepts the backticked spelling the old gate used', () => {
		const exemption = parseExemption('No `.feature` scenario needed: docs — wording only, nothing executes\nClaims preserved: REQ-SUB-012\n')!

		assert.equal(exemption.category, 'docs')
	})

	it('records that no claims line was given, rather than inventing one', () => {
		const exemption = parseExemption('No .feature scenario needed: refactor — moved the loop somewhere better\n')!

		assert.equal(exemption.citesClaims, false)
		assert.deepEqual(exemption.claims, [])
	})

	it('reads a reason and a claims list that the merge queue wrapped onto several lines', () => {
		const message = [
			'Lint with typescript-eslint strict-type-checked (#781) (#795)',
			'',
			'No .feature scenario needed: refactor — lint configuration and',
			'type-level fixes only; every handler, guard, request and rendered',
			'element behaves and renders exactly as before.',
			'Claims preserved: REQ-SUB-013, REQ-SUB-053, REQ-SUB-054,',
			'REQ-WLD-032',
			'',
			'## Screenshots',
			'REQ-SUB-099 is mentioned later and is not a preserved claim.',
		].join('\n')

		const exemption = parseExemption(message)!

		assert.equal(exemption.reason, 'lint configuration and type-level fixes only; every handler, guard, request and rendered element behaves and renders exactly as before.')
		assert.deepEqual(exemption.claims, ['REQ-SUB-013', 'REQ-SUB-053', 'REQ-SUB-054', 'REQ-WLD-032'])
		assert.deepEqual(rejectExemption(exemption, ['src/web/src/a.ts'], new Set(exemption.claims)), [])
	})

	it('stops a wrapped reason at a blank line, a heading, a list item, or the next directive', () => {
		for (const next of ['', '## Screenshots', '- a list item', 'Claims preserved: REQ-SUB-012']) {
			const exemption = parseExemption(`No .feature scenario needed: refactor — moved the loop\nsomewhere better\n${next}\nnot part of it`)!

			assert.equal(exemption.reason, 'moved the loop somewhere better', JSON.stringify(next))
		}
	})

	it('reads a body with Windows line endings', () => {
		const exemption = parseExemption('No .feature scenario needed: refactor — moved the loop\r\nsomewhere better\r\nClaims preserved: REQ-SUB-012\r\n')!

		assert.equal(exemption.reason, 'moved the loop somewhere better')
		assert.deepEqual(exemption.claims, ['REQ-SUB-012'])
	})
})

describe('rejectExemption', () => {
	const ok = { category: 'refactor', reason: 'extracted the ingest loop, behavior unchanged', claims: ['REQ-SUB-012'], citesClaims: true }

	it('accepts a well-formed citation', () => {
		assert.deepEqual(rejectExemption(ok, ['src/HpacSafety.Api/Reports/Endpoints.cs'], KNOWN), [])
	})

	it('refuses a category nobody agreed to', () => {
		const problems = rejectExemption({ ...ok, category: 'cleanup' }, ['src/a.cs'], KNOWN)

		assert.match(problems[0], /not a reason a scenario can be skipped/)
	})

	it('refuses a reason that is only the category typed twice', () => {
		const problems = rejectExemption({ ...ok, reason: 'refactor' }, ['src/a.cs'], KNOWN)

		assert.match(problems[0], /is not a reason/)
	})

	it('refuses an exemption that cites no claims at all', () => {
		const problems = rejectExemption({ ...ok, claims: [], citesClaims: false }, ['src/a.cs'], KNOWN)

		assert.match(problems[0], /No "Claims preserved:" line/)
	})

	it('refuses a claims line that names no id at all', () => {
		const problems = rejectExemption({ ...ok, claims: [], citesClaims: true }, ['src/a.cs'], KNOWN)

		assert.match(problems[0], /names no claim id/)
	})

	it('refuses a claim id no scenario declares', () => {
		const problems = rejectExemption({ ...ok, claims: ['REQ-SUB-999'] }, ['src/a.cs'], KNOWN)

		assert.match(problems[0], /REQ-SUB-999 is not a claim any scenario declares/)
	})

	it('contradicts a test-only exemption that touched production code', () => {
		const problems = rejectExemption({ ...ok, category: 'test-only' }, ['tests/a.cs', 'src/b.cs'], KNOWN)

		assert.equal(problems.length, 1)
		assert.match(problems[0], /"test-only" but these are not tests: src\/b\.cs/)
	})

	it('accepts a test-only exemption that touched only tests', () => {
		assert.deepEqual(rejectExemption({ ...ok, category: 'test-only' }, ['tests/a.cs'], KNOWN), [])
	})

	it('accepts a test-only exemption that touched only web unit tests beside their code', () => {
		const changed = ['src/web/src/lib/sortChoices.test.ts', 'src/web/src/Foo.test.tsx', 'tests/a.cs']

		assert.deepEqual(rejectExemption({ ...ok, category: 'test-only' }, changed, KNOWN), [])
	})

	it('does not take a web file that merely mentions test as a test', () => {
		const problems = rejectExemption({ ...ok, category: 'test-only' }, ['src/web/src/contest.tsx', 'src/web/src/Foo.test.tsx.bak'], KNOWN)

		assert.match(problems[0], /"test-only" but these are not tests: src\/web\/src\/contest\.tsx, src\/web\/src\/Foo\.test\.tsx\.bak/)
	})

	it('contradicts a docs exemption that touched code', () => {
		const problems = rejectExemption({ ...ok, category: 'docs' }, ['src/a.cs'], KNOWN)

		assert.match(problems[0], /"docs" but these are not documentation/)
	})

	it('contradicts a dependency exemption that touched something else', () => {
		const problems = rejectExemption({ ...ok, category: 'dependency' }, ['src/HpacSafety.Api/Program.cs'], KNOWN)

		assert.match(problems[0], /not dependency manifests/)
	})

	it('accepts a dependency exemption over manifests and lock files', () => {
		const changed = ['src/HpacSafety.Api/HpacSafety.Api.csproj', 'Directory.Packages.props', 'src/web/package-lock.json', 'src/HpacSafety.Worker/Dockerfile']

		assert.deepEqual(rejectExemption({ ...ok, category: 'dependency' }, changed, KNOWN), [])
	})
})

describe('judge', () => {
	const body = exempt('refactor', 'extracted the ingest loop, behavior unchanged', 'REQ-SUB-012')

	it('passes a change that touched no behavior-bearing file', () => {
		assert.equal(judge({ changed: [], features: [], body: '', knownClaims: KNOWN }).ok, true)
	})

	it('passes a behavior change that changed a scenario too', () => {
		const verdict = judge({ changed: ['src/a.cs'], features: ['.spec/features/media/media.feature'], body: '', knownClaims: KNOWN })

		assert.equal(verdict.ok, true)
	})

	it('fails a behavior change with no scenario and no exemption', () => {
		const verdict = judge({ changed: ['src/a.cs'], features: [], body: '## What changed\n', knownClaims: KNOWN })

		assert.equal(verdict.ok, false)
		assert.deepEqual(verdict.problems, [])
	})

	it('passes a behavior change whose exemption cites real claims', () => {
		const verdict = judge({ changed: ['src/a.cs'], features: [], body, knownClaims: KNOWN })

		assert.equal(verdict.ok, true)
		assert.match(verdict.note, /preserving REQ-SUB-012/)
	})

	it('passes a manifest-only diff with no exemption line at all', () => {
		const changed = ['src/HpacSafety.Api/HpacSafety.Api.csproj', 'Directory.Packages.props', 'src/web/package-lock.json', 'src/HpacSafety.Worker/Dockerfile']
		const verdict = judge({ changed, features: [], body: '', knownClaims: KNOWN })

		assert.equal(verdict.ok, true)
		assert.match(verdict.note, /Dependency manifests only/)
	})

	it('passes a single manifest-only diff with no exemption line', () => {
		const verdict = judge({ changed: ['src/web/package-lock.json'], features: [], body: '', knownClaims: KNOWN })

		assert.equal(verdict.ok, true)
		assert.match(verdict.note, /Dependency manifest only/)
	})

	it('still fails a mixed diff with no exemption', () => {
		const verdict = judge({ changed: ['src/web/package-lock.json', 'src/a.cs'], features: [], body: '', knownClaims: KNOWN })

		assert.equal(verdict.ok, false)
	})

	it('still requires a well-formed exemption for a mixed diff, unaffected by the manifest carve-out', () => {
		const verdict = judge({ changed: ['src/web/package-lock.json', 'src/a.cs'], features: [], body, knownClaims: KNOWN })

		assert.equal(verdict.ok, true)
		assert.match(verdict.note, /preserving REQ-SUB-012/)
	})
})

describe('main', () => {
	it('names every reason an exemption failed, not just the first', () => {
		const body = 'No .feature scenario needed: cleanup — tidy\nClaims preserved: REQ-SUB-999\n'
		const { code, output } = runMain({ changed: ['src/a.cs'], features: [], body })

		assert.equal(code, 1)
		const errors = output.error.join('\n')
		assert.match(errors, /not a reason a scenario can be skipped/)
		assert.match(errors, /is not a reason/)
		assert.match(errors, /REQ-SUB-999 is not a claim/)
	})

	it('tells a malformed exemption that it is malformed, not that it is missing', () => {
		const { code, output } = runMain({ changed: ['src/a.cs'], features: [], body: 'No .feature scenario needed: refactor' })

		assert.equal(code, 1)
		assert.match(output.error.join('\n'), /not in the required shape/)
	})

	it('tells a bare behavior change to write the scenario', () => {
		const { code, output } = runMain({ changed: ['src/a.cs'], features: [], body: '' })

		assert.equal(code, 1)
		assert.match(output.error.join('\n'), /Write the scenario/)
	})

	it('reports the claims an accepted exemption cited', () => {
		const body = exempt('revert', 'undoing the change that broke the queue', 'REQ-SUB-013')
		const { code, output } = runMain({ changed: ['src/a.cs'], features: [], body })

		assert.equal(code, 0)
		assert.match(output.log.join('\n'), /Exempt as "revert", preserving REQ-SUB-013/)
	})

	it('passes a manifest-only diff with an empty body', () => {
		const { code, output } = runMain({ changed: ['src/web/package-lock.json'], features: [], body: '' })

		assert.equal(code, 0)
		assert.match(output.log.join('\n'), /Dependency manifest only/)
	})

	it('offers every category in its guidance', () => {
		const { output } = runMain({ changed: ['src/a.cs'], features: [], body: '' })

		for (const category of Object.keys(CATEGORIES)) assert.match(output.error.join('\n'), new RegExp(category))
	})
})

// The template is where an author writes the pull-request body, so it names the
// categories the check accepts (issue #473, lesson 0022).
describe('pull request template', () => {
	const template = readFileSync(new URL('../../../.github/pull_request_template.md', import.meta.url), 'utf8')

	it('lists exactly the categories the check accepts, with their meanings', () => {
		const listed = Object.fromEntries([...template.matchAll(/^- ([a-z-]+) — (.+)$/gm)].map(([, category, meaning]) => [category, meaning]))

		assert.deepEqual(listed, CATEGORIES)
	})

	it('is not itself read as an exemption when left in a body', () => {
		assert.equal(parseExemption(template), null)
	})
})
