import { describe, it } from 'node:test'
import assert from 'node:assert/strict'
import { mkdtempSync, mkdirSync, symlinkSync, writeFileSync } from 'node:fs'
import { tmpdir } from 'node:os'
import { dirname, join } from 'node:path'

import { checkFile, expectationFor, isExempt, main, parseFrontmatter, type FrontmatterEntry } from '../../../tools/docs/check-frontmatter.ts'

/** Runs `main` with console output captured, restoring it afterwards even on failure. */
function runMain(root: string) {
	const output: { log: string[]; error: string[] } = { log: [], error: [] }
	const original = { log: console.log, error: console.error }
	console.log = (...args: unknown[]) => output.log.push(args.join(' '))
	console.error = (...args: unknown[]) => output.error.push(args.join(' '))
	try {
		return { code: main(root), output }
	} finally {
		console.log = original.log
		console.error = original.error
	}
}

/** A throwaway tree, one file per entry (nested paths create subdirectories). */
function tree(files: Record<string, string>): string {
	const root = mkdtempSync(join(tmpdir(), 'frontmatter-'))
	for (const [path, contents] of Object.entries(files)) {
		const full = join(root, path)
		mkdirSync(dirname(full), { recursive: true })
		writeFileSync(full, contents)
	}
	return root
}

const guide = (title = 'Glossary'): string => `---\ntitle: ${title}\ndescription: What the shared terms mean.\ntype: guide\n---\n\n# ${title}\n`

/** The entries of a block that parses; fails the test when it does not. */
function entriesOf(text: string): FrontmatterEntry[] {
	const parsed = parseFrontmatter(text)
	assert.ok(parsed.entries, `expected the block to parse, got: ${String(parsed.error)}`)
	return parsed.entries
}

/** The error of a block that does not parse; fails the test when it does. */
function errorOf(text: string): string {
	const parsed = parseFrontmatter(text)
	assert.ok(parsed.error, 'expected the block to be refused')
	return parsed.error
}

describe('parseFrontmatter', () => {
	it('reads top-level keys in order', () => {
		const entries = entriesOf('---\ntitle: A\ndescription: B\n---\n\nbody\n')

		assert.deepEqual(
			entries.map((entry) => entry.key),
			['title', 'description'],
		)
	})

	it('reports a file that does not open with a frontmatter block', () => {
		const error = errorOf('# Just a heading\n')

		assert.match(error, /must open with a --- line/)
	})

	it('reports a block that never closes', () => {
		const error = errorOf('---\ntitle: A\n\n# heading\n')

		assert.match(error, /never closes/)
	})

	it('rejects a tab, which YAML forbids in indentation', () => {
		const error = errorOf('---\nkeywords:\n\t- one\n---\n')

		assert.match(error, /contains a tab/)
	})

	it('ignores a blank line and a comment inside the block', () => {
		const entries = entriesOf('---\ntitle: A\n\n# a comment\ndescription: B\n---\n')

		assert.deepEqual(
			entries.map((entry) => entry.key),
			['title', 'description'],
		)
	})

	it('reports an indented line that follows no key', () => {
		const error = errorOf('---\n  - orphan\ntitle: A\n---\n')

		assert.match(error, /indented but follows no key/)
	})

	it('reports a line that is not a key: value pair', () => {
		const error = errorOf('---\ntitle: A\nnonsense\n---\n')

		assert.match(error, /is not a "key: value" pair/)
	})

	it('treats a nested map as a value rather than an empty key', () => {
		const entries = entriesOf('---\nmetadata:\n  version: 2\n---\n')

		assert.equal(entries.length, 1)
		assert.equal(entries[0].nested, true)
	})

	it('keeps a block list attached to the key above it', () => {
		const entries = entriesOf('---\ntitle: A\nkeywords:\n  - one\n  - two\n---\n')

		assert.equal(entries.length, 2)
		assert.equal(entries[1].key, 'keywords')
		assert.match(entries[1].value, /one/)
	})
})

describe('checkFile', () => {
	it('accepts a file carrying the core keys', () => {
		assert.deepEqual(checkFile('docs/glossary.md', guide()), [])
	})

	it('names every missing core key', () => {
		const problems = checkFile('docs/glossary.md', '---\ntitle: Glossary\n---\n')

		assert.equal(problems.length, 2)
		assert.match(problems[0], /missing "description"/)
		assert.match(problems[1], /missing "type"/)
	})

	it('rejects a key declared and left empty', () => {
		const problems = checkFile('docs/glossary.md', '---\ntitle: Glossary\ndescription:\ntype: guide\n---\n')

		assert.equal(problems.length, 1)
		assert.match(problems[0], /"description" is declared but empty/)
	})

	it('accepts a key whose value is a nested map rather than calling it empty', () => {
		const source = '---\ntitle: A\ndescription:\n  short: B\ntype: guide\n---\n'

		assert.deepEqual(checkFile('docs/glossary.md', source), [])
	})

	it('rejects a type outside the vocabulary', () => {
		const problems = checkFile('docs/glossary.md', '---\ntitle: A\ndescription: B\ntype: memo\n---\n')

		assert.equal(problems.length, 1)
		assert.match(problems[0], /"type: memo" is not one of/)
	})

	it('reports a key declared twice', () => {
		const problems = checkFile('docs/glossary.md', '---\ntitle: A\ntitle: B\ndescription: C\ntype: guide\n---\n')

		assert.ok(problems.some((problem) => /"title" is declared twice/.test(problem)))
	})

	it('requires an ADR to keep the keys the existing records carry', () => {
		const problems = checkFile('.spec/decisions/ADR-0001-a-decision.md', '---\ntitle: A\ndescription: B\ntype: adr\n---\n')

		assert.deepEqual(
			problems.map((problem) => /missing "([^"]+)"/.exec(problem)?.[1]),
			['status', 'date', 'decision-makers', 'keywords'],
		)
	})

	it('rejects a type that contradicts the file location', () => {
		const problems = checkFile('.spec/decisions/ADR-0001-a-decision.md', '---\ntitle: A\ndescription: B\ntype: guide\n---\n')

		assert.ok(problems.some((problem) => /contradicts its location/.test(problem)))
	})

	it('requires a spec page to name its area', () => {
		const problems = checkFile('.spec/features/media/README.md', '---\ntitle: A\ndescription: B\ntype: spec\n---\n')

		assert.equal(problems.length, 1)
		assert.match(problems[0], /missing "area"/)
	})

	it('requires a lesson to carry its date, issue, and status', () => {
		const problems = checkFile('.spec/lessons/0001-a-lesson.md', '---\ntitle: A\ndescription: B\ntype: lesson\n---\n')

		assert.equal(problems.length, 3)
	})

	it('holds a skill to the two keys its loader expects', () => {
		assert.deepEqual(checkFile('skills/a-skill/SKILL.md', '---\nname: a-skill\ndescription: B\n---\n'), [])
		assert.match(checkFile('skills/a-skill/SKILL.md', '---\nname: a-skill\n---\n')[0], /missing "description"/)
	})

	it('refuses to let a repository key leak into a file a third-party loader parses', () => {
		const problems = checkFile('agents/spec-reviewer.md', '---\nname: spec-reviewer\ndescription: B\nmodel: opus\neffort: high\ntype: agent\n---\n')

		assert.equal(problems.length, 1)
		assert.match(problems[0], /does not carry "type"/)
	})

	it('refuses a lesson status outside the closed set', () => {
		const lesson = (status: string): string => `---\ntitle: A\ndescription: B\ntype: lesson\ndate: 2026-09-30\nissue: 1\nstatus: ${status}\n---\n`

		assert.deepEqual(checkFile('.spec/lessons/0001-a-lesson.md', lesson('accepted')), [])
		assert.match(checkFile('.spec/lessons/0001-a-lesson.md', lesson('draft'))[0], /"status: draft" is not one of accepted, superseded/)
	})

	it('refuses a specification file left where it lived before the move to .spec/', () => {
		for (const path of ['docs/decisions/ADR-0999-late.md', 'docs/lessons/0099-late.md', 'features/media/README.md']) {
			assert.match(checkFile(path, '---\ntitle: A\ndescription: B\ntype: guide\n---\n')[0], /moved to \.spec\//)
		}
	})

	const agent = (extra: string): string => `---\nname: implementer\ndescription: B\n${extra}---\n`

	it('accepts an agent that declares its model and effort', () => {
		assert.deepEqual(checkFile('agents/implementer.md', agent('model: sonnet\neffort: medium\n')), [])
		assert.deepEqual(checkFile('agents/implementer.md', agent('model: claude-opus-5-5\neffort: 8000\n')), [])
	})

	it('requires an agent to declare its model and effort', () => {
		const problems = checkFile('agents/implementer.md', agent('effort: medium\n'))

		assert.equal(problems.length, 1)
		assert.match(problems[0], /missing "model"/)
	})

	it('accepts the optional keys Claude Code reads on an agent', () => {
		assert.deepEqual(checkFile('agents/implementer.md', agent('model: sonnet\neffort: medium\ntools: Read, Grep\nmaxTurns: 20\n')), [])
	})

	it('refuses an agent key Claude Code does not read', () => {
		const problems = checkFile('agents/implementer.md', agent('model: sonnet\neffort: medium\ncolour: blue\n'))

		assert.equal(problems.length, 1)
		assert.match(problems[0], /"colour" is not a key an agent carries/)
	})

	it('refuses a model or effort its loader would reject', () => {
		const problems = checkFile('agents/implementer.md', agent('model: gpt-5\neffort: silly\n'))

		assert.equal(problems.length, 2)
		assert.match(problems[0], /"model: gpt-5"/)
		assert.match(problems[1], /"effort: silly"/)
	})
})

describe('expectationFor', () => {
	it('resolves a skill and an agent from the path', () => {
		assert.equal(expectationFor('skills/a-skill/SKILL.md').vendor?.kind, 'skill')
		assert.equal(expectationFor('agents/reviewer.md').vendor?.kind, 'agent')
	})

	it('assigns adr and lesson from the path', () => {
		assert.equal(expectationFor('.spec/decisions/ADR-0042-a-decision.md').type, 'adr')
		assert.equal(expectationFor('.spec/lessons/0001-a-lesson.md').type, 'lesson')
	})

	it('leaves an ordinary page type to its author', () => {
		assert.deepEqual(expectationFor('docs/glossary.md'), {})
	})
})

describe('isExempt', () => {
	it('exempts the runtime prompts and nothing else', () => {
		assert.equal(isExempt('src/HpacSafety.Worker/Prompts/summarize-anonymize.v1.md'), true)
		assert.equal(isExempt('locales/translation-prompt.v1.md'), true)
		assert.equal(isExempt('src/HpacSafety.Worker/README.md'), false)
		assert.equal(isExempt('locales/README.md'), false)
	})
})

describe('main', () => {
	it('passes a tree where every file declares itself', () => {
		const { code, output } = runMain(tree({ 'docs/glossary.md': guide(), 'README.md': guide('HPAC Safety') }))

		assert.equal(code, 0)
		assert.match(output.log.join('\n'), /2 markdown file\(s\) checked/)
	})

	it('fails and annotates the offending file', () => {
		const { code, output } = runMain(tree({ 'docs/glossary.md': '# No frontmatter\n' }))

		assert.equal(code, 1)
		assert.match(output.error.join('\n'), /::error file=docs\/glossary\.md::/)
	})

	it('skips the exempt runtime prompt', () => {
		const { code, output } = runMain(tree({ 'src/HpacSafety.Worker/Prompts/summarize-anonymize.v1.md': '# A prompt\n' }))

		assert.equal(code, 0)
		assert.match(output.log.join('\n'), /0 markdown file\(s\) checked/)
	})

	it('walks the tree when there is no git repository to list', () => {
		const root = tree({ 'docs/a.md': guide('A'), 'docs/nested/b.md': guide('B'), 'docs/notes.txt': 'ignored' })

		const { code, output } = runMain(root)

		assert.equal(code, 0)
		assert.match(output.log.join('\n'), /2 markdown file\(s\) checked/)
	})

	it('skips a symlink, which resolves to a file checked on its own path', () => {
		const root = tree({ 'AGENTS.md': guide('HPAC Safety agent instructions') })
		symlinkSync('AGENTS.md', join(root, 'CLAUDE.md'))

		const { code, output } = runMain(root)

		assert.equal(code, 0)
		assert.match(output.log.join('\n'), /1 markdown file\(s\) checked/)
	})
})
