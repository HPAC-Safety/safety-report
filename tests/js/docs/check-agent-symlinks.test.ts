import { describe, it, beforeEach, afterEach } from 'node:test'
import assert from 'node:assert/strict'
import { mkdirSync, mkdtempSync, realpathSync, rmSync, symlinkSync, writeFileSync } from 'node:fs'
import { tmpdir } from 'node:os'
import { join } from 'node:path'
import { spawnSync } from 'node:child_process'
import { fileURLToPath } from 'node:url'

import { main, problems } from '../../../tools/docs/check-agent-symlinks.ts'

describe('check-agent-symlinks', () => {
	let root: string
	beforeEach(() => {
		root = realpathSync(mkdtempSync(join(tmpdir(), 'agent-symlinks-')))
		mkdirSync(join(root, '.github'))
		mkdirSync(join(root, '.cursor/rules'), { recursive: true })
		writeFileSync(join(root, 'AGENTS.md'), '# agents')
	})
	afterEach(() => rmSync(root, { recursive: true, force: true }))

	const linkAll = () => {
		symlinkSync('AGENTS.md', join(root, 'CLAUDE.md'))
		symlinkSync('../AGENTS.md', join(root, '.github/copilot-instructions.md'))
		symlinkSync('../../AGENTS.md', join(root, '.cursor/rules/agents.mdc'))
	}

	it('passes when all three are symlinks to AGENTS.md', () => {
		linkAll()
		assert.deepEqual(problems(root), [])
		assert.equal(main({ root, log: () => assert.fail('logged') }), 0)
	})

	it('names a link that is a regular file or missing', () => {
		linkAll()
		rmSync(join(root, 'CLAUDE.md'))
		writeFileSync(join(root, 'CLAUDE.md'), 'copy')
		rmSync(join(root, '.cursor/rules/agents.mdc'))
		assert.deepEqual(problems(root), [
			'::error file=CLAUDE.md::Not a symlink. It must point at AGENTS.md.',
			'::error file=.cursor/rules/agents.mdc::Not a symlink. It must point at AGENTS.md.',
		])
	})

	it('names a link that resolves elsewhere, and a broken one', () => {
		linkAll()
		writeFileSync(join(root, 'OTHER.md'), '')
		rmSync(join(root, 'CLAUDE.md'))
		symlinkSync('OTHER.md', join(root, 'CLAUDE.md'))
		rmSync(join(root, '.github/copilot-instructions.md'))
		symlinkSync('../nowhere.md', join(root, '.github/copilot-instructions.md'))
		const found = problems(root)
		assert.equal(found[0], `::error file=CLAUDE.md::Resolves to '${join(root, 'OTHER.md')}', expected '${join(root, 'AGENTS.md')}'.`)
		assert.equal(found[1], `::error file=.github/copilot-instructions.md::Resolves to '<broken>', expected '${join(root, 'AGENTS.md')}'.`)
		const logs: string[] = []
		assert.equal(main({ root, log: (m) => logs.push(m) }), 1)
		assert.equal(logs.length, 2)
	})
})

describe('check-agent-symlinks as a command', () => {
	it('reports every link missing, and falls back to the unresolved AGENTS.md path', () => {
		const dir = realpathSync(mkdtempSync(join(tmpdir(), 'agent-symlinks-cli-')))
		const script = fileURLToPath(new URL('../../../tools/docs/check-agent-symlinks.ts', import.meta.url))
		const result = spawnSync(process.execPath, [script], { cwd: dir, encoding: 'utf8', env: { PATH: process.env.PATH } })
		rmSync(dir, { recursive: true, force: true })
		assert.equal(result.status, 1)
		assert.equal(result.stdout.split('\n').filter((line) => line.includes('Not a symlink')).length, 3)
	})

	it('resolves a link against a root with no AGENTS.md', () => {
		const dir = realpathSync(mkdtempSync(join(tmpdir(), 'agent-symlinks-none-')))
		symlinkSync('AGENTS.md', join(dir, 'CLAUDE.md'))
		const found = problems(dir, { links: ['CLAUDE.md'] })
		rmSync(dir, { recursive: true, force: true })
		assert.equal(found.length, 1)
		assert.match(found[0], /<broken>/)
	})
})
