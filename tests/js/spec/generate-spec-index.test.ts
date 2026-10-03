import { describe, it } from 'node:test'
import assert from 'node:assert/strict'
import { mkdtempSync, mkdirSync, readFileSync, rmSync, writeFileSync } from 'node:fs'
import { tmpdir } from 'node:os'
import { dirname, join } from 'node:path'

import { collect, main, remedy, render } from '../../../tools/spec/generate-spec-index.ts'
import { CONSTRAINT_PAGES, SPEC_INDEX } from '../../../tools/spec/spec-paths.ts'

const page = (title: string, extra = ''): string => `---\ntitle: ${title}\ndescription: About ${title.toLowerCase()}.\ntype: spec\narea: x\n---\n\n# ${title}\n${extra}`
const adr = (number: string, title: string, status = 'accepted'): string =>
	`---\ntitle: ${title}\ndescription: D.\ntype: adr\nstatus: ${status}\ndate: 2026-09-${number.slice(2)}\ndecision-makers: Someone\nkeywords: a\n---\n\n# ADR-${number} — ${title}\n`
const lesson = (title: string, body: string): string => `---\ntitle: ${title}\ndescription: What ${title.toLowerCase()} cost.\ntype: lesson\ndate: 2026-09-30\nissue: 704\nstatus: accepted\n---\n\n# Lesson\n\n${body}`

/** A throwaway specification: two areas, the constraint pages, two ADRs, two lessons. */
function specification(): string {
	const root = mkdtempSync(join(tmpdir(), 'spec-index-'))
	const files: Record<string, string> = {
		'.spec/features/media/media.feature': 'Feature: Media\n\n@REQ-MED-001\nScenario: One\n  Given a\n\n@REQ-MED-002\n@ui\n@ignore\nScenario: Two\n  Given b\n',
		'.spec/features/media/README.md': page('Attachments'),
		'.spec/features/comments/comments.feature': 'Feature: Comments\n\n@REQ-COM-001\n@ui\nScenario: One\n  Given a\n',
		'.spec/features/comments/README.md': page('Comments | threads'),
		'.spec/decisions/README.md': '---\ntitle: ADRs\ndescription: D.\ntype: guide\n---\n',
		'.spec/decisions/ADR-0001-first.md': adr('0001', 'First'),
		'.spec/decisions/ADR-0002-second.md': adr('0002', 'Second', 'superseded'),
		'.spec/lessons/README.md': '---\ntitle: Lessons\ndescription: D.\ntype: guide\n---\n',
		'.spec/lessons/0001-a-product-gap.md': lesson('A product gap', '## Scenario\n\n`REQ-MED-001` now proves it.\n\n## Skill\n\nNone — the claim is the remedy.\n'),
		'.spec/lessons/0002-a-process-gap.md': lesson('A process gap', '## Scenario\n\nNo scenario can.\n\n## Skill\n\n`deliver-change` now says so.\n'),
	}
	for (const path of CONSTRAINT_PAGES) files[path] = page('Overview', '\n- **CON-SO-001** A rule. *Verified by: REQ-MED-001.*\n')
	for (const [path, text] of Object.entries(files)) {
		mkdirSync(dirname(join(root, path)), { recursive: true })
		writeFileSync(join(root, path), text)
	}
	return root
}

/** Runs `main` with console output captured, restoring it afterwards even on failure. */
function runMain(root: string, options?: { check?: boolean }): { code: number; output: { log: string[]; error: string[] } } {
	const output: { log: string[]; error: string[] } = { log: [], error: [] }
	const original = { log: console.log, error: console.error }
	console.log = (...args: unknown[]) => output.log.push(args.join(' '))
	console.error = (...args: unknown[]) => output.error.push(args.join(' '))
	try {
		return { code: main(root, options), output }
	} finally {
		console.log = original.log
		console.error = original.error
	}
}

describe('remedy', () => {
	it('names the claims under Scenario and the skills under Skill', () => {
		assert.equal(remedy('## Scenario\n\nREQ-QB-001 and REQ-QB-002.\n\n## Skill\n\n`deliver-hpac-change` and `deliver-change`.\n'), 'REQ-QB-001, REQ-QB-002, `deliver-hpac-change`, `deliver-change`')
	})

	it('says none when a lesson names neither', () => {
		assert.equal(remedy('## Scenario\n\nNone.\n\n## Skill\n\nNone.\n'), 'none')
	})
})

describe('render', () => {
	const index = render(collect(specification()))

	it('lists each area with its counts and supporting page, in directory order', () => {
		const comments = index.indexOf('[Comments \\| threads](features/comments/comments.feature)')
		const media = index.indexOf('[Attachments](features/media/media.feature) | `REQ-MED` | 2 | 1 | 1 | [README](features/media/README.md)')

		assert.ok(comments > -1, 'a pipe in a title is escaped')
		assert.ok(media > comments, 'areas sort by directory name')
	})

	it('lists every constraint page with its count', () => {
		assert.match(index, /\| \[Overview\]\(system-overview\.md\) \| `CON-SO` \| 1 \|/)
	})

	it('lists decisions newest first, with their status', () => {
		assert.ok(index.indexOf('[0002](decisions/ADR-0002-second.md) | Second | superseded') < index.indexOf('[0001](decisions/ADR-0001-first.md) | First | accepted'))
	})

	it('lists lessons newest first, with what each changed upstream', () => {
		assert.match(index, /\[0002\]\(lessons\/0002-a-process-gap\.md\) \| A process gap \| What a process gap cost\. \| `deliver-change` \| #704/)
		assert.match(index, /\[0001\]\(lessons\/0001-a-product-gap\.md\) \| A product gap \| .* \| REQ-MED-001 \|/)
	})

	it('declares itself a generated readme', () => {
		assert.match(index, /^---\ntitle: Specification index\n.*\ntype: readme\n---\n/s)
		assert.match(index, /Generated file — do not edit by hand/)
	})
})

describe('render, at the edges', () => {
	it('falls back to the directory name for an area with no README and no scenarios yet', () => {
		const root = specification()
		mkdirSync(join(root, '.spec/features/new-area'), { recursive: true })
		writeFileSync(join(root, '.spec/features/new-area/new-area.feature'), 'Feature: New\n')

		const index = render(collect(root))

		assert.match(index, /\| \[new-area\]\(features\/new-area\/new-area\.feature\) \| `` \| 0 \| 0 \| 0 \| — \|/)
	})

	it('falls back to the path for a constraint page with no title and no constraints yet', () => {
		const root = specification()
		writeFileSync(join(root, CONSTRAINT_PAGES[1]), '# Untitled\n')

		const page = CONSTRAINT_PAGES[1]

		assert.ok(render(collect(root)).includes(`| [${page}](${page.replace('.spec/', '')}) | \`\` | 0 |  |`))
	})

	it('lists each lesson\'s kind', () => {
		const root = specification()
		writeFileSync(join(root, '.spec/lessons/0003-an-incident.md'), lesson('An incident', '## Symptom\n\nIt broke.\n').replace('status: accepted', 'status: accepted\nkind: incident'))

		assert.match(render(collect(root)), /\[0003\]\(lessons\/0003-an-incident\.md\) \| An incident \| .* \| accepted \| incident \|$/m)
	})

	it('says there are no conventions yet when the directory is missing or holds only its README', () => {
		const root = specification()

		assert.match(render(collect(root)), /## Conventions\n[\s\S]*None yet\./)
	})

	it('lists conventions newest first', () => {
		const root = specification()
		mkdirSync(join(root, '.spec/conventions'), { recursive: true })
		const convention = (number: string, title: string): string => `---\ntitle: ${title}\ndescription: D.\ntype: convention\nstatus: accepted\ndate: 2026-10-0${number.slice(2)}\n---\n\n# CONV-${number} — ${title}\n`
		writeFileSync(join(root, '.spec/conventions/README.md'), '---\ntitle: Conventions\ndescription: D.\ntype: guide\n---\n')
		writeFileSync(join(root, '.spec/conventions/CONV-001-first.md'), convention('001', 'First'))
		writeFileSync(join(root, '.spec/conventions/CONV-002-second.md'), convention('002', 'Second'))

		const index = render(collect(root))

		assert.ok(index.indexOf('| [CONV-002](conventions/CONV-002-second.md) | Second | accepted | 2026-10-02 |') > -1)
		assert.ok(index.indexOf('CONV-002') < index.indexOf('CONV-001'))
	})

	it('shows an issue that is not a number as written', () => {
		const root = specification()
		writeFileSync(join(root, '.spec/lessons/0003-no-issue.md'), lesson('No issue', '## Scenario\n\nNone.\n').replace('issue: 704', 'issue: none'))

		assert.match(render(collect(root)), /\[0003\]\(lessons\/0003-no-issue\.md\) \| No issue \| .* \| none \| none \|/)
	})
})

describe('main', () => {
	it('fails --check when the index has never been generated', () => {
		const root = specification()
		rmSync(join(root, SPEC_INDEX), { force: true })

		assert.equal(runMain(root, { check: true }).code, 1)
	})

	it('adds the totals to the job summary when one is set', () => {
		const root = specification()
		const summary = join(root, 'summary.md')
		process.env.GITHUB_STEP_SUMMARY = summary
		try {
			runMain(root)
		} finally {
			delete process.env.GITHUB_STEP_SUMMARY
		}

		assert.match(readFileSync(summary, 'utf8'), /\*\*Specification:\*\* 2 areas \(3 scenarios\)/)
	})

	it('writes the index, then passes --check', () => {
		const root = specification()

		assert.equal(runMain(root).code, 0)
		assert.match(readFileSync(join(root, SPEC_INDEX), 'utf8'), /# Specification index/)
		assert.equal(runMain(root, { check: true }).code, 0)
	})

	it('fails --check on a stale index and names the fix', () => {
		const root = specification()
		runMain(root)
		writeFileSync(join(root, '.spec/decisions/ADR-0003-third.md'), adr('0003', 'Third'))

		const { code, output } = runMain(root, { check: true })

		assert.equal(code, 1)
		assert.match(output.error.join('\n'), /::error file=\.spec\/README\.md::Out of date/)
	})
})
