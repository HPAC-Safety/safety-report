import { describe, it } from 'node:test'
import assert from 'node:assert/strict'
import { readFileSync } from 'node:fs'

import { FIRST_PR, judge, main, renderedFiles } from '../../tools/pr-screenshots.mjs'

const SHOT = 'https://raw.githubusercontent.com/owner/repo/0123456789abcdef0123456789abcdef01234567/docs/screenshots/form/after-form.png'
const COMPONENT = 'src/web/src/components/Form.tsx'

/** Runs `main` with console output captured, restoring it afterwards even on failure. */
function runMain(input) {
	const output = { log: [], error: [] }
	const original = { log: console.log, error: console.error }
	console.log = (...args) => output.log.push(args.join(' '))
	console.error = (...args) => output.error.push(args.join(' '))
	try {
		return { code: main(input), output }
	} finally {
		console.log = original.log
		console.error = original.error
	}
}

describe('renderedFiles', () => {
	it('keeps components and stylesheets under the web source tree', () => {
		assert.deepEqual(renderedFiles([COMPONENT, 'src/web/src/index.css']), [COMPONENT, 'src/web/src/index.css'])
	})

	it('drops tests, non-rendering files, and files outside the web source tree', () => {
		const changed = [
			'src/web/src/components/Form.test.tsx',
			'src/web/src/lib/format.ts',
			'src/web/package.json',
			'src/HpacSafety.Api/Program.cs',
			'docs/screenshots/form/after-form.png',
		]

		assert.deepEqual(renderedFiles(changed), [])
	})
})

describe('judge', () => {
	it('passes a pull request that changes no rendered web file, whatever its body', () => {
		assert.equal(judge({ changed: ['src/HpacSafety.Api/Program.cs', 'src/web/src/lib/format.ts'], body: '' }).ok, true)
	})

	it('passes a rendered change whose body links a pinned screenshot', () => {
		const verdict = judge({ changed: [COMPONENT], body: `## Screenshots\n\n![after](${SHOT})\n` })

		assert.equal(verdict.ok, true)
		assert.match(verdict.note, /Screenshots linked: 1/)
	})

	it('fails a rendered change whose body has neither a screenshot nor an exemption', () => {
		const verdict = judge({ changed: [COMPONENT, 'src/web/src/lib/format.ts'], body: '## What changed\n\nA thing.\n' })

		assert.equal(verdict.ok, false)
		assert.deepEqual(verdict.rendered, [COMPONENT])
		assert.deepEqual(verdict.problems, [])
	})

	it('does not count a screenshot linked by a page URL or an unpinned branch', () => {
		const body = [
			'https://github.com/owner/repo/blob/main/docs/screenshots/form/after-form.png',
			'https://raw.githubusercontent.com/owner/repo/main/docs/screenshots/form/after-form.png',
			'docs/screenshots/form/after-form.png',
		].join('\n')

		assert.equal(judge({ changed: [COMPONENT], body }).ok, false)
	})

	it('passes a rendered change that says why it needs no screenshot', () => {
		const verdict = judge({ changed: [COMPONENT], body: 'No screenshot needed: renamed a prop, nothing on screen moved\n' })

		assert.equal(verdict.ok, true)
		assert.match(verdict.note, /renamed a prop/)
	})

	it('refuses an exemption with no real reason', () => {
		for (const body of ['No screenshot needed:\n', 'No screenshot needed: refactor\n']) {
			const verdict = judge({ changed: [COMPONENT], body })

			assert.equal(verdict.ok, false)
			assert.match(verdict.problems[0], /is not a reason/)
		}
	})

	it('ignores a screenshot or an exemption left inside an HTML comment', () => {
		const body = `<!--\n![after](${SHOT})\nNo screenshot needed: renamed a prop, nothing on screen moved\n-->\n`

		assert.equal(judge({ changed: [COMPONENT], body }).ok, false)
	})

	it('passes a pull request opened before the rule, and applies it from the first one after', () => {
		assert.equal(judge({ changed: [COMPONENT], body: '', number: FIRST_PR - 1 }).ok, true)
		assert.equal(judge({ changed: [COMPONENT], body: '', number: FIRST_PR }).ok, false)
	})
})

describe('main', () => {
	it('notes a pass and exits 0', () => {
		const { code, output } = runMain({ changed: [COMPONENT], body: `![after](${SHOT})` })

		assert.equal(code, 0)
		assert.match(output.log.join('\n'), /::notice::Screenshots linked: 1/)
	})

	it('names the rendered files and the exemption line, and exits 1', () => {
		const { code, output } = runMain({ changed: [COMPONENT], body: '' })
		const errors = output.error.join('\n')

		assert.equal(code, 1)
		assert.match(errors, /::error::This pull request changes a rendered web file and shows no screenshot/)
		assert.match(errors, /src\/web\/src\/components\/Form\.tsx/)
		assert.match(errors, /No screenshot needed: </)
	})

	it('annotates why an exemption was refused', () => {
		const { code, output } = runMain({ changed: [COMPONENT], body: 'No screenshot needed: none\n' })

		assert.equal(code, 1)
		assert.match(output.error.join('\n'), /::error::"none" is not a reason/)
	})
})

// The template is where an author writes the body, so it asks for screenshots
// and shows the exemption line (issue #530).
describe('pull request template', () => {
	const template = readFileSync(new URL('../../.github/pull_request_template.md', import.meta.url), 'utf8')

	it('has a Screenshots section that shows the exemption line', () => {
		assert.match(template, /^## Screenshots$/m)
		assert.match(template, /No screenshot needed: </)
	})

	it('is not itself read as a screenshot or an exemption when left in a body', () => {
		assert.equal(judge({ changed: [COMPONENT], body: template }).ok, false)
	})
})
