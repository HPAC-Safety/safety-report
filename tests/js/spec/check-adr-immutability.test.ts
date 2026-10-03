import { describe, it } from 'node:test'
import assert from 'node:assert/strict'
import { execFileSync } from 'node:child_process'
import { mkdtempSync, mkdirSync, rmSync, writeFileSync } from 'node:fs'
import { tmpdir } from 'node:os'
import { join } from 'node:path'

import { compare, immutableParts, linkedPath, main, statusRemainder } from '../../../tools/spec/check-adr-immutability.ts'

const record = (status: string, statusLine: string, decision = 'Use [the thing](../a/b.md).'): string =>
	`---\ntitle: A\ndescription: B.\ntype: adr\nstatus: ${status}\ndate: 2026-10-03\ndecision-makers: Someone\nkeywords: a\n---\n\n# ADR-0001 — A\n\n${statusLine}\n\n## Context\n\nWhy.\n\n## Decision\n\n${decision}\n`

const ACCEPTED = record('accepted', '**Status:** Accepted. Decided by the owner in [#1](https://example.test/1).')
const SUPERSEDED = record('superseded', '**Status:** Superseded by\n[ADR-0002](ADR-0002-b.md). Decided by the owner in [#1](https://example.test/1).')

describe('statusRemainder', () => {
	it('drops the status word and a Superseded or Deprecated sentence, and keeps the rest', () => {
		assert.equal(statusRemainder('**Status:** Accepted. Decided in #1.'), 'Decided in #1.')
		assert.equal(statusRemainder('**Status:** Superseded by [ADR-0002](ADR-0002-b.md). Decided in #1.'), 'Decided in #1.')
		assert.equal(statusRemainder('**Status:** Accepted. Deprecated by [CONV-001](../conventions/CONV-001-a.md). Decided in #1.'), 'Decided in #1.')
		assert.equal(statusRemainder('**Status:** Accepted, partially superseded by [ADR-0053](x.md): a note.'), 'partially superseded by [ADR-0053](x.md): a note.')
	})
})

describe('immutableParts and linkedPath', () => {
	it('separates the body, the status remainder, and the link targets', () => {
		const parts = immutableParts(ACCEPTED)

		assert.ok(!parts.body.includes('Accepted'))
		assert.equal(parts.status, 'Decided by the owner in [#1](https://example.test/1).')
		assert.deepEqual(parts.targets, ['../a/b.md'])
	})

	it('resolves a relative link from the decisions directory, and ignores a URL or anchor', () => {
		assert.equal(linkedPath('../a/b.md#part'), '.spec/a/b.md')
		assert.equal(linkedPath('ADR-0002-b.md'), '.spec/decisions/ADR-0002-b.md')
		assert.equal(linkedPath('https://example.test/x'), null)
		assert.equal(linkedPath('#section'), null)
	})
})

describe('compare', () => {
	const base = new Map([['ADR-0001-a.md', ACCEPTED]])
	const now = (text: string, name = 'ADR-0001-a.md'): Map<string, string> => new Map([[name, text]])

	it('passes a record unchanged, superseded, or deprecated', () => {
		assert.deepEqual(compare(base, now(ACCEPTED)), [])
		assert.deepEqual(compare(base, now(SUPERSEDED)), [])
		assert.deepEqual(compare(base, now(record('deprecated', '**Status:** Deprecated by [CONV-001](../conventions/CONV-001-a.md). Decided by the owner in [#1](https://example.test/1).'))), [])
	})

	it('follows a record whose file was renamed under the same number', () => {
		assert.deepEqual(compare(base, now(ACCEPTED, 'ADR-0001-renamed.md')), [])
	})

	it('fails a changed body, a deletion, and a new partially-superseded', () => {
		assert.match(compare(base, now(`${ACCEPTED}\n## Amendment (2026-10-04)\n\nLater.\n`))[0], /ADR-0001-a\.md: its body changed/)
		assert.match(compare(base, new Map())[0], /ADR-0001-a\.md: deleted/)
		assert.match(compare(base, now(record('partially-superseded', '**Status:** Accepted, partially superseded by [ADR-0002](ADR-0002-b.md).')))[0], /"status: accepted" may not become "status: partially-superseded"/)
	})

	it('fails accepted moving back to proposed, and any move out of a terminal status', () => {
		assert.match(compare(base, now(record('proposed', '**Status:** Proposed. Decided by the owner in [#1](https://example.test/1).')))[0], /an accepted record moves only to superseded or deprecated/)
		assert.match(compare(new Map([['ADR-0001-a.md', SUPERSEDED]]), now(ACCEPTED))[0], /superseded is terminal/)
		const rejected = record('rejected', '**Status:** Rejected.')
		assert.match(compare(new Map([['ADR-0001-a.md', rejected]]), now(record('accepted', '**Status:** Accepted.')))[0], /rejected is terminal/)
	})

	it('fails anything injected into the status paragraph beyond a status sentence', () => {
		const injected = record('superseded', '**Status:** Superseded by [ADR-0002](ADR-0002-b.md). Decided by the owner in [#1](https://example.test/1). Also, we now do something else.')

		assert.match(compare(base, now(injected))[0], /its body changed/)
	})

	it('lets a link follow a file that is gone, and fails one retargeted while its file still exists', () => {
		const moved = record('accepted', '**Status:** Accepted. Decided by the owner in [#1](https://example.test/1).', 'Use [the thing](../moved/b.md).')

		assert.deepEqual(compare(base, now(moved), (path) => path !== '.spec/a/b.md'), [])
		assert.match(compare(base, now(moved), () => true)[0], /a link's target changed from \.\.\/a\/b\.md, which still exists/)
	})

	it('fails a link whose text changed even when its file moved', () => {
		const renamed = record('accepted', '**Status:** Accepted. Decided by the owner in [#1](https://example.test/1).', 'Use [another thing](../moved/b.md).')

		assert.match(compare(base, now(renamed), () => false)[0], /its body changed/)
	})

	it('lets a record already partially superseded keep that status, and a proposed record change freely', () => {
		const partial = record('partially-superseded', '**Status:** Accepted, partially superseded by [ADR-0002](ADR-0002-b.md).')
		const proposed = record('proposed', '**Status:** Proposed.')

		assert.deepEqual(compare(new Map([['ADR-0001-a.md', partial]]), now(partial)), [])
		assert.deepEqual(compare(new Map([['ADR-0001-a.md', proposed]]), now(`${proposed}\nMore.\n`)), [])
	})

	it('ignores a new record and a file that is not a record', () => {
		assert.deepEqual(compare(new Map([['README.md', 'x']]), now('anything', 'ADR-0002-new.md')), [])
	})
})

describe('main', () => {
	const RULE = '.spec/decisions/ADR-0192-the-rule.md'

	/** A repository whose first commit is the base; returns the root, the base SHA, and a git runner. */
	function repository(withRule: boolean): { root: string; base: string; git: (...args: string[]) => string } {
		const root = mkdtempSync(join(tmpdir(), 'adr-immutability-'))
		const git = (...args: string[]): string => execFileSync('git', ['-C', root, ...args], { encoding: 'utf8' }).trim()
		mkdirSync(join(root, '.spec/decisions'), { recursive: true })
		writeFileSync(join(root, '.spec/decisions/ADR-0001-a.md'), ACCEPTED)
		writeFileSync(join(root, '.spec/decisions/ADR-0002-b.md'), record('accepted', '**Status:** Accepted.').replace('ADR-0001', 'ADR-0002'))
		if (withRule) writeFileSync(join(root, RULE), record('accepted', '**Status:** Accepted.').replace('ADR-0001', 'ADR-0192'))
		git('init', '--quiet', '--initial-branch=main')
		git('config', 'user.email', 'test@example.test')
		git('config', 'user.name', 'Test')
		git('add', '-A')
		git('commit', '--quiet', '-m', 'base')
		return { root, base: git('rev-parse', 'HEAD'), git }
	}

	const write = (root: string, text: string | null, name = 'ADR-0001-a.md'): void => {
		const path = join(root, '.spec/decisions', name)
		if (text === null) rmSync(path)
		else writeFileSync(path, text)
	}

	const commit = (git: (...args: string[]) => string): void => {
		git('add', '-A')
		git('commit', '--quiet', '--allow-empty', '-m', 'change')
	}

	function run(root: string, options: { argv?: string[]; env?: Record<string, string> }): { code: number; log: string[]; error: string[] } {
		const log: string[] = []
		const error: string[] = []
		const code = main({ ...options, cwd: root, log: (line) => log.push(line), error: (line) => error.push(line) })
		return { code, log, error }
	}

	it('passes a pull request that only supersedes an accepted record, counting only what it touched', () => {
		const { root, base, git } = repository(true)
		write(root, SUPERSEDED)
		commit(git)

		const result = run(root, { env: { BASE_SHA: base } })

		assert.equal(result.code, 0)
		assert.match(result.log[0], /1 changed ADR\(s\) since [0-9a-f]{12}, each changed only in its status/)
	})

	it('fails a pull request that edits or deletes an accepted record', () => {
		const edited = repository(true)
		write(edited.root, ACCEPTED.replace('Why.', 'Why, revised.'))
		commit(edited.git)
		const deleted = repository(true)
		write(deleted.root, null)
		commit(deleted.git)

		const editedResult = run(edited.root, { env: { BASE_SHA: edited.base } })
		const deletedResult = run(deleted.root, { env: { BASE_SHA: deleted.base } })

		assert.equal(editedResult.code, 1)
		assert.match(editedResult.error[0], /^::error file=\.spec\/decisions\/ADR-0001-a\.md::its body changed/)
		assert.equal(deletedResult.code, 1)
		assert.match(deletedResult.error[0], /deleted/)
	})

	it('follows a record moved with git mv, and still judges its body', () => {
		const clean = repository(true)
		clean.git('mv', '.spec/decisions/ADR-0001-a.md', '.spec/decisions/ADR-0001-renamed.md')
		commit(clean.git)
		const changed = repository(true)
		changed.git('mv', '.spec/decisions/ADR-0001-a.md', '.spec/decisions/ADR-0001-renamed.md')
		write(changed.root, ACCEPTED.replace('Why.', 'Why, revised.'), 'ADR-0001-renamed.md')
		commit(changed.git)

		assert.equal(run(clean.root, { env: { BASE_SHA: clean.base } }).code, 0)
		assert.match(run(changed.root, { env: { BASE_SHA: changed.base } }).error[0], /ADR-0001-renamed\.md::its body changed/)
	})

	it('fails a base that is not a commit in the clone', () => {
		const { root } = repository(true)

		const result = run(root, { env: { BASE_SHA: '0123456789abcdef0123456789abcdef01234567' } })

		assert.equal(result.code, 1)
		assert.match(result.error[0], /is not a commit in this clone/)
	})

	it('compares nothing when the base predates the rule, or there is no base', () => {
		const { root, base, git } = repository(false)
		write(root, ACCEPTED.replace('Why.', 'Normalized.'))
		commit(git)

		assert.match(run(root, { env: { BASE_SHA: base } }).log[0], /predates ADR-0192/)
		assert.match(run(root, { env: {} }).log[0], /No BASE_SHA/)
	})

	it('judges the index against the merge base with origin/main when staged', () => {
		const { root, base, git } = repository(true)
		git('update-ref', 'refs/remotes/origin/main', base)
		write(root, ACCEPTED.replace('Why.', 'Why, revised.'))
		git('add', '-A')

		const result = run(root, { argv: ['--staged'] })

		assert.equal(result.code, 1)
		assert.match(result.error[0], /its body changed/)
	})

	it('fails a staged deletion', () => {
		const { root, base, git } = repository(true)
		git('update-ref', 'refs/remotes/origin/main', base)
		git('rm', '--quiet', '.spec/decisions/ADR-0001-a.md')

		const result = run(root, { argv: ['--staged'] })

		assert.equal(result.code, 1)
		assert.match(result.error[0], /ADR-0001-a\.md::deleted/)
	})

	it('passes --staged when there is no origin/main to compare with', () => {
		const { root } = repository(true)

		assert.match(run(root, { argv: ['--staged'] }).log[0], /No origin\/main/)
	})
})
