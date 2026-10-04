import { describe, it } from 'node:test'
import assert from 'node:assert/strict'
import { mkdirSync, mkdtempSync, writeFileSync } from 'node:fs'
import { tmpdir } from 'node:os'
import { dirname, join } from 'node:path'
import { fileURLToPath } from 'node:url'

import { RULES, lintScenarios, lintSource, main, parse, placeholders, stripQuotes, units, wordRule } from '../../../tools/gherkin/lint-scenarios.ts'

const REPO = join(dirname(fileURLToPath(import.meta.url)), '..', '..', '..')

/** A feature holding one scenario with `steps`, or the whole `body` when it starts with a keyword. */
function feature(body: string): string {
	return /^(?:@|Scenario|Background|Rule)/m.test(body) ? `Feature: Sample\n\n${body}\n` : `Feature: Sample\n\nScenario: A sample\n${body}\n`
}

/** The rule IDs and found text of every violation in `body`. */
function found(body: string): Array<[string, string]> {
	return lintSource('sample', 'sample.feature', feature(body)).map(({ rule, found: text }) => [rule, text])
}

/** The violations of one rule only. */
function only(rule: string, body: string): string[] {
	return found(body)
		.filter(([id]) => id === rule)
		.map(([, text]) => text)
}

describe('no-http-status', () => {
	it('fails a status code in a step', () => {
		assert.deepEqual(only('no-http-status', '  Then the API answers 403'), ['403'])
	})

	it('fails a status code in an Examples cell a step reads', () => {
		const body = ['Scenario Outline: Codes', '  Then the request is refused with <status>', '', 'Examples:', '  | status |', '  | 404    |'].join('\n')
		assert.deepEqual(only('no-http-status', body), ['404'])
	})

	it('passes an outcome phrase, a size, a length, and a count', () => {
		assert.deepEqual(only('no-http-status', '  Then the request is refused as forbidden\n  And a file of 250 MB, 501 characters long, of 249 countries'), [])
	})
})

describe('no-storage-identifiers', () => {
	it('fails snake_case, camelCase, and PascalCase identifiers', () => {
		assert.deepEqual(only('no-storage-identifiers', '  Then report_answers.value holds AiSummaryEn and allowFutureDates'), ['report_answers', 'AiSummaryEn', 'allowFutureDates'])
	})

	it('fails an identifier in a code span, which is still an identifier', () => {
		assert.deepEqual(only('no-storage-identifiers', '  Then `consent_publish` is true'), ['consent_publish'])
	})

	it('fails storage and wire words, and a configuration key', () => {
		assert.deepEqual(only('no-storage-identifiers', '  Then Postgres refuses the column\n  And an outbox DTO carries a JSON boolean\n  And startup names Translation:Model'), [
			'Postgres',
			'column',
			'outbox',
			'DTO',
			'boolean',
			'JSON',
			'Translation:Model',
		])
	})

	it('passes quoted interface copy, product names, key names, and the verb "triggers"', () => {
		assert.deepEqual(only('no-storage-identifiers', '  Then the page shows "report_answers" behind CloudFront\n  And ArrowDown moves on, which triggers the prompt'), [])
	})

	it('skips an Examples column every step reads inside quotes', () => {
		const body = ['Scenario Outline: Reasons', '  Then it is refused with the reason "<reason>"', '', 'Examples:', '  | reason    |', '  | too_large |'].join('\n')
		assert.deepEqual(only('no-storage-identifiers', body), [])
	})

	it('reads a quoted column when one step also reads it bare', () => {
		const body = ['Scenario Outline: Reasons', '  Given the reason "<reason>"', '  Then it names <reason>', '', 'Examples:', '  | reason    |', '  | too_large |'].join('\n')
		assert.deepEqual(only('no-storage-identifiers', body), ['too_large'])
	})

	it('skips an Examples column no step reads', () => {
		const body = ['Scenario Outline: Unread', '  Then it holds <value>', '', 'Examples:', '  | value | note_column |', '  | one   | snake_case  |'].join('\n')
		assert.deepEqual(only('no-storage-identifiers', body), [])
	})
})

describe('no-transport-terms', () => {
	it('fails "the API", HTTP verbs, endpoints, routes, bodies, query strings, and headers', () => {
		const body = [
			'  Given the API is running',
			'  When a POST reaches the endpoint /api/v1/reports',
			'  Then the request body and query string are empty',
			'  And the response carries the header X-Content-Type-Options: nosniff',
		].join('\n')
		assert.deepEqual(only('no-transport-terms', body), ['API', 'api', 'POST', 'endpoint', '/api/v1/reports', 'request body', 'query string', 'header X-Content-Type-Options', 'X-Content-Type-Options'])
	})

	it('passes the site header, a slash inside a word, markup, and a quoted address', () => {
		assert.deepEqual(only('no-transport-terms', '  Then the header shows yes/no, a <b>bold</b> tag, and "/reports/"'), [])
	})
})

describe('no-rationale', () => {
	it('fails a reason in a title, a step, or a cell a step reads', () => {
		const body = [
			'Scenario Outline: Kept rather than refused',
			'  Then it is kept because <why>',
			'  And it closes, since nothing remains',
			'',
			'Examples:',
			'  | why                    |',
			'  | instead of it, nothing |',
		].join('\n')
		assert.deepEqual(only('no-rationale', body), ['rather than', 'because', ', since', 'instead of'])
	})

	it('passes a temporal "since" and a contrast stated as an assertion', () => {
		assert.deepEqual(only('no-rationale', '  Given a reviewer has since changed it\n  Then it shows a sign-out action and no sign-in action'), [])
	})
})

describe('no-locale-codes', () => {
	it('fails a locale code in a step, and passes one in an Examples cell', () => {
		const body = ['Scenario Outline: Codes', '  Given a report in fr-CA', '  Then its language is "<code>" and <other>', '', 'Examples:', '  | code  | other |', '  | en-CA | en-CA |'].join('\n')
		assert.deepEqual(only('no-locale-codes', body), ['fr-CA'])
	})
})

describe('what the rules read', () => {
	it('reads the Feature, Rule, and Scenario names, each once', () => {
		const source = ['Feature: The API', '', 'Rule: The API again', '', 'Scenario: One', '  Then it holds', '', 'Scenario: Two', '  Then it holds'].join('\n')
		const violations = lintSource('sample', 'sample.feature', `${source}\n`).filter(({ rule }) => rule === 'no-transport-terms')
		assert.deepEqual(
			violations.map(({ line }) => line),
			[1, 3],
		)
	})

	it('reads a Background, but not a doc string, a data table, or a comment', () => {
		const body = [
			'',
			'',
			'Background:',
			'  Given the API is up',
			'',
			'Scenario: Quiet',
			'  # the API, in a comment',
			'  Given a table:',
			'    | report_answers |',
			'  And a doc string:',
			'    """',
			'    POST /api/v1/reports',
			'    """',
		].join('\n')
		assert.deepEqual(found(body), [['no-transport-terms', 'API']])
	})

	it('reads a description for transport terms and rationale only, each line where it sits', () => {
		const source = ['Feature: Sample', 'The report_answers table is fine here,', 'but the API is not, because it is transport.', '', 'Scenario: One', '  Then it holds'].join('\n')
		assert.deepEqual(
			lintSource('sample', 'sample.feature', `${source}\n`).map(({ rule, line, found: text }) => [rule, line, text]),
			[
				['no-rationale', 3, 'because'],
				['no-transport-terms', 3, 'API'],
			],
		)
	})

	it('maps each placeholder to whether every use of it is quoted', () => {
		const scenario = { name: 'Of <a>', steps: [{ text: 'it says "<b>" and <c>' }, { text: 'and "<c>"' }] }
		assert.deepEqual([...placeholders(scenario)], [
			['a', false],
			['b', true],
			['c', false],
		])
	})

	it('strips double and curly quotes', () => {
		assert.equal(stripQuotes('a "b" c “d” e'), 'a   c   e')
	})

	it('gives each unit its steps, so a rule can judge a scenario\'s shape', () => {
		const parsed = parse(feature('  Given one\n  When two\n  Then three'))
		assert.ok(parsed)
		const [unit] = units('sample', 'sample.feature', parsed)
		assert.deepEqual(
			unit.steps.map((step) => step.keywordType),
			['Context', 'Action', 'Outcome'],
		)
		const tooLong = { id: 'max-steps', message: 'at most 2 steps', check: (u: typeof unit) => (u.steps.length > 2 ? [{ line: u.line, found: `${u.steps.length} steps` }] : []) }
		assert.deepEqual(
			lintSource('sample', 'sample.feature', feature('  Given one\n  When two\n  Then three'), [tooLong]).map(({ found: text }) => text),
			['3 steps'],
		)
	})

	it('builds a word rule whose patterns may omit the global flag', () => {
		const rule = wordRule('x', 'x', [/cat/i])
		const parsed = parse(feature('  Then a Cat and a cat'))
		assert.ok(parsed)
		assert.equal(rule.check(units('sample', 'sample.feature', parsed)[0]).length, 2)
	})
})

/** A repository with one area per entry of `areas`. */
function tree(areas: Record<string, string>): string {
	const root = mkdtempSync(join(tmpdir(), 'lint-scenarios-'))
	mkdirSync(join(root, '.spec', 'features'), { recursive: true })
	writeFileSync(join(root, '.spec', 'features', 'README.md'), '# Not an area\n')
	for (const [area, source] of Object.entries(areas)) {
		mkdirSync(join(root, '.spec', 'features', area), { recursive: true })
		writeFileSync(join(root, '.spec', 'features', area, `${area}.feature`), source)
	}
	mkdirSync(join(root, '.spec', 'features', 'empty-area'), { recursive: true })
	return root
}

describe('lintScenarios and main', () => {
	it('lints a feature file nested anywhere under the features, named by its directory', () => {
		const root = tree({})
		mkdirSync(join(root, '.spec', 'features', 'nested', 'deeper'), { recursive: true })
		writeFileSync(join(root, '.spec', 'features', 'nested', 'deeper', 'extra.feature'), feature('  Then the API answers'))
		assert.deepEqual(
			lintScenarios(root).map(({ rule, file }) => [rule, file]),
			[['no-transport-terms', '.spec/features/nested/deeper/extra.feature']],
		)
	})

	it('finds nothing when there is no features directory', () => {
		assert.deepEqual(lintScenarios(mkdtempSync(join(tmpdir(), 'lint-scenarios-'))), [])
	})

	it('lints every area, and reports a file that does not parse', () => {
		const root = tree({ clean: feature('  Then the request is refused as forbidden'), leaky: feature('  Then the API answers 403'), broken: 'Not a feature at all\n' })
		const violations = lintScenarios(root)
		assert.deepEqual(
			violations.map(({ rule, file }) => [rule, file.split('/').pop()]),
			[
				['parse', 'broken.feature'],
				['no-http-status', 'leaky.feature'],
				['no-transport-terms', 'leaky.feature'],
			],
		)
	})

	it('exits 1 with an annotation per violation and a count per rule', () => {
		const root = tree({ leaky: feature('  Then the API answers 403') })
		const lines: string[] = []
		assert.equal(main(root, (line) => lines.push(line)), 1)
		assert.match(lines[0], /^::error file=\.spec\/features\/leaky\/leaky\.feature,line=4::no-http-status: "403": /)
		assert.match(lines.at(-1) ?? '', /^2 scenario lint violation\(s\): no-http-status 1, no-storage-identifiers 0, no-transport-terms 1,/)
	})

	it('exits 0 when every scenario keeps to every rule', () => {
		const lines: string[] = []
		assert.equal(main(tree({ clean: feature('  Then the request is refused as forbidden') }), (line) => lines.push(line)), 0)
		assert.deepEqual(lines, [`The scenarios keep to ${RULES.map((rule) => rule.id).join(', ')}.`])
	})

	it('finds nothing in this repository\'s scenarios', () => {
		assert.deepEqual(lintScenarios(REPO), [])
	})
})
