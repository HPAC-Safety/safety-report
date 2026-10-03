import { describe, it } from 'node:test'
import assert from 'node:assert/strict'
import { execFileSync } from 'node:child_process'
import { mkdtempSync, mkdirSync, readFileSync, rmSync, symlinkSync, writeFileSync } from 'node:fs'
import { tmpdir } from 'node:os'
import { join } from 'node:path'

import { checkNumbering, checkStatus, claimedNumbers, main, nextNumber, renumber, statusLine } from '../../../tools/spec/adr-numbers.ts'

const adr = (number: string, title = 'A decision'): string => `---\ntitle: ${title}\ndescription: A decision.\ntype: adr\nstatus: accepted\ndate: 2026-09-22\ndecision-makers: Someone\nkeywords: a, b\n---\n\n# ADR-${number} — ${title}\n\n## Context\n\nSomething.\n`

/** A throwaway git repository holding the named ADR files. */
function repository(files: Record<string, string>): string {
	const root = mkdtempSync(join(tmpdir(), 'adr-numbers-'))
	mkdirSync(join(root, '.spec/decisions'), { recursive: true })
	for (const [name, contents] of Object.entries(files)) writeFileSync(join(root, '.spec/decisions', name), contents)

	execFileSync('git', ['-C', root, 'init', '--quiet'])
	execFileSync('git', ['-C', root, 'config', 'user.email', 'test@example.test'])
	execFileSync('git', ['-C', root, 'config', 'user.name', 'Test'])
	execFileSync('git', ['-C', root, 'add', '-A'])
	execFileSync('git', ['-C', root, 'commit', '--quiet', '-m', 'fixtures'])
	return root
}

/** Runs `main` with console output captured, restoring it afterwards even on failure. */
function runMain(argv: string[], root: string): { code: number; output: { log: string[]; error: string[] } } {
	const output: { log: string[]; error: string[] } = { log: [], error: [] }
	const original = { log: console.log, error: console.error }
	console.log = (...args: unknown[]) => output.log.push(args.join(' '))
	console.error = (...args: unknown[]) => output.error.push(args.join(' '))
	try {
		return { code: main(argv, root), output }
	} finally {
		console.log = original.log
		console.error = original.error
	}
}

describe('checkStatus', () => {
	const record = (number: string, status: string, line: string): string =>
		`---\ntitle: A\ndescription: B.\ntype: adr\nstatus: ${status}\ndate: 2026-09-30\ndecision-makers: Someone\nkeywords: a\n---\n\n# ADR-${number} — A\n\n${line}\n\n## Context\n\nADR-0009 was superseded by ADR-0010 long ago.\n`
	const check = (files: Record<string, string>): string[] => {
		const adrs = Object.keys(files).map((name) => ({ name, number: name.slice(4, 8) }))
		return checkStatus(adrs, (name) => files[name] ?? '')
	}

	it('passes a status that agrees with its status line', () => {
		assert.deepEqual(
			check({
				'ADR-0001-a.md': record('0001', 'partially-superseded', '**Status:** Accepted, partially superseded by\n[ADR-0002](ADR-0002-b.md).'),
				'ADR-0002-b.md': record('0002', 'accepted', '**Status:** Accepted.'),
			}),
			[],
		)
	})

	it('fails an accepted record whose status line says another supersedes it', () => {
		const problems = check({
			'ADR-0001-a.md': record('0001', 'accepted', '**Status:** Accepted, partially superseded by [ADR-0002](ADR-0002-b.md).'),
			'ADR-0002-b.md': record('0002', 'accepted', '**Status:** Accepted.'),
		})

		assert.equal(problems.length, 1)
		assert.match(problems[0], /ADR-0001-a\.md: its status line says it is superseded by ADR-0002/)
	})

	it('reads only the status line, not a record discussing another one', () => {
		assert.deepEqual(check({ 'ADR-0001-a.md': record('0001', 'accepted', '**Status:** Accepted.') }), [])
	})

	it('fails a successor that does not exist', () => {
		const problems = check({ 'ADR-0001-a.md': record('0001', 'superseded', '**Status:** Superseded by [ADR-0099](ADR-0099-gone.md).') })

		assert.match(problems[0], /names ADR-0099, which does not exist/)
	})

	it('fails a superseded record that links nothing that replaced it', () => {
		const problems = check({ 'ADR-0001-a.md': record('0001', 'superseded', '**Status:** Superseded.') })

		assert.match(problems[0], /needs a \*\*Status:\*\* line linking what replaced or narrowed it/)
	})

	it('fails a status outside the closed set', () => {
		assert.match(check({ 'ADR-0001-a.md': record('0001', 'draft', '**Status:** Draft.') })[0], /"status: draft" is not one of/)
	})

	it('accepts proposed and rejected records with no successor to link (ADR-0192)', () => {
		for (const status of ['proposed', 'rejected']) {
			assert.deepEqual(check({ 'ADR-0001-a.md': record('0001', status, `**Status:** ${status}.`) }), [], status)
		}
	})

	it('needs a deprecated record to link the ADR or convention that retired it', () => {
		assert.match(check({ 'ADR-0001-a.md': record('0001', 'deprecated', '**Status:** Deprecated.') })[0], /linking the ADR or convention that retired it/)
		assert.deepEqual(check({ 'ADR-0001-a.md': record('0001', 'deprecated', '**Status:** Deprecated by [CONV-001](../conventions/CONV-001-a.md).') }), [])
	})

	it('fails a deprecated record whose status line says another supersedes it', () => {
		const problems = check({
			'ADR-0001-a.md': record('0001', 'deprecated', '**Status:** Deprecated, superseded by [ADR-0002](ADR-0002-b.md).'),
			'ADR-0002-b.md': record('0002', 'accepted', '**Status:** Accepted.'),
		})

		assert.match(problems[0], /declares "status: deprecated" — make it superseded/)
	})

	it('reads a ## Status section when there is no bold status line', () => {
		assert.equal(statusLine('# ADR\n\n## Status\n\nAccepted; amended by [ADR-0002](x.md).\n\n## Context\n'), 'Accepted; amended by [ADR-0002](x.md).')
	})
})

describe('checkNumbering', () => {
	const read =
		(files: Record<string, string>) =>
		(name: string): string =>
			files[name] ?? ''

	it('accepts records numbered without collision', () => {
		const files = { 'ADR-0001-one.md': adr('0001'), 'ADR-0002-two.md': adr('0002') }

		assert.deepEqual(checkNumbering(localAdrsOf(files), read(files)), [])
	})

	it('reports two records sharing a number, and names both', () => {
		const files = { 'ADR-0089-one.md': adr('0089'), 'ADR-0089-two.md': adr('0089') }
		const problems = checkNumbering(localAdrsOf(files), read(files))

		assert.equal(problems.length, 1)
		assert.match(problems[0], /ADR-0089-two\.md: ADR-0089 is already taken by ADR-0089-one\.md/)
		assert.match(problems[0], /--renumber 0089/)
	})

	it('reports a filename and heading that disagree', () => {
		const files = { 'ADR-0003-three.md': adr('0004') }
		const problems = checkNumbering(localAdrsOf(files), read(files))

		assert.match(problems[0], /the file says ADR-0003 and the heading says ADR-0004/)
	})

	it('reports a record with no heading at all', () => {
		const files = { 'ADR-0005-five.md': '---\ntitle: A\n---\n\nNo heading here.\n' }

		assert.match(checkNumbering(localAdrsOf(files), read(files))[0], /no "# ADR-0005 — <title>" heading/)
	})

	it('accepts the older colon heading style', () => {
		const files = { 'ADR-0016-old.md': '# ADR-0016: The question set is data\n' }

		assert.deepEqual(checkNumbering(localAdrsOf(files), read(files)), [])
	})

	it('reports a file that is not named like a record', () => {
		const files = { 'ADR-draft-notes.md': '# Notes\n' }

		assert.match(checkNumbering(localAdrsOf(files), read(files))[0], /not named ADR-NNNN-kebab-slug\.md/)
	})
})

/** localAdrs' shape, without touching a filesystem. */
function localAdrsOf(files: Record<string, string>): { name: string; number: string | null }[] {
	return Object.keys(files)
		.sort()
		.map((name) => ({ name, number: name.match(/^ADR-(\d{4})-[a-z0-9-]+\.md$/)?.[1] ?? null }))
}

describe('a decision file whose name carries no number', () => {
	const files = { 'ADR-0001-one.md': adr('0001'), 'ADR-draft.md': '---\ntitle: Draft\n---\n' }

	it('is skipped by the status check and claims no number', () => {
		const root = repository(files)
		const adrs = [{ name: 'ADR-0001-one.md', number: '0001' }, { name: 'ADR-draft.md', number: null }]
		assert.deepEqual(checkStatus(adrs, (name) => files[name as keyof typeof files]), [])
		assert.deepEqual([...claimedNumbers(root, { remote: false })], ['0001'])
	})

	it('is refused by a renumber that names it, because it has no number to move', () => {
		assert.throws(() => renumber(repository(files), '0001', '0002', { file: 'ADR-draft.md' }), /ADR-draft\.md/)
	})

	it('is refused by a renumber of a file that does not exist', () => {
		assert.throws(() => renumber(repository(files), '0001', '0002', { file: 'ADR-9999-none.md' }), /No ADR-9999-none\.md in/)
	})
})

describe('a decision with no status line in its front matter', () => {
	it('is reported with an empty status', () => {
		const adrs = [{ name: 'ADR-0001-one.md', number: '0001' }]
		assert.deepEqual(
			checkStatus(adrs, () => '---\ntitle: A\n---\n\n# ADR-0001 — A\n'),
			['.spec/decisions/ADR-0001-one.md: "status: " is not one of ' + 'proposed, accepted, rejected, deprecated, superseded, partially-superseded'],
		)
	})
})

describe('nextNumber', () => {
	it('is one past the highest claimed, not one past the count', () => {
		assert.equal(nextNumber(new Set(['0001', '0089'])), '0090')
	})

	it('starts at 0001 when nothing is claimed', () => {
		assert.equal(nextNumber(new Set()), '0001')
	})
})

describe('claimedNumbers', () => {
	it('reads the working tree', () => {
		const root = repository({ 'ADR-0007-seven.md': adr('0007') })

		assert.ok(claimedNumbers(root, { remote: false }).has('0007'))
	})

	it('counts a number claimed on a remote branch nobody has merged yet', () => {
		// The race this tool exists for: another session's open pull request
		// has taken the number, and it is nowhere in this working tree.
		const origin = repository({ 'ADR-0007-seven.md': adr('0007') })
		execFileSync('git', ['-C', origin, 'checkout', '--quiet', '-b', 'someone-elses-work'])
		writeFileSync(join(origin, '.spec/decisions/ADR-0008-theirs.md'), adr('0008'))
		execFileSync('git', ['-C', origin, 'add', '-A'])
		execFileSync('git', ['-C', origin, 'commit', '--quiet', '-m', 'their decision'])

		const clone = mkdtempSync(join(tmpdir(), 'adr-numbers-clone-'))
		execFileSync('git', ['clone', '--quiet', origin, clone])
		execFileSync('git', ['-C', clone, 'fetch', '--quiet', 'origin'])

		const claimed = claimedNumbers(clone)

		assert.ok(claimed.has('0008'), 'a number claimed only on a remote branch is still taken')
		assert.equal(nextNumber(claimed), '0009')
	})

	it('counts a remote branch not yet rebased past the move to .spec/, and keeps scanning past one without it', () => {
		// The first ref git lists (origin/HEAD, origin/main) has .spec/decisions
		// and no docs/decisions; an un-rebased branch has only docs/decisions.
		// A missing directory on one ref must not end the scan (ADR-0183).
		const origin = repository({ 'ADR-0007-seven.md': adr('0007') })
		execFileSync('git', ['-C', origin, 'checkout', '--quiet', '-b', 'zz-before-the-move'])
		execFileSync('git', ['-C', origin, 'mv', '.spec/decisions', 'legacy'])
		mkdirSync(join(origin, 'docs'), { recursive: true })
		execFileSync('git', ['-C', origin, 'mv', 'legacy', 'docs/decisions'])
		writeFileSync(join(origin, 'docs/decisions/ADR-0012-old-place.md'), adr('0012'))
		execFileSync('git', ['-C', origin, 'add', '-A'])
		execFileSync('git', ['-C', origin, 'commit', '--quiet', '-m', 'a decision in the old place'])
		execFileSync('git', ['-C', origin, 'checkout', '--quiet', '-'])

		const clone = mkdtempSync(join(tmpdir(), 'adr-numbers-clone-'))
		execFileSync('git', ['clone', '--quiet', origin, clone])
		const reports: string[] = []

		const claimed = claimedNumbers(clone, { report: (line) => reports.push(line) })

		assert.ok(claimed.has('0012'), 'a number claimed under docs/decisions on an un-rebased branch is still taken')
		assert.match(reports.join('\n'), /1 still on docs\/decisions/)
	})

	it('falls back to the working tree where git cannot answer', () => {
		const root = mkdtempSync(join(tmpdir(), 'adr-numbers-bare-'))
		mkdirSync(join(root, '.spec/decisions'), { recursive: true })
		writeFileSync(join(root, '.spec/decisions/ADR-0003-three.md'), adr('0003'))

		assert.deepEqual([...claimedNumbers(root)], ['0003'])
	})
})

describe('renumber', () => {
	it('moves the file and rewrites every reference to it', () => {
		const root = repository({
			'ADR-0089-taken.md': adr('0089', 'Taken'),
			'ADR-0001-cites.md': `${adr('0001')}\nSee [ADR-0089](ADR-0089-taken.md), and bare ADR-0089 too.\n`,
		})

		const result = renumber(root, '0089', '0091')

		assert.equal(result.to, 'ADR-0091-taken.md')
		const citing = readFileSync(join(root, '.spec/decisions/ADR-0001-cites.md'), 'utf8')
		assert.match(citing, /\[ADR-0091\]\(ADR-0091-taken\.md\)/)
		assert.match(citing, /bare ADR-0091 too/)
		assert.doesNotMatch(citing, /ADR-0089/)

		const moved = readFileSync(join(root, '.spec/decisions/ADR-0091-taken.md'), 'utf8')
		assert.match(moved, /# ADR-0091 — Taken/)
	})

	it('rewrites the real file rather than a symlink pointing at it', () => {
		const root = repository({ 'ADR-0089-taken.md': adr('0089', 'Taken') })
		writeFileSync(join(root, 'NOTES.md'), 'See ADR-0089.\n')
		symlinkSync('NOTES.md', join(root, 'ALIAS.md'))
		execFileSync('git', ['-C', root, 'add', '-A'])
		execFileSync('git', ['-C', root, 'commit', '--quiet', '-m', 'notes and an alias'])

		const result = renumber(root, '0089', '0091')

		assert.ok(result.touched.includes('NOTES.md'))
		assert.ok(!result.touched.includes('ALIAS.md'), 'a symlink is not reported as a second edit')
		assert.match(readFileSync(join(root, 'NOTES.md'), 'utf8'), /ADR-0091/)
	})

	it('ignores a file git still tracks but the worktree no longer has', () => {
		const root = repository({ 'ADR-0089-taken.md': adr('0089', 'Taken') })
		writeFileSync(join(root, 'GONE.md'), 'Mentions ADR-0089.\n')
		execFileSync('git', ['-C', root, 'add', '-A'])
		execFileSync('git', ['-C', root, 'commit', '--quiet', '-m', 'a file about to vanish'])
		rmSync(join(root, 'GONE.md'))

		const result = renumber(root, '0089', '0091')

		assert.ok(!result.touched.includes('GONE.md'))
		assert.equal(result.to, 'ADR-0091-taken.md')
	})

	// A tracked path that is now a directory cannot be read as a file by any
	// user. A chmod 000 file can: root reads it anyway, and act runs its jobs
	// as root, so that version of this test skipped locally and the path it
	// covers measured differently from CI (#546).
	it('ignores a tracked path it cannot read as a file', () => {
		const root = repository({ 'ADR-0089-taken.md': adr('0089', 'Taken') })
		writeFileSync(join(root, 'LOCKED.md'), 'Mentions ADR-0089.\n')
		execFileSync('git', ['-C', root, 'add', '-A'])
		execFileSync('git', ['-C', root, 'commit', '--quiet', '-m', 'a file about to become a directory'])
		rmSync(join(root, 'LOCKED.md'))
		mkdirSync(join(root, 'LOCKED.md'))

		const result = renumber(root, '0089', '0091')

		assert.ok(!result.touched.includes('LOCKED.md'))
	})

	it('refuses a number that is already taken', () => {
		const root = repository({ 'ADR-0001-one.md': adr('0001'), 'ADR-0002-two.md': adr('0002') })

		assert.throws(() => renumber(root, '0001', '0002'), /ADR-0002 already exists/)
	})

	it('refuses a number that does not exist', () => {
		const root = repository({ 'ADR-0001-one.md': adr('0001') })

		assert.throws(() => renumber(root, '0044', '0045'), /No ADR-0044/)
	})
})

describe('main', () => {
	it('passes a cleanly numbered set', () => {
		const { code, output } = runMain([], repository({ 'ADR-0001-one.md': adr('0001') }))

		assert.equal(code, 0)
		assert.match(output.log.join('\n'), /1 decision record\(s\) numbered without collision/)
	})

	it('fails and annotates a duplicate', () => {
		const root = repository({ 'ADR-0089-one.md': adr('0089'), 'ADR-0089-two.md': adr('0089') })
		const { code, output } = runMain([], root)

		assert.equal(code, 1)
		assert.match(output.error.join('\n'), /::error file=\.spec\/decisions\/ADR-0089-two\.md::/)
	})

	it('prints the next free number', () => {
		const { code, output } = runMain(['--next'], repository({ 'ADR-0089-one.md': adr('0089') }))

		assert.equal(code, 0)
		assert.equal(output.log.join('').trim(), '0090')
	})

	it('renumbers from the command line', () => {
		const root = repository({ 'ADR-0089-one.md': adr('0089') })
		const { code, output } = runMain(['--renumber', '0089', '0090'], root)

		assert.equal(code, 0)
		assert.match(output.log.join('\n'), /ADR-0089-one\.md -> ADR-0090-one\.md/)
	})

	it('refuses to guess which record a duplicated number means', () => {
		const root = repository({ 'ADR-0090-one.md': adr('0090', 'One'), 'ADR-0090-two.md': adr('0090', 'Two') })
		const { code, output } = runMain(['--renumber', '0090', '0092'], root)

		assert.equal(code, 1)
		assert.match(output.error.join('\n'), /names 2 files/)
		assert.match(output.error.join('\n'), /--file <name>/)
	})

	it('leaves a bare reference alone while the number is ambiguous, and says so', () => {
		// Rewriting "ADR-0090" here would repoint a citation of the record that
		// is NOT moving at the one that is.
		const root = repository({ 'ADR-0090-one.md': adr('0090', 'One'), 'ADR-0090-two.md': adr('0090', 'Two') })
		writeFileSync(join(root, 'NOTES.md'), 'Mentions bare ADR-0090, meaning the other one.\n')
		execFileSync('git', ['-C', root, 'add', '-A'])
		execFileSync('git', ['-C', root, 'commit', '--quiet', '-m', 'notes'])

		const { code, output } = runMain(['--renumber', '0090', '0092', '--file', 'ADR-0090-two.md'], root)

		assert.equal(code, 0)
		assert.match(readFileSync(join(root, 'NOTES.md'), 'utf8'), /bare ADR-0090/, 'the ambiguous reference is untouched')
		assert.match(output.log.join('\n'), /bare reference\(s\) left alone/)
		assert.match(output.log.join('\n'), /NOTES\.md:1/)
		assert.match(readFileSync(join(root, '.spec/decisions/ADR-0092-two.md'), 'utf8'), /# ADR-0092 — Two/)
	})

	it('refuses a renumber that is not two four-digit numbers', () => {
		const { code, output } = runMain(['--renumber', '89', '90'], repository({ 'ADR-0089-one.md': adr('0089') }))

		assert.equal(code, 1)
		assert.match(output.error.join('\n'), /each number four digits/)
	})
})
