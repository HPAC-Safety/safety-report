import { describe, it } from 'node:test'
import assert from 'node:assert/strict'

import { addedCoverage, isOrdinaryChange } from '../../tools/coverage-gate.mjs'

const totals = (linesValid, linesCovered, branchesValid = 0, branchesCovered = 0) => ({
	linesValid,
	linesCovered,
	branchesValid,
	branchesCovered,
})

describe('the coverage ratchet', () => {
	describe('given a change that leaves the repository roughly the same size', () => {
		it('when it is classified then it is compared as a ratio', () => {
			// Given
			const baseline = totals(4000, 3600)
			const current = totals(4040, 3640)

			// When
			const ordinary = isOrdinaryChange(current, baseline)

			// Then
			assert.equal(ordinary, true)
		})
	})

	describe('given a change that deletes code', () => {
		it('when it is classified then it is compared as a ratio', () => {
			// Given — deleting tests must still be caught by the ratio
			const baseline = totals(4000, 3600)
			const current = totals(3200, 2600)

			// When
			const ordinary = isOrdinaryChange(current, baseline)

			// Then
			assert.equal(ordinary, true)
		})
	})

	describe('given a branch that grows the codebase by more than a quarter', () => {
		it('when it is classified then the added code is judged instead', () => {
			// Given — the first real feature against a scaffolding baseline
			const baseline = totals(30, 30)
			const current = totals(480, 478)

			// When
			const ordinary = isOrdinaryChange(current, baseline)

			// Then
			assert.equal(ordinary, false)
		})
	})

	describe('given a small repository and a small addition', () => {
		it('when it is classified then fifty lines is the floor for switching modes', () => {
			// Given — a quarter of 30 lines is 8, which is far too small a sample
			const baseline = totals(30, 30)
			const current = totals(70, 60)

			// When
			const ordinary = isOrdinaryChange(current, baseline)

			// Then
			assert.equal(ordinary, true)
		})
	})

	describe('given a branch that added well-tested code', () => {
		it('when the added coverage is measured then it reflects the new lines only', () => {
			// Given — main was at 100% over 30 lines; this branch adds 450 at 99.5%
			const baseline = totals(30, 30, 0, 0)
			const current = totals(480, 478, 200, 190)

			// When
			const added = addedCoverage(current, baseline)

			// Then
			assert.equal(added.linesAdded, 450)
			assert.equal(added.branchesAdded, 200)
			assert.ok(added.line > 99)
			assert.equal(added.branch, 95)
		})
	})

	describe('given a branch that added untested code', () => {
		it('when the added coverage is measured then it is low even though the total looks fine', () => {
			// Given — 200 new lines, 20 of them covered, hiding behind a large covered base
			const baseline = totals(1000, 1000)
			const current = totals(1200, 1020)

			// When
			const added = addedCoverage(current, baseline)

			// Then
			assert.equal(added.line, 10)
		})
	})

	describe('given a branch that adds no branches at all', () => {
		it('when the added coverage is measured then nothing to cover is not zero per cent', () => {
			// Given
			const baseline = totals(1000, 900, 100, 90)
			const current = totals(1400, 1300, 100, 90)

			// When
			const added = addedCoverage(current, baseline)

			// Then
			assert.equal(added.branch, 100)
			assert.equal(added.branchesAdded, 0)
		})
	})
})
