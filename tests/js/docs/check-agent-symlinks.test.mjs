import { describe, it, beforeEach, afterEach } from 'node:test'
import assert from 'node:assert/strict'
import { mkdirSync, mkdtempSync, realpathSync, rmSync, symlinkSync, writeFileSync } from 'node:fs'
import { tmpdir } from 'node:os'
import { join } from 'node:path'

import { main, problems } from '../../../tools/docs/check-agent-symlinks.mjs'

describe('check-agent-symlinks', () => {
	let root
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
		const logs = []
		assert.equal(main({ root, log: (m) => logs.push(m) }), 1)
		assert.equal(logs.length, 2)
	})
})
