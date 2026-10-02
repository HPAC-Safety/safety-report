import { describe, it } from 'node:test'
import assert from 'node:assert/strict'
import { mkdirSync, mkdtempSync, readFileSync, writeFileSync } from 'node:fs'
import { tmpdir } from 'node:os'
import path from 'node:path'

import { changedGenerator, GENERATORS, main } from '../../../tools/spec/regenerate-spec-docs.mjs'

function trees({ headOverrides = {}, skip = [] } = {}) {
	const root = mkdtempSync(path.join(tmpdir(), 'rsd-'))
	for (const side of ['base', 'head']) {
		mkdirSync(path.join(root, side, 'tools/spec'), { recursive: true })
		for (const tool of GENERATORS) {
			if (side === 'head' && skip.includes(tool)) continue
			writeFileSync(path.join(root, side, 'tools/spec', tool), side === 'head' && headOverrides[tool] ? headOverrides[tool] : `// ${tool}\n`)
		}
	}
	return { base: path.join(root, 'base'), head: path.join(root, 'head'), output: path.join(root, 'out') }
}

describe('changed generator', () => {
	it('is null when every generator matches', () => {
		const t = trees()
		assert.equal(changedGenerator(t.base, t.head), null)
	})

	it('names the first generator the pull request edited', () => {
		const t = trees({ headOverrides: { 'generate-spec-index.mjs': '// edited\n' } })
		assert.equal(changedGenerator(t.base, t.head), 'generate-spec-index.mjs')
	})

	it('treats a generator missing on the head as changed', () => {
		const t = trees({ skip: ['spec-paths.mjs'] })
		assert.equal(changedGenerator(t.base, t.head), 'spec-paths.mjs')
	})
})

describe('regenerate spec docs', () => {
	it('skips with a notice when a generator changed', () => {
		const t = trees({ headOverrides: { 'generate-bindings.mjs': '// edited\n' } })
		const logged = []
		const calls = []
		const code = main({ argv: [t.base, t.head], env: { GITHUB_OUTPUT: t.output }, exec: (...c) => calls.push(c), log: (l) => logged.push(l) })
		assert.equal(code, 0)
		assert.equal(calls.length, 0)
		assert.match(logged[0], /^::notice::This pull request changes tools\/spec\/generate-bindings\.mjs, so the base generators/)
		assert.equal(readFileSync(t.output, 'utf8'), 'changed=false\n')
	})

	function run(diffStatus) {
		const t = trees()
		const calls = []
		const exec = (command, args, options) => {
			calls.push({ command, args, cwd: options.cwd })
			return { status: command === 'git' ? diffStatus : 0, stdout: '', stderr: '' }
		}
		const code = main({ argv: [t.base, t.head], env: { GITHUB_OUTPUT: t.output }, exec, log: () => {} })
		return { t, calls, code }
	}

	it('runs the base generators inside the head checkout, then reports a change', () => {
		const { t, calls, code } = run(1)
		assert.equal(code, 0)
		assert.deepEqual(
			calls.map((call) => [call.command, path.basename(call.args[0]), ...call.args.slice(1)]),
			[
				['node', 'generate-traceability.mjs'],
				['node', 'generate-spec-index.mjs'],
				['node', 'generate-bindings.mjs', '--no-fail'],
				['git', 'diff', '--quiet', '--', '.spec/traceability.md', '.spec/README.md', '.spec/bindings.md'],
			],
		)
		assert.ok(calls.every((call) => call.cwd === t.head))
		assert.ok(calls[0].args[0].startsWith(t.base), 'the generator comes from the base checkout')
		assert.equal(readFileSync(t.output, 'utf8'), 'changed=true\n')
	})

	it('reports no change when nothing differs', () => {
		const { t } = run(0)
		assert.equal(readFileSync(t.output, 'utf8'), 'changed=false\n')
	})

	it('fails when a generator fails', () => {
		const t = trees()
		const code = main({ argv: [t.base, t.head], env: { GITHUB_OUTPUT: t.output }, exec: () => ({ status: 2, stdout: '', stderr: '' }), log: () => {} })
		assert.equal(code, 2)
	})
})
