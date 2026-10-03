import { describe, it } from 'node:test'
import assert from 'node:assert/strict'
import { mkdirSync, mkdtempSync, readFileSync, writeFileSync } from 'node:fs'
import { tmpdir } from 'node:os'
import path from 'node:path'

import type { Exec } from '../../../tools/lib/actions.ts'
import { changedGenerator, GENERATORS, main } from '../../../tools/spec/regenerate-spec-docs.ts'

function trees({ headOverrides = {}, skip = [] }: { headOverrides?: Partial<Record<string, string>>; skip?: string[] } = {}): { base: string; head: string; output: string } {
	const root = mkdtempSync(path.join(tmpdir(), 'rsd-'))
	for (const side of ['base', 'head']) {
		for (const tool of GENERATORS) {
			if (side === 'head' && skip.includes(tool)) continue
			mkdirSync(path.dirname(path.join(root, side, tool)), { recursive: true })
			writeFileSync(path.join(root, side, tool), side === 'head' ? (headOverrides[tool] ?? `// ${tool}\n`) : `// ${tool}\n`)
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
		const t = trees({ headOverrides: { 'tools/spec/generate-spec-index.ts': '// edited\n' } })
		assert.equal(changedGenerator(t.base, t.head), 'tools/spec/generate-spec-index.ts')
	})

	it('treats a generator missing on the head as changed', () => {
		const t = trees({ skip: ['tools/docs/check-frontmatter.ts'] })
		assert.equal(changedGenerator(t.base, t.head), 'tools/docs/check-frontmatter.ts')
	})
})

describe('regenerate spec docs', () => {
	it('skips with a notice when a generator changed', () => {
		const t = trees({ headOverrides: { 'tools/spec/step-bindings.ts': '// edited\n' } })
		const logged: string[] = []
		const calls: unknown[][] = []
		const exec: Exec = (...c) => {
			calls.push(c)
			return { status: 0, stdout: '', stderr: '' }
		}
		const code = main({ argv: [t.base, t.head], env: { GITHUB_OUTPUT: t.output }, exec, log: (l) => logged.push(l) })
		assert.equal(code, 0)
		assert.equal(calls.length, 0)
		assert.match(logged[0] ?? '', /^::notice::This pull request changes tools\/spec\/step-bindings\.ts, so the base generators/)
		assert.equal(readFileSync(t.output, 'utf8'), 'changed=false\n')
	})

	function run(diffStatus: number) {
		const t = trees()
		const calls: { command: string; args: readonly string[]; cwd: string | undefined }[] = []
		const exec: Exec = (command, args = [], options = {}) => {
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
				['node', 'generate-traceability.ts', '--no-fail'],
				['node', 'generate-spec-index.ts'],
				['git', 'diff', '--quiet', '--', '.spec/claims.json', '.spec/traceability.md', '.spec/README.md'],
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
