import { describe, it } from 'node:test'
import assert from 'node:assert/strict'
import { execFileSync } from 'node:child_process'
import { mkdtempSync, mkdirSync, rmSync, writeFileSync } from 'node:fs'
import { tmpdir } from 'node:os'
import { join } from 'node:path'

import { compare, immutablePart, main } from '../../../tools/spec/check-adr-immutability.ts'

const record = (status: string, statusLine: string, decision = 'Use [the thing](../a/b.md).'): string =>
	`---\ntitle: A\ndescription: B.\ntype: adr\nstatus: ${status}\ndate: 2026-10-03\ndecision-makers: Someone\nkeywords: a\n---\n\n# ADR-0001 — A\n\n${statusLine}\n\n## Context\n\nWhy.\n\n## Decision\n\n${decision}\n`

const ACCEPTED = record('accepted', '**Status:** Accepted.')

describe('immutablePart', () => {
	it('ignores the status, its line, and link targets, and nothing else', () => {
		const superseded = record('superseded', '**Status:** Superseded by\n[ADR-0002](ADR-0002-b.md), which\nnarrows it.', 'Use [the thing](../moved/b.md).')

		assert.equal(immutablePart(superseded), immutablePart(ACCEPTED))
		assert.notEqual(immutablePart(record('accepted', '**Status:** Accepted.', 'Use [another thing](../a/b.md).')), immutablePart(ACCEPTED))
	})
})

describe('compare', () => {
	const base = new Map([['ADR-0001-a.md', ACCEPTED]])

	it('passes a record unchanged, or changed only in its status', () => {
		assert.deepEqual(compare(base, new Map(base)), [])
		assert.deepEqual(compare(base, new Map([['ADR-0001-a.md', record('superseded', '**Status:** Superseded by [ADR-0002](ADR-0002-b.md).')]])), [])
	})

	it('follows a record whose file was renamed under the same number', () => {
		assert.deepEqual(compare(base, new Map([['ADR-0001-renamed.md', ACCEPTED]])), [])
	})

	it('fails a changed body, a deletion, and a new partially-superseded', () => {
		const edited = compare(base, new Map([['ADR-0001-a.md', `${ACCEPTED}\n## Amendment (2026-10-04)\n\nLater.\n`]]))
		const deleted = compare(base, new Map())
		const partial = compare(base, new Map([['ADR-0001-a.md', record('partially-superseded', '**Status:** Accepted, partially superseded by [ADR-0002](ADR-0002-b.md).')]]))

		assert.match(edited[0], /ADR-0001-a\.md: its body changed/)
		assert.match(deleted[0], /ADR-0001-a\.md: deleted/)
		assert.match(partial[0], /newly "partially-superseded"/)
	})

	it('lets a record already partially superseded keep that status, and a proposed record change freely', () => {
		const partial = record('partially-superseded', '**Status:** Accepted, partially superseded by [ADR-0002](ADR-0002-b.md).')
		const proposed = record('proposed', '**Status:** Proposed.')

		assert.deepEqual(compare(new Map([['ADR-0001-a.md', partial]]), new Map([['ADR-0001-a.md', partial]])), [])
		assert.deepEqual(compare(new Map([['ADR-0001-a.md', proposed]]), new Map([['ADR-0001-a.md', `${proposed}\nMore.\n`]])), [])
	})

	it('ignores a new record and a file that is not a record', () => {
		assert.deepEqual(compare(new Map([['README.md', 'x']]), new Map([['ADR-0002-new.md', 'anything']])), [])
	})
})

describe('main', () => {
	/** A repository whose first commit is the base; returns the root and the base SHA. */
	function repository(withTemplate: boolean): { root: string; base: string } {
		const root = mkdtempSync(join(tmpdir(), 'adr-immutability-'))
		const git = (...args: string[]): string => execFileSync('git', ['-C', root, ...args], { encoding: 'utf8' }).trim()
		mkdirSync(join(root, '.spec/decisions'), { recursive: true })
		writeFileSync(join(root, '.spec/decisions/ADR-0001-a.md'), ACCEPTED)
		if (withTemplate) writeFileSync(join(root, '.spec/decisions/TEMPLATE.md'), 'template')
		git('init', '--quiet', '--initial-branch=main')
		git('config', 'user.email', 'test@example.test')
		git('config', 'user.name', 'Test')
		git('add', '-A')
		git('commit', '--quiet', '-m', 'base')
		return { root, base: git('rev-parse', 'HEAD') }
	}

	const commit = (root: string, text: string | null): void => {
		const path = join(root, '.spec/decisions/ADR-0001-a.md')
		if (text === null) rmSync(path)
		else writeFileSync(path, text)
		execFileSync('git', ['-C', root, 'add', '-A'])
		execFileSync('git', ['-C', root, 'commit', '--quiet', '--allow-empty', '-m', 'change'])
	}

	function run(root: string, options: { argv?: string[]; env?: Record<string, string> }): { code: number; log: string[]; error: string[] } {
		const log: string[] = []
		const error: string[] = []
		const code = main({ ...options, cwd: root, log: (line) => log.push(line), error: (line) => error.push(line) })
		return { code, log, error }
	}

	it('passes a pull request that only supersedes an accepted record', () => {
		const { root, base } = repository(true)
		commit(root, record('superseded', '**Status:** Superseded by [ADR-0002](ADR-0002-b.md).'))

		const result = run(root, { env: { BASE_SHA: base } })

		assert.equal(result.code, 0)
		assert.match(result.log[0], /1 ADR\(s\) on [0-9a-f]{12} unchanged but for their status/)
	})

	it('fails a pull request that edits or deletes an accepted record', () => {
		const edited = repository(true)
		commit(edited.root, ACCEPTED.replace('Why.', 'Why, revised.'))
		const deleted = repository(true)
		commit(deleted.root, null)

		const editedResult = run(edited.root, { env: { BASE_SHA: edited.base } })
		const deletedResult = run(deleted.root, { env: { BASE_SHA: deleted.base } })

		assert.equal(editedResult.code, 1)
		assert.match(editedResult.error[0], /^::error file=\.spec\/decisions\/ADR-0001-a\.md::its body changed/)
		assert.equal(deletedResult.code, 1)
		assert.match(deletedResult.error[0], /deleted/)
	})

	it('compares nothing when the base predates the rule, or there is no base', () => {
		const { root, base } = repository(false)
		commit(root, ACCEPTED.replace('Why.', 'Normalized.'))

		assert.match(run(root, { env: { BASE_SHA: base } }).log[0], /predates ADR-0191/)
		assert.match(run(root, { env: {} }).log[0], /No BASE_SHA/)
	})

	it('judges the index against the merge base with origin/main when staged', () => {
		const { root, base } = repository(true)
		execFileSync('git', ['-C', root, 'update-ref', 'refs/remotes/origin/main', base])
		writeFileSync(join(root, '.spec/decisions/ADR-0001-a.md'), ACCEPTED.replace('Why.', 'Why, revised.'))
		execFileSync('git', ['-C', root, 'add', '-A'])

		const result = run(root, { argv: ['--staged'] })

		assert.equal(result.code, 1)
		assert.match(result.error[0], /its body changed/)
	})

	it('passes --staged when there is no origin/main to compare with', () => {
		const { root } = repository(true)

		assert.match(run(root, { argv: ['--staged'] }).log[0], /No origin\/main/)
	})
})
