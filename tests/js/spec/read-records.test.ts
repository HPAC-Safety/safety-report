import { describe, it } from 'node:test'
import assert from 'node:assert/strict'
import { mkdtempSync } from 'node:fs'
import { tmpdir } from 'node:os'
import { join } from 'node:path'

import { citations, readDecisions, readLessons, relations, statusParagraph } from '../../../tools/spec/read-records.ts'

describe('statusParagraph', () => {
	it('reads the bold status paragraph, or the first paragraph under ## Status', () => {
		assert.equal(statusParagraph('# ADR\n\n**Status:** Accepted. Amends\n[ADR-0001](x.md).\n\n## Context\n\nADR-0002\n'), '**Status:** Accepted. Amends\n[ADR-0001](x.md).')
		assert.equal(statusParagraph('# ADR\n\n## Status\n\nAccepted. Amended by ADR-0003.\n\nMore.\n'), 'Accepted. Amended by ADR-0003.')
		assert.equal(statusParagraph('# ADR\n'), '')
	})
})

describe('relations', () => {
	it('takes each ADR to the nearest verb before it in its sentence, in either voice', () => {
		const paragraph =
			'**Status:** Accepted. Amends [ADR-0001](ADR-0001-a.md) and [ADR-0002](ADR-0002-b.md) on one point. ' +
			'Partially superseded by [ADR-0009](ADR-0009-c.md): the format. Narrows ADR-0003. Extended by ADR-0010. Decided with ADR-0004.'

		assert.deepEqual(relations('ADR-0005', paragraph), [
			{ from: 'ADR-0005', type: 'amends', to: 'ADR-0001' },
			{ from: 'ADR-0005', type: 'amends', to: 'ADR-0001' },
			{ from: 'ADR-0005', type: 'amends', to: 'ADR-0002' },
			{ from: 'ADR-0005', type: 'amends', to: 'ADR-0002' },
			{ from: 'ADR-0009', type: 'supersedes', to: 'ADR-0005' },
			{ from: 'ADR-0009', type: 'supersedes', to: 'ADR-0005' },
			{ from: 'ADR-0005', type: 'supersedes', to: 'ADR-0003' },
			{ from: 'ADR-0010', type: 'amends', to: 'ADR-0005' },
		])
	})

	it('never relates a record to itself', () => {
		assert.deepEqual(relations('ADR-0005', '**Status:** Superseded by ADR-0005.'), [])
	})
})

describe('citations', () => {
	it('lists each claim, constraint, and other ADR once, sorted', () => {
		assert.deepEqual(citations('REQ-MED-002, REQ-MED-001, REQ-MED-002; CON-SO-001; ADR-0007 and ADR-0001 and ADR-0002', 'ADR-0002'), {
			claims: ['REQ-MED-001', 'REQ-MED-002'],
			constraints: ['CON-SO-001'],
			decisions: ['ADR-0001', 'ADR-0007'],
		})
	})
})

describe('readers', () => {
	it('read nothing from a tree with no decisions or lessons', () => {
		const root = mkdtempSync(join(tmpdir(), 'records-'))

		assert.deepEqual(readDecisions(root), [])
		assert.deepEqual(readLessons(root), [])
	})
})
