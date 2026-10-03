import { describe, it } from 'node:test'
import assert from 'node:assert/strict'
import { existsSync, mkdtempSync, mkdirSync, readFileSync, readdirSync, writeFileSync } from 'node:fs'
import { createRequire } from 'node:module'
import { tmpdir } from 'node:os'
import { dirname, join } from 'node:path'
import { fileURLToPath } from 'node:url'

import {
	type PlaywrightBinding,
	type ReadScenario,
	type ReqnrollBinding,
	type Step,
	UnsupportedExpression,
	collectBindings,
	cucumberToRegExp,
	display,
	playwrightMatcher,
	readPlaywrightBindings,
	readReqnrollBindings,
	readScenarios,
	reqnrollKind,
	reqnrollMatcher,
	resolve,
	unescapeString,
} from '../../../tools/spec/step-bindings.ts'
import type { Claim } from '../../../tools/spec/read-claims.ts'

const REPO = join(dirname(fileURLToPath(import.meta.url)), '..', '..', '..')

/** A throwaway tree holding the given files. */
function tree(files: Record<string, string>): string {
	const root = mkdtempSync(join(tmpdir(), 'bindings-'))
	for (const [path, text] of Object.entries(files)) {
		mkdirSync(dirname(join(root, path)), { recursive: true })
		writeFileSync(join(root, path), text)
	}
	return root
}

const FEATURE = '.spec/features/media/media.feature'
const scenarios = (source: string, path = FEATURE): ReturnType<typeof readScenarios> => readScenarios(path, source)

describe('cucumberToRegExp', () => {
	const matches = (expression: string, text: string): boolean => cucumberToRegExp(expression).test(text)

	it('matches plain text exactly, anchored at both ends', () => {
		assert.ok(matches('a report is published', 'a report is published'))
		assert.ok(!matches('a report is published', 'a report is published twice'))
		assert.ok(!matches('report is published', 'a report is published'))
	})

	it('reads escaped characters literally', () => {
		assert.ok(matches('yes\\/no', 'yes/no'))
		assert.ok(matches('a \\(note\\) \\{x\\} \\\\', 'a (note) {x} \\'))
	})

	it('matches {string} in either quote, and an empty one', () => {
		assert.ok(matches('the label is {string}', 'the label is "Name"'))
		assert.ok(matches('the label is {string}', "the label is 'Name'"))
		assert.ok(matches('the label is {string}', 'the label is ""'))
		assert.ok(!matches('the label is {string}', 'the label is Name'))
	})

	it('matches {word}, {int}, and {}', () => {
		assert.ok(matches('the {word} role', 'the Administrator role'))
		assert.ok(!matches('the {word} role', 'the Safety Officer role'))
		assert.ok(matches('{int} files', '-3 files'))
		assert.ok(!matches('{int} files', 'three files'))
		assert.ok(matches('a {} question', 'a single-select question'))
	})

	it('treats (text) as optional and a/b as alternatives within a word', () => {
		assert.ok(matches('{int} file(s)', '1 file'))
		assert.ok(matches('{int} file(s)', '2 files'))
		assert.ok(matches('a yes/no question', 'a yes question'))
		assert.ok(matches('a yes/no question', 'a no question'))
		assert.ok(!matches('a yes/no question', 'a yes/no question'))
		assert.ok(matches('a\\(n\\) ok', 'a(n) ok'))
	})

	const refused: [string, RegExp][] = [
		['{float} metres', /parameter type \{float\}/],
		['an (a(b)) case', /nests/],
		['a () case', /empty optional/],
		['a yes/ case', /empty alternative/],
		['a {string}/no case', /parameter inside an alternative/],
		['a (x)/(y) case', /only optional text/],
		['a {string case', /never closes/],
		['a (open case', /never closes/],
		['ends with \\', /lone backslash/],
	]
	for (const [expression, why] of refused) {
		it(`refuses "${expression}"`, () => {
			assert.throws(() => cucumberToRegExp(expression), (error) => error instanceof UnsupportedExpression && why.test(error.message))
		})
	}

	it('keeps an escaped character inside an optional', () => {
		assert.ok(matches('a(\\/b) c', 'a/b c'))
		assert.ok(matches('a(\\/b) c', 'a c'))
	})
})

describe('reqnrollKind and reqnrollMatcher', () => {
	const kinds: [string, string][] = [
		['^a (\\w+) question$', 'regex'],
		['a submission DTO contains (.*)$', 'regex'],
		['a report count is (\\d+)', 'regex'],
		['the {word} role', 'cucumber'],
		['a report is published', 'cucumber'],
	]
	for (const [pattern, kind] of kinds) {
		it(`reads "${pattern}" as ${kind}`, () => assert.equal(reqnrollKind(pattern), kind))
	}

	it('anchors a regex at both ends, whichever end it already anchors', () => {
		assert.ok(reqnrollMatcher('a submission DTO contains (.*)$').test('a submission DTO contains a malformed ID'))
		assert.ok(!reqnrollMatcher('count is (\\d+)').test('the count is 3'))
		assert.ok(reqnrollMatcher('^a (\\w+) question$').test('a yes question'))
	})

	it('reads a plain pattern as a Cucumber Expression', () => {
		assert.ok(reqnrollMatcher('a yes\\/no question').test('a yes/no question'))
	})
})

describe('playwrightMatcher', () => {
	it('reads a string as a Cucumber Expression and a regex literal with its own flags', () => {
		assert.ok(playwrightMatcher('a {word} page').test('a public page'))
		assert.ok(playwrightMatcher({ source: '^the (\\w+) option$', flags: 'gi' }).test('THE manage OPTION'))
	})
})

describe('readScenarios', () => {
	it('prepends Background steps and resolves And and But to the keyword before them', () => {
		const source = [
			'Feature: Attachments',
			'',
			'  Background:',
			'    Given a member is signed in',
			'',
			'  @REQ-MED-001',
			'  Scenario: Upload',
			'    Given a file',
			'    And another file',
			'    When it is attached',
			'    Then it is stored',
			'    But it is not published',
			'    * it is quarantined',
		].join('\n')
		const { feature, scenarios: [claim], problems } = scenarios(source)

		assert.deepEqual(problems, [])
		assert.equal(feature, 'Attachments')
		assert.deepEqual(
			claim.steps.map((step) => `${step.keyword} ${step.text}`),
			['Given a member is signed in', 'Given a file', 'Given another file', 'When it is attached', 'Then it is stored', 'Then it is not published', 'Then it is quarantined'],
		)
	})

	it('reports an And with no step before it', () => {
		const { problems } = scenarios('Feature: A\n\n@REQ-MED-001\nScenario: B\n  And something\n')

		assert.match(problems[0], /has no step before it/)
	})

	it('expands an outline once per row across every Examples block, deduplicated', () => {
		const source = [
			'Feature: A',
			'@REQ-MED-001',
			'Scenario Outline: B',
			'  Given a <kind> file named <name>',
			'  Then it is <result> and <b>literal</b>',
			'  Examples:',
			'    | kind  | name | result |',
			'    | image | a    | kept   |',
			'    | image | a    | kept   |',
			'  Scenarios:',
			'    | kind  | name | result |',
			'    | video | b    | kept   |',
		].join('\n')
		const { scenarios: [claim] } = scenarios(source)

		assert.deepEqual(
			claim.steps.map((step) => step.text),
			['a image file named a', 'it is kept and <b>literal</b>', 'a video file named b'],
		)
	})

	it('reads an Examples row that has no closing bar, and fills a cell the row leaves out with nothing', () => {
		const source = [
			'Feature: A',
			'@REQ-MED-001',
			'Scenario Outline: B',
			'  Given a <kind> file named <name>',
			'  Examples:',
			'    | kind | name',
			'    | image',
		].join('\n')
		const { scenarios: [claim] } = scenarios(source)

		assert.deepEqual(claim.steps.map((step) => step.text), ['a image file named '])
	})

	it('ignores data tables, doc strings, comments, and tags', () => {
		const source = [
			'Feature: A',
			'# a comment',
			'@REQ-MED-001',
			'@ui',
			'Scenario: B',
			'  Given these addresses, in order:',
			'    | a@example.com |',
			'  And this body',
			'    """',
			'    Given not a step',
			'    """',
			'  Then done',
		].join('\n')
		const { scenarios: [claim] } = scenarios(source)

		assert.deepEqual(claim.steps.map((step) => step.text), ['these addresses, in order:', 'this body', 'done'])
	})

	it('reports Examples outside a scenario', () => {
		assert.match(scenarios('Feature: A\nExamples:\n  | a |\n').problems.join(), /Examples outside a scenario/)
	})

	it('carries claim facts from readClaims and reports a scenario without a claim', () => {
		const source = 'Feature: A\n\n@REQ-MED-001\n@ui\n@ignore\nScenario: B\n  Given x\n\nScenario: Untagged\n  Given y\n'
		const { scenarios: list, problems } = scenarios(source)

		assert.equal(list[0].engine, 'playwright-bdd')
		assert.equal(list[0].status, 'Planned')
		assert.match(problems.join('\n'), /carries no claim ID/)
	})

	it('reports when the two parsers disagree about a scenario', () => {
		const { scenarios: list, problems } = scenarios('Feature: A\n\nScenario: X\n  Given x\n\n@REQ-MED-001\nScenario: Y\n  Given y\n\n@REQ-MED-002\nScenario: X\n  Given z\n')

		assert.match(problems.join('\n'), /the two parsers disagree/)
		assert.match(problems.join('\n'), /REQ-MED-001 "Y" does not line up/)
		assert.deepEqual(list[0].steps, [])
	})

	it('adds a Rule Background to the scenarios inside that Rule only', () => {
		const source = [
			'Feature: A',
			'Background:',
			'  Given everyone',
			'Rule: First',
			'  Background:',
			'    Given the first rule',
			'  @REQ-MED-001',
			'  Scenario: B',
			'    Then b',
			'Rule: Second',
			'  @REQ-MED-002',
			'  Scenario: C',
			'    Then c',
		].join('\n')
		const { scenarios: [first, second] } = scenarios(source)

		assert.deepEqual(first.steps.map((step) => step.text), ['everyone', 'the first rule', 'b'])
		assert.equal(first.rule, 'First')
		assert.deepEqual(second.steps.map((step) => step.text), ['everyone', 'c'])
	})
})

describe('readReqnrollBindings', () => {
	const cs = (body: string): string => `namespace X;\n\n${body}\n`

	it('reads steps, unescapes doubled quotes, and carries the class scope', () => {
		const { bindings, problems } = readReqnrollBindings([
			{
				path: 'A.cs',
				source: cs(
					[
						'[Binding]',
						'[Scope(Feature = "Attachments")]',
						'[Scope(Feature = "Comments")]',
						'public sealed class ASteps',
						'{',
						'\t[Given(@"a label ""Name""")]',
						'\t[When(@"it runs")] [Then(@"it holds")]',
						'\tpublic void Step() { }',
						'}',
					].join('\n'),
				),
			},
		])

		assert.deepEqual(problems, [])
		assert.deepEqual(bindings.map((binding) => [binding.keyword, binding.pattern]), [['Given', 'a label "Name"'], ['When', 'it runs'], ['Then', 'it holds']])
		assert.deepEqual([...bindings[0].scopes].sort(), ['Attachments', 'Comments'])
	})

	it('applies a partial class scope declared in one file to its steps in another', () => {
		const { bindings } = readReqnrollBindings([
			{ path: 'A.cs', source: cs('[Binding]\n[Scope(Feature = "Comments")]\npublic partial class ASteps { }') },
			{ path: 'A.More.cs', source: cs('public partial class ASteps\n{\n\t[Then(@"more")]\n\tpublic void M() { }\n}') },
		])

		assert.deepEqual([...bindings[0].scopes], ['Comments'])
		assert.equal(bindings[0].file, 'A.More.cs')
	})

	it('keeps steps on the binding class across a nested helper class', () => {
		const { bindings } = readReqnrollBindings([
			{ path: 'A.cs', source: cs('[Binding]\npublic class ASteps\n{\n\tprivate sealed class Helper { }\n\t[Given(@"x")]\n\tpublic void X() { }\n}') },
		])

		assert.equal(bindings.length, 1)
	})

	const problemCases: [string, RegExp][] = [
		['[Binding]\npublic class A\n{\n\t[Scope(Feature = "x")]\n\tpublic void M() { }\n}', /method-level \[Scope\]/],
		['[Binding]\n[Scope(Tag = "ui")]\npublic class A { }', /Tag\|Scenario/],
		['[Binding]\npublic class A\n{\n\t[Given("not verbatim")]\n}', /cannot read/],
		['[Binding]\npublic class A\n{\n\t[StepDefinition(@"x")]\n}', /cannot read/],
		['public class A\n{\n\t[Given(@"orphan")]\n}', /outside a \[Binding\] class/],
	]
	for (const [body, why] of problemCases) {
		it(`reports ${why}`, () => {
			assert.match(readReqnrollBindings([{ path: 'A.cs', source: cs(body) }]).problems.join('\n'), why)
		})
	}
})

describe('readPlaywrightBindings', () => {
	it('reads double, single, next-line, and regex patterns, unescaping strings by hand', () => {
		const source = [
			'import { createBdd } from "playwright-bdd"',
			'const { Given, When, Then, After } = createBdd()',
			'Given("a yes\\\\/no {string}", async () => {})',
			"When('it\\'s done', async () => {})",
			'Then(',
			'\t"the next line",',
			'\tasync () => {},',
			')',
			'Then(/^the ((?:a|b)[/]c) option$/i, async () => {})',
			'// Given("commented out", async () => {})',
		].join('\n')
		const { bindings, problems } = readPlaywrightBindings('x.steps.ts', source)

		assert.deepEqual(problems, [])
		assert.deepEqual(bindings.map((binding) => binding.pattern), ['a yes\\/no {string}', "it's done", 'the next line', { source: '^the ((?:a|b)[/]c) option$', flags: 'i' }])
		assert.deepEqual(bindings.map((binding) => binding.line), [3, 4, 5, 9])
	})

	it('unescapes the JavaScript escapes a step string can hold', () => {
		assert.equal(unescapeString('a\\tb\\nc\\\\d\\"e\\x'), 'a\tb\nc\\d"ex')
	})

	it('reports a template literal pattern', () => {
		const { problems } = readPlaywrightBindings('x.ts', 'const { Given } = createBdd()\nGiven(`a ${x}`, async () => {})\n')

		assert.match(problems[0], /1 step definition\(s\) but 0 read/)
	})

	it('reports an aliased createBdd', () => {
		const { problems } = readPlaywrightBindings('x.ts', 'const { Given: G } = createBdd()\n')

		assert.match(problems[0], /alias/)
	})

	it('reports steps declared without createBdd, and ignores a file with neither', () => {
		assert.match(readPlaywrightBindings('x.ts', 'Given("x", async () => {})\n').problems[0], /without/)
		assert.deepEqual(readPlaywrightBindings('fixture.ts', 'export const x = 1\n'), { bindings: [], problems: [] })
	})
})

const step = (keyword: string, text: string): Step => ({ keyword, text, line: 1 })

describe('resolve', () => {
	const claim = (id: string, steps: Step[], { engine = 'Reqnroll', status = 'Covered', area = 'media' }: { engine?: Claim['engine']; status?: Claim['status']; area?: string } = {}): ReadScenario => ({
		id,
		area,
		scenario: id,
		engine,
		status,
		tags: [],
		rule: null,
		line: 1,
		steps,
	})
	const cs = (keyword: string, pattern: string, file: string, scopes: string[] = []): ReqnrollBinding => ({ engine: 'Reqnroll', keyword, pattern, file, line: 1, scopes: new Set(scopes) })
	const ts = (keyword: string, pattern: string, file: string): PlaywrightBinding => ({ engine: 'playwright-bdd', keyword, pattern, file, line: 1 })
	const features = (...claims: ReadScenario[]) => [{ path: FEATURE, feature: 'Attachments', scenarios: claims }]

	it('matches C# by keyword and TypeScript by text alone, each for its own engine', () => {
		const result = resolve(
			features(claim('REQ-MED-001', [step('Given', 'x')]), claim('REQ-MED-002', [step('Then', 'x')], { engine: 'playwright-bdd' })),
			[cs('When', 'x', 'Wrong.cs'), cs('Given', 'x', 'A.cs')],
			[ts('Given', 'x', 'a.steps.ts')],
		)

		assert.deepEqual(result.claims.map((each) => each.files), [['A.cs'], ['a.steps.ts']])
		assert.deepEqual(result.unused.map((binding) => binding.file), ['Wrong.cs'])
	})

	it('prefers a binding scoped to the feature and excludes one scoped elsewhere', () => {
		const result = resolve(
			features(claim('REQ-MED-001', [step('Given', 'x')])),
			[cs('Given', 'x', 'Plain.cs'), cs('Given', 'x', 'Scoped.cs', ['Attachments']), cs('Given', 'x', 'Other.cs', ['Comments'])],
			[],
		)

		assert.deepEqual(result.claims[0].files, ['Scoped.cs'])
		assert.deepEqual(result.unused.map((binding) => binding.file).sort(), ['Other.cs', 'Plain.cs'])
		assert.equal(result.ambiguous.size, 0)
	})

	it('lists a step two bindings match as ambiguous', () => {
		const result = resolve(features(claim('REQ-MED-001', [step('Given', 'x')])), [cs('Given', 'x', 'A.cs'), cs('Given', '{}', 'B.cs')], [])

		assert.deepEqual([...(result.ambiguous.get('Given x') ?? [])].sort(), ['A.cs', 'B.cs'])
	})

	it('lists unused definitions by file, then by what they match', () => {
		const result = resolve(features(), [cs('Given', 'b', 'Z.cs'), cs('Given', 'b', 'A.cs'), cs('Given', 'a', 'A.cs')], [])

		assert.deepEqual(result.unused.map((binding) => [binding.file, binding.pattern]), [['A.cs', 'a'], ['A.cs', 'b'], ['Z.cs', 'b']])
	})

	it('records an unbound step, and counts a binding a planned claim uses as used', () => {
		const result = resolve(
			features(claim('REQ-MED-001', [step('Given', 'missing')]), claim('REQ-MED-002', [step('Given', 'y')], { status: 'Planned' })),
			[cs('Given', 'y', 'Y.cs')],
			[],
		)

		assert.equal(result.claims[0].unbound[0].text, 'missing')
		assert.deepEqual(result.unused, [])
	})

	it('reports a pattern that does not compile, and uses the cache for a repeated step', () => {
		const result = resolve(
			features(claim('REQ-MED-001', [step('Given', 'x')]), claim('REQ-MED-002', [step('Given', 'x')])),
			[cs('Given', '^(unclosed$', 'Bad.cs'), cs('Given', 'x', 'A.cs')],
			[ts('Given', '{float}', 'bad.ts')],
		)

		assert.equal(result.problems.length, 2)
		assert.deepEqual(result.claims.map((each) => each.files), [['A.cs'], ['A.cs']])
	})
})

describe('collectBindings', () => {
	it('reads the feature files and both step trees, skipping build output', () => {
		const root = tree({
			[FEATURE]: 'Feature: Attachments\n\n@REQ-MED-001\nScenario: Upload\n  Given a file is attached\n',
			'tests/HpacSafety.Acceptance.Tests/ASteps.cs': '[Binding]\npublic class ASteps\n{\n\t[Given(@"a file is attached")]\n\tpublic void A() { }\n}\n',
			'tests/HpacSafety.Acceptance.Tests/obj/Ignored.cs': '[Given(@"never read")]\n',
			'tests/e2e/steps/a.steps.ts': 'const { Given } = createBdd()\nGiven(/^unused$/, async () => {})\n',
		})

		const result = collectBindings(root)

		assert.deepEqual(result.problems, [])
		assert.deepEqual(result.claims[0].bound, [{ keyword: 'Given', text: 'a file is attached', line: 5, files: ['tests/HpacSafety.Acceptance.Tests/ASteps.cs'], ambiguous: false }])
		assert.deepEqual(result.unused.map(display), ['/^unused$/'])
	})

	it('reports a problem reading a step file', () => {
		const root = tree({ [FEATURE]: 'Feature: A\n', 'tests/HpacSafety.Acceptance.Tests/B.cs': 'public class B\n{\n\t[Given(@"orphan")]\n}\n' })

		assert.match(collectBindings(root).problems.join('\n'), /outside a \[Binding\] class/)
	})

	it('reads an empty tree with no step directories', () => {
		assert.deepEqual(collectBindings(tree({ [FEATURE]: 'Feature: A\n' })).claims, [])
	})
})

/** The part of `@cucumber/cucumber-expressions` this test uses. */
interface CucumberExpressions {
	CucumberExpression: new (expression: string, registry: object) => { match: (text: string) => unknown }
	ParameterTypeRegistry: new () => object
}

describe('the real tree', () => {
	it('binds every built claim and reads every step definition without a problem', () => {
		const result = collectBindings(REPO)

		assert.deepEqual(result.problems, [])
		assert.deepEqual(
			result.claims.filter((claim) => claim.status === 'Covered' && claim.unbound.length > 0).map((claim) => claim.id),
			[],
		)
	})

	// The converter is checked against the official library wherever the e2e
	// dependencies are installed; CI's docs job, which installs none, skips it.
	const library = join(REPO, 'tests/e2e/node_modules/@cucumber/cucumber-expressions')
	it('matches every Cucumber Expression in the tree exactly as the official library does', { skip: !existsSync(library) }, () => {
		const require = createRequire(join(REPO, 'tests/e2e/package.json'))
		const { CucumberExpression, ParameterTypeRegistry } = require('@cucumber/cucumber-expressions') as CucumberExpressions
		const registry = new ParameterTypeRegistry()

		const result = collectBindings(REPO)
		const texts = [...new Set(result.claims.flatMap((claim) => claim.steps.map((step) => step.text)))]
		const expressions = new Set<string>()
		for (const file of readdirSync(join(REPO, 'tests/e2e/steps'))) {
			for (const binding of readPlaywrightBindings(file, readFileSync(join(REPO, 'tests/e2e/steps', file), 'utf8')).bindings) {
				if (typeof binding.pattern === 'string') expressions.add(binding.pattern)
			}
		}

		let compared = 0
		for (const expression of expressions) {
			const official = new CucumberExpression(expression, registry)
			const ours = cucumberToRegExp(expression)
			for (const text of texts) {
				assert.equal(ours.test(text), official.match(text) !== null, `${expression} vs "${text}"`)
				compared += 1
			}
		}
		assert.ok(compared > 0)
	})
})
