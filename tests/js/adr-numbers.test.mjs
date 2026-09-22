import { describe, it } from 'node:test'
import assert from 'node:assert/strict'
import { execFileSync } from 'node:child_process'
import { mkdtempSync, mkdirSync, readFileSync, writeFileSync } from 'node:fs'
import { tmpdir } from 'node:os'
import { join } from 'node:path'

import { checkNumbering, claimedNumbers, localAdrs, main, nextNumber, renumber } from '../../tools/adr-numbers.mjs'

const adr = (number, title = 'A decision') => `---\ntitle: ${title}\ndescription: A decision.\ntype: adr\nstatus: accepted\ndate: 2026-09-22\ndecision-makers: Someone\nkeywords: a, b\n---\n\n# ADR-${number} — ${title}\n\n## Context\n\nSomething.\n`

/** A throwaway git repository holding the named ADR files. */
function repository(files) {
	const root = mkdtempSync(join(tmpdir(), 'adr-numbers-'))
	mkdirSync(join(root, 'docs/decisions'), { recursive: true })
	for (const [name, contents] of Object.entries(files)) writeFileSync(join(root, 'docs/decisions', name), contents)

	execFileSync('git', ['-C', root, 'init', '--quiet'])
	execFileSync('git', ['-C', root, 'config', 'user.email', 'test@example.test'])
	execFileSync('git', ['-C', root, 'config', 'user.name', 'Test'])
	execFileSync('git', ['-C', root, 'add', '-A'])
	execFileSync('git', ['-C', root, 'commit', '--quiet', '-m', 'fixtures'])
	return root
}

/** Runs `main` with console output captured, restoring it afterwards even on failure. */
function runMain(argv, root) {
	const output = { log: [], error: [] }
	const original = { log: console.log, error: console.error }
	console.log = (...args) => output.log.push(args.join(' '))
	console.error = (...args) => output.error.push(args.join(' '))
	try {
		return { code: main(argv, root), output }
	} finally {
		console.log = original.log
		console.error = original.error
	}
}

describe('checkNumbering', () => {
	const read = (files) => (name) => files[name]

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
function localAdrsOf(files) {
	return Object.keys(files)
		.sort()
		.map((name) => ({ name, number: name.match(/^ADR-(\d{4})-[a-z0-9-]+\.md$/)?.[1] ?? null }))
}

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
		writeFileSync(join(origin, 'docs/decisions/ADR-0008-theirs.md'), adr('0008'))
		execFileSync('git', ['-C', origin, 'add', '-A'])
		execFileSync('git', ['-C', origin, 'commit', '--quiet', '-m', 'their decision'])

		const clone = mkdtempSync(join(tmpdir(), 'adr-numbers-clone-'))
		execFileSync('git', ['clone', '--quiet', origin, clone])
		execFileSync('git', ['-C', clone, 'fetch', '--quiet', 'origin'])

		const claimed = claimedNumbers(clone)

		assert.ok(claimed.has('0008'), 'a number claimed only on a remote branch is still taken')
		assert.equal(nextNumber(claimed), '0009')
	})

	it('falls back to the working tree where git cannot answer', () => {
		const root = mkdtempSync(join(tmpdir(), 'adr-numbers-bare-'))
		mkdirSync(join(root, 'docs/decisions'), { recursive: true })
		writeFileSync(join(root, 'docs/decisions/ADR-0003-three.md'), adr('0003'))

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
		const citing = readFileSync(join(root, 'docs/decisions/ADR-0001-cites.md'), 'utf8')
		assert.match(citing, /\[ADR-0091\]\(ADR-0091-taken\.md\)/)
		assert.match(citing, /bare ADR-0091 too/)
		assert.doesNotMatch(citing, /ADR-0089/)

		const moved = readFileSync(join(root, 'docs/decisions/ADR-0091-taken.md'), 'utf8')
		assert.match(moved, /# ADR-0091 — Taken/)
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
		assert.match(output.error.join('\n'), /::error file=docs\/decisions\/ADR-0089-two\.md::/)
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

	it('refuses a renumber that is not two four-digit numbers', () => {
		const { code, output } = runMain(['--renumber', '89', '90'], repository({ 'ADR-0089-one.md': adr('0089') }))

		assert.equal(code, 1)
		assert.match(output.error.join('\n'), /each four digits/)
	})
})
