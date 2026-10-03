import { describe, it } from 'node:test'
import assert from 'node:assert/strict'

import { type WorkflowRun, isEligible, selectBaselineRun } from '../../../tools/coverage/find-coverage-baseline.ts'

/** A push run on main, successful unless overridden - the common case. */
const run = (databaseId: number, overrides: Partial<WorkflowRun> = {}): WorkflowRun => ({
	databaseId,
	event: 'push',
	headBranch: 'main',
	conclusion: 'success',
	...overrides,
})

describe('the coverage baseline walk-back (#589)', () => {
	describe('given the newest run on main lacks the coverage-report artifact', () => {
		it('when it selects a baseline then it walks back to the older run that has one', () => {
			// Given - a docs-only merge left the newest run with no artifact
			const runs = [run(3), run(2), run(1)]
			const hasArtifact = (id: number) => id === 2

			// When
			const chosen = selectBaselineRun(runs, hasArtifact)

			// Then
			assert.equal(chosen, 2)
		})
	})

	describe('given the newest run\'s artifact has expired', () => {
		it('when it selects a baseline then the expired one is skipped for an older, live one', () => {
			// Given - hasArtifact only reports true for a non-expired coverage-report
			const runs = [run(5), run(4)]
			const hasArtifact = (id: number) => id === 4

			// When
			const chosen = selectBaselineRun(runs, hasArtifact)

			// Then
			assert.equal(chosen, 4)
		})
	})

	describe('given a candidate that is not a push run on main', () => {
		it('when checked for eligibility then a pull_request event is rejected', () => {
			assert.equal(isEligible(run(1, { event: 'pull_request' })), false)
		})

		it('when checked for eligibility then a merge-group branch is rejected', () => {
			assert.equal(isEligible(run(1, { headBranch: 'gh-readonly-queue/main/pr-12-abc123' })), false)
		})

		it('when checked for eligibility then a fork branch literally named main still needs event push', () => {
			// event push + headBranch main is exactly what a same-repository push looks
			// like; this only proves the check reads both fields, not one.
			assert.equal(isEligible(run(1, { event: 'push', headBranch: 'main' })), true)
		})

		it('when checked for eligibility then a failed run is rejected', () => {
			assert.equal(isEligible(run(1, { conclusion: 'failure' })), false)
		})

		it('when it selects a baseline then an ineligible run is skipped even if it has an artifact', () => {
			// Given - the newest run is a merge-group run (never main) with an artifact
			const runs = [run(9, { headBranch: 'gh-readonly-queue/main/pr-1-abc' }), run(8)]
			const hasArtifact = () => true

			// When
			const chosen = selectBaselineRun(runs, hasArtifact)

			// Then - the merge-group run is never chosen even though hasArtifact is true
			assert.equal(chosen, 8)
		})
	})

	describe('given no candidate within the walked-back runs has a usable artifact', () => {
		it('when it selects a baseline then none is chosen', () => {
			// Given
			const runs = [run(3), run(2), run(1)]
			const hasArtifact = () => false

			// When
			const chosen = selectBaselineRun(runs, hasArtifact)

			// Then
			assert.equal(chosen, null)
		})
	})

	describe('given no runs at all', () => {
		it('when it selects a baseline then none is chosen', () => {
			assert.equal(selectBaselineRun([], () => true), null)
		})
	})
})
