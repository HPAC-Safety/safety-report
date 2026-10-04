import { describe, it } from 'node:test'
import assert from 'node:assert/strict'
import { mkdirSync, mkdtempSync, readFileSync, writeFileSync } from 'node:fs'
import { tmpdir } from 'node:os'
import { dirname, join } from 'node:path'
import { fileURLToPath } from 'node:url'

import { cells, checkGlossary, featureLines, main, matcher, paragraphs, readGlossary, readmeLines, scan, stripLiterals } from '../../../tools/spec/check-glossary.ts'
import { GLOSSARY } from '../../../tools/spec/spec-paths.ts'

const REPO = join(dirname(fileURLToPath(import.meta.url)), '..', '..', '..')

const GLOSSARY_PAGE = [
	'---',
	'title: Glossary',
	'---',
	'',
	'| Term | Definition | Banned in scenarios | Exempt areas |',
	'|---|---|---|---|',
	'| **Safety Officer** | The role. | `/\\b[Ss]afety officers?\\b/` | — |',
	'| **Question** | One item of the form. | `/\\bfields?\\b/i` | `typeform-question-import-export` |',
	'| **refused** | The one verb. | `/\\breject(?:s\\|ed)?\\b/i` | `ai-anonymization` |',
	'| **Report list** | The reviewers\' list. | `review queue`, `dashboard` | — |',
	'',
	'| Other table | Without a banned column |',
	'|---|---|',
	'| field | not read |',
	'',
].join('\n')

/** A repository with the glossary above and one area per entry of `areas`. */
function tree(areas: Record<string, { feature?: string; readme?: string }>, glossary = GLOSSARY_PAGE): string {
	const root = mkdtempSync(join(tmpdir(), 'check-glossary-'))
	mkdirSync(join(root, dirname(GLOSSARY)), { recursive: true })
	writeFileSync(join(root, GLOSSARY), glossary)
	for (const [area, { feature, readme }] of Object.entries(areas)) {
		const directory = join(root, '.spec', 'features', area)
		mkdirSync(directory, { recursive: true })
		if (feature !== undefined) writeFileSync(join(directory, `${area}.feature`), feature)
		if (readme !== undefined) writeFileSync(join(directory, 'README.md'), readme)
	}
	mkdirSync(join(root, '.spec', 'features'), { recursive: true })
	writeFileSync(join(root, '.spec', 'features', 'README.md'), '# Not an area\n\nA safety officer here is not read.\n')
	return root
}

describe('cells and matcher', () => {
	it('splits a row on bars, keeping an escaped bar inside a cell', () => {
		assert.deepEqual(cells('| a | `x\\|y` | c |'), ['a', '`x|y`', 'c'])
	})

	it('matches a plain phrase as whole words, ignoring case and spacing', () => {
		const pattern = matcher('review queue')
		assert.equal('The Review  Queue opens'.match(pattern)?.[0], 'Review  Queue')
		assert.equal('the review queues'.match(pattern), null)
	})

	it('reads /pattern/flags as a regular expression, case as written', () => {
		const pattern = matcher('/\\badministrators?\\b/')
		assert.equal('an administrator'.match(pattern)?.[0], 'administrator')
		assert.equal('an Administrator'.match(pattern), null)
	})
})

describe('readGlossary', () => {
	it('reads every row of a table with a banned column, and nothing from other tables', () => {
		const { entries, problems } = readGlossary(GLOSSARY_PAGE)

		assert.deepEqual(problems, [])
		assert.deepEqual(entries.map((entry) => entry.term), ['Safety Officer', 'Question', 'refused', 'Report list'])
		assert.deepEqual(entries[1].exempt, ['typeform-question-import-export'])
		assert.equal(entries[3].banned.length, 2)
		assert.ok(entries[2].banned[0].test('the import is rejected'))
	})

	it('reports a pattern that does not compile, and a page that bans nothing', () => {
		const broken = readGlossary('| Term | Definition | Banned in scenarios | Exempt areas |\n|---|---|---|---|\n| **X** | Y. | `/(/` | — |\n')

		assert.ok(broken.problems.some((problem) => /`\/\(\/` is not a valid pattern/.test(problem)))
		assert.ok(broken.problems.some((problem) => /column bans anything/.test(problem)))
	})

	it('reads the real glossary with no problem, and bans in every section it lists them', () => {
		const { entries, problems } = readGlossary(readFileSync(join(REPO, GLOSSARY), 'utf8'))

		assert.deepEqual(problems, [])
		assert.ok(entries.length >= 70, `${entries.length} entries`)
		for (const term of ['Safety Officer', 'Question', 'Choice', 'Deleted', 'refused', 'Reviewer']) {
			assert.ok(entries.find((entry) => entry.term === term)?.banned.length, term)
		}
	})
})

describe('what the lint reads', () => {
	it('skips quoted literals, code spans, and placeholders', () => {
		assert.doesNotMatch(stripLiterals('the "review queue" title, `field`, and <field>'), /queue|field/)
	})

	it('reads a feature file\'s steps, names, and descriptions, but not tags, comments, tables, or doc strings', () => {
		const source = [
			'@REQ-X-001',
			'Feature: A field',
			'A description with a field.',
			'# a comment with a field',
			'Scenario: A field',
			'  Given a field',
			'  """',
			'  a field in a doc string',
			'  """',
			'  | field |',
		].join('\n')

		assert.deepEqual(featureLines(source).map(([line]) => line), [2, 3, 5, 6])
	})

	it('reads an Examples table\'s body rows, which steps read through placeholders, but not its header or a data table', () => {
		const source = [
			'Scenario Outline: A role',
			'  Given a <role>',
			'  | a data table row |',
			'',
			'Examples:',
			'  | role |',
			'  | safety officer |',
		].join('\n')

		assert.deepEqual(featureLines(source).map(([line]) => line), [1, 2, 5, 7])
	})

	it('reads a README\'s prose, but not its frontmatter, fences, comments, or link targets', () => {
		const source = ['---', 'title: field', '---', 'A field.', '```', 'field', '```', '<!-- field -->', 'See [the list](field.md).'].join('\n')
		const read = readmeLines(source)

		assert.deepEqual(read.map(([line]) => line), [4, 8, 9])
		assert.doesNotMatch(read.slice(1).map(([, text]) => text).join(' '), /field/)
	})

	it('joins a wrapped paragraph, so a phrase broken across lines still matches on the line it starts', () => {
		const lines: Array<[number, string]> = [[1, 'Only a Safety'], [2, 'officer may do it.'], [4, '- a list item'], [5, '  wrapped on'], [6, '| a | row |']]
		const joined = paragraphs(lines)

		assert.deepEqual(joined.map((paragraph) => paragraph.text), ['Only a Safety officer may do it.', '- a list item   wrapped on', '| a | row |'])
		const [found] = scan('README.md', 'area', lines, readGlossary(GLOSSARY_PAGE).entries, true)
		assert.deepEqual(found, { file: 'README.md', line: 1, found: 'Safety officer', term: 'Safety Officer' })
	})
})

describe('checkGlossary', () => {
	it('passes scenarios and READMEs that use only the glossary\'s words', () => {
		const root = tree({
			comments: {
				feature: 'Feature: Comments\n\nScenario: A Safety Officer hides a comment\n  Given the "review queue" page\n  Then the request is refused\n',
				readme: '---\ntitle: Comments\n---\n\nA Safety Officer opens the report list.\n',
			},
		})

		assert.deepEqual(checkGlossary(root), { violations: [], problems: [] })
		assert.equal(main(root, () => {}), 0)
	})

	it('fails a banned synonym in a step, a name, and a README, with its line and the glossary\'s word', () => {
		const root = tree({
			comments: {
				feature: 'Feature: Comments\n\nScenario: The dashboard\n  Given a safety officer\n  Then the attempt is rejected\n',
				readme: '---\ntitle: Comments\n---\n\nThe review queue.\n',
			},
		})
		const { violations } = checkGlossary(root)

		assert.deepEqual(violations.map(({ file, line, found, term }) => `${file}:${line} ${found} -> ${term}`), [
			'.spec/features/comments/comments.feature:3 dashboard -> Report list',
			'.spec/features/comments/comments.feature:4 safety officer -> Safety Officer',
			'.spec/features/comments/comments.feature:5 rejected -> refused',
			'.spec/features/comments/README.md:5 review queue -> Report list',
		])
		const output: string[] = []
		assert.equal(main(root, (line) => output.push(line)), 1)
		assert.ok(output.some((line) => line.includes('::error file=.spec/features/comments/comments.feature,line=4::"safety officer" is a banned synonym; the glossary\'s word is "Safety Officer"')))
	})

	it('honours an exempt area: Typeform keeps its field, and the Worker rejects a response', () => {
		const root = tree({
			'typeform-question-import-export': { feature: 'Feature: Typeform\n\nScenario: A field type is imported\n  Given a Typeform field\n' },
			'ai-anonymization': { feature: 'Feature: AI\n\nScenario: A bad response\n  Then the summary is rejected\n' },
			'report-form': { feature: 'Feature: Report form\n\nScenario: A field\n  Then the summary is rejected\n' },
		})

		assert.deepEqual(checkGlossary(root).violations.map(({ file, found }) => `${file} ${found}`), [
			'.spec/features/report-form/report-form.feature field',
			'.spec/features/report-form/report-form.feature rejected',
		])
	})

	it('fails when the glossary is missing', () => {
		const root = tree({})
		const empty = mkdtempSync(join(tmpdir(), 'check-glossary-none-'))
		mkdirSync(join(empty, '.spec', 'features'), { recursive: true })

		assert.deepEqual(checkGlossary(empty).problems, [`${GLOSSARY} is missing`])
		assert.deepEqual(checkGlossary(root).problems, [])
	})

	it('finds no banned synonym in this repository', () => {
		assert.deepEqual(checkGlossary(REPO).violations, [])
	})
})

// The real glossary's patterns, sample by sample: what each must refuse, and
// the narrow carve-outs each must leave alone (CONV-003).
describe('the real glossary', () => {
	const { entries } = readGlossary(readFileSync(join(REPO, GLOSSARY), 'utf8'))
	const found = (text: string, area = 'report-form'): string[] => scan('x.feature', area, [[1, stripLiterals(text)]], entries).map((violation) => violation.found)

	const mustFlag: Array<[string, string]> = [
		['Given a signed-in Administrator opens it', 'signed-in '],
		['Then a long-text answer is shown', 'long-text'],
		['Given a Safety Officer is signed in', 'Safety Officer is signed in'],
		['Given a member is signed in', 'member is signed in'],
		['When the safety officer opens it', 'safety officer'],
		['Given a SafetyOfficer', 'SafetyOfficer'],
		['When an administrator saves it', 'administrator'],
		['Then the report is soft-deleted', 'soft-deleted'],
		['Then the question is stamped as deleted', 'stamped'],
		['Then the report is archived', 'archived'],
		['Then it appears in the review queue', 'queue'],
		['When they type in the field', 'field'],
		['Then the attempt is rejected', 'rejected'],
		['When a visitor logs in', 'logs in'],
		['Then a photo is shown', 'photo'],
		['Fixing a picker option', 'option'],
		['an authorized reviewer', 'authorized reviewer'],
		['a Safety Officer or an Administrator', 'Safety Officer or an Administrator'],
		['a reporter-added choice', 'reporter-added choice'],
		['the public never sees it', 'the public '],
		['the submitter', 'submitter'],
		['records the subject', 'subject'],
		['the report goes live', 'goes live'],
		['a bilingual pair', 'bilingual pair'],
		['Concurrent workers claim it', 'workers'],
	]
	for (const [text, banned] of mustFlag) {
		it(`flags "${banned}" in "${text}"`, () => {
			assert.ok(found(text).some((match) => match === banned), found(text).join(', ') || 'nothing found')
		})
	}

	const mustPass = [
		'Then the summary is visible to the public',
		'Then the sign-in page shows a third-party sign-in option',
		'When the visitor activates the Admin menu',
		'Then it joins to no user table and no user record',
		'Then the summary names the landing field',
		'Then the bailout field is named',
		'Given a zip archive',
		'| reports/pilot/photo.jpg | photo.jpg |',
		'If production evidence shows it matters',
		'Then the token subject is recorded',
		'Given a Safety Officer is on the admin site',
		'Then the response carries X-Content-Type-Options: nosniff',
		'Then every optional question is skipped',
		'Then the "review queue" title is shown',
		'Then the paragraph answer is shown',
	]
	for (const text of mustPass) {
		it(`passes "${text}"`, () => {
			assert.deepEqual(found(text), [])
		})
	}

	it('lets Typeform keep its field and its own type names, and the Worker reject a response', () => {
		assert.deepEqual(found('Given a Typeform long text field', 'typeform-question-import-export'), [])
		assert.deepEqual(found('Then the summary is rejected', 'ai-anonymization'), [])
		assert.deepEqual(found('Then the summary is rejected', 'report-form'), ['rejected'])
	})
})
