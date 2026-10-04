import { describe, it } from 'node:test'
import assert from 'node:assert/strict'
import { readFileSync } from 'node:fs'
import { dirname, join } from 'node:path'
import { fileURLToPath } from 'node:url'

import { GENERATORS } from '../../../tools/spec/regenerate-spec-docs.ts'
import { CLAIMS, CLAIMS_SCHEMA, CONSTRAINT_PAGES, PLAYWRIGHT_STEPS, REQNROLL_STEPS, SPEC_INDEX, SPEC_ROOT, TRACEABILITY } from '../../../tools/spec/spec-paths.ts'

const REPO = join(dirname(fileURLToPath(import.meta.url)), '..', '..', '..')
const read = (path: string): string => readFileSync(join(REPO, path), 'utf8')

const GENERATED = [CLAIMS, TRACEABILITY, SPEC_INDEX]

// A shell hook or a workflow cannot import tools/spec/spec-paths.ts, so each one
// names the paths itself. These tie every copy to the module (ADR-0183).
describe('the places that cannot import the specification paths', () => {
	it('every constraint page and generated file sits under the specification root', () => {
		for (const path of [...CONSTRAINT_PAGES, ...GENERATED, CLAIMS_SCHEMA]) assert.ok(path.startsWith(`${SPEC_ROOT}/`), path)
	})

	it('traceability.yml is triggered by the whole specification and every generator module', () => {
		const workflow = read('.github/workflows/traceability.yml')

		assert.match(workflow, new RegExp(`^ {6}- ${SPEC_ROOT.replace('.', '\\.')}/\\*\\*$`, 'm'))
		for (const tool of GENERATORS) assert.ok(workflow.includes(`- ${tool}`), tool)
		for (const steps of [REQNROLL_STEPS, PLAYWRIGHT_STEPS]) assert.ok(workflow.includes(`- ${steps}/**`), steps)
	})

	for (const path of ['.githooks/post-merge', '.githooks/post-rewrite', '.github/workflows/traceability.yml', 'tools/dev/ci-local.sh']) {
		it(`${path} regenerates every generated file`, () => {
			const text = read(path)

			for (const generated of GENERATED) assert.ok(text.includes(generated), `names ${generated}`)
		})
	}

	for (const path of ['.githooks/post-merge', '.githooks/post-rewrite', 'init-dev.sh']) {
		it(`${path} merges the specification into the local graph`, () => {
			assert.ok(read(path).includes('node tools/spec/graph-fragment.ts'))
		})
	}

	it('ci.yml checks every generated file, every link, the glossary, and the scenario style', () => {
		const ci = read('.github/workflows/ci.yml')

		assert.ok(ci.includes('node tools/spec/generate-traceability.ts --check'))
		assert.ok(ci.includes('node tools/spec/generate-spec-index.ts --check'))
		assert.ok(ci.includes('node tools/docs/check-links.ts'))
		assert.ok(ci.includes('node tools/spec/check-glossary.ts'))
		assert.ok(ci.includes('node tools/gherkin/lint-scenarios.ts'))
	})

	it('.gitattributes keeps the checked-out side of every generated file on a conflict', () => {
		const attributes = read('.gitattributes')

		for (const generated of GENERATED) assert.match(attributes, new RegExp(`^${generated.replace(/\./g, '\\.')} merge=ours$`, 'm'))
	})
})
