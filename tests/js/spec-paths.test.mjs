import { describe, it } from 'node:test'
import assert from 'node:assert/strict'
import { readFileSync } from 'node:fs'
import { dirname, join } from 'node:path'
import { fileURLToPath } from 'node:url'

import { CONSTRAINT_PAGES, SPEC_INDEX, SPEC_ROOT, TRACEABILITY } from '../../tools/spec-paths.mjs'

const REPO = join(dirname(fileURLToPath(import.meta.url)), '..', '..')
const read = (path) => readFileSync(join(REPO, path), 'utf8')

// A shell hook or a workflow cannot import tools/spec-paths.mjs, so each one
// names the paths itself. These tie every copy to the module (ADR-0183).
describe('the places that cannot import the specification paths', () => {
	it('every constraint page and generated file sits under the specification root', () => {
		for (const path of [...CONSTRAINT_PAGES, TRACEABILITY, SPEC_INDEX]) assert.ok(path.startsWith(`${SPEC_ROOT}/`), path)
	})

	it('traceability.yml is triggered by the whole specification and both generators', () => {
		const workflow = read('.github/workflows/traceability.yml')

		assert.match(workflow, new RegExp(`^ {6}- ${SPEC_ROOT.replace('.', '\\.')}/\\*\\*$`, 'm'))
		for (const tool of ['tools/traceability.mjs', 'tools/spec-index.mjs', 'tools/spec-paths.mjs']) assert.ok(workflow.includes(`- ${tool}`), tool)
	})

	for (const path of ['.githooks/post-merge', '.githooks/post-rewrite', '.github/workflows/traceability.yml', 'tools/ci-local.sh']) {
		it(`${path} regenerates both generated files`, () => {
			const text = read(path)

			assert.ok(text.includes(TRACEABILITY), `names ${TRACEABILITY}`)
			assert.ok(text.includes(SPEC_INDEX), `names ${SPEC_INDEX}`)
		})
	}

	it('ci.yml checks both generated files and every link', () => {
		const ci = read('.github/workflows/ci.yml')

		assert.ok(ci.includes(`git diff --exit-code --stat -- ${TRACEABILITY}`))
		assert.ok(ci.includes('node tools/spec-index.mjs --check'))
		assert.ok(ci.includes('node tools/check-links.mjs'))
	})

	it('.gitattributes keeps the checked-out side of both generated files on a conflict', () => {
		const attributes = read('.gitattributes')

		assert.match(attributes, new RegExp(`^${TRACEABILITY.replace(/\./g, '\\.')} merge=ours$`, 'm'))
		assert.match(attributes, new RegExp(`^${SPEC_INDEX.replace(/\./g, '\\.')} merge=ours$`, 'm'))
	})
})
