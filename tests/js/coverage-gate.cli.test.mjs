import { describe, it } from 'node:test'
import assert from 'node:assert/strict'
import { execFileSync } from 'node:child_process'
import { mkdtempSync, writeFileSync } from 'node:fs'
import { tmpdir } from 'node:os'
import { join } from 'node:path'

const GATE = new URL('../../tools/coverage-gate.mjs', import.meta.url).pathname
const workspace = mkdtempSync(join(tmpdir(), 'coverage-gate-'))

/** Writes a Cobertura file carrying only the totals the gate reads. */
function report(name, { lines, linesCovered, branches = 0, branchesCovered = 0 }) {
	const path = join(workspace, `${name}.xml`)
	writeFileSync(
		path,
		`<?xml version="1.0"?>\n<coverage lines-covered="${linesCovered}" lines-valid="${lines}" ` +
			`branches-covered="${branchesCovered}" branches-valid="${branches}">\n</coverage>\n`,
	)
	return path
}

/** Runs the gate as CI runs it, and reports what a human would see. */
function run(args) {
	try {
		const stdout = execFileSync(process.execPath, [GATE, ...args], { encoding: 'utf8' })
		return { code: 0, output: stdout }
	} catch (error) {
		return { code: error.status, output: `${error.stdout ?? ''}${error.stderr ?? ''}` }
	}
}

const scaffolding = report('scaffolding', { lines: 30, linesCovered: 30, branches: 4, branchesCovered: 4 })

describe('the coverage gate command', () => {
	describe('given no report argument', () => {
		it('when it runs then it refuses rather than passing silently', () => {
			// Given / When
			const { code, output } = run([])

			// Then
			assert.equal(code, 2)
			assert.match(output, /--report is required/)
		})
	})

	describe('given coverage above both floors and no baseline', () => {
		it('when it runs then it passes and says the ratchet did not run', () => {
			// Given
			const current = report('healthy', { lines: 400, linesCovered: 360, branches: 100, branchesCovered: 80 })

			// When
			const { code, output } = run(['--report', current])

			// Then
			assert.equal(code, 0)
			assert.match(output, /Coverage gate passed/)
			assert.match(output, /No main baseline was available/)
		})
	})

	describe('given coverage below the line floor', () => {
		it('when it runs then it fails and names the floor', () => {
			// Given
			const current = report('thin', { lines: 400, linesCovered: 200, branches: 100, branchesCovered: 90 })

			// When
			const { code, output } = run(['--report', current, '--min-line', '80', '--min-branch', '70'])

			// Then
			assert.equal(code, 1)
			assert.match(output, /Line coverage 50\.00% is below the 80% floor/)
		})
	})

	describe('given a large, well-tested feature against a scaffolding baseline', () => {
		it('when it runs then it judges the added code and passes', () => {
			// Given — the ratio drops from 100%, and that is not what the ratchet is asking
			const current = report('feature', { lines: 480, linesCovered: 478, branches: 190, branchesCovered: 187 })

			// When
			const { code, output } = run([
				'--report', current, '--baseline', scaffolding, '--min-line', '80', '--min-branch', '70',
			])

			// Then
			assert.equal(code, 0)
			assert.match(output, /judged the \*\*added code\*\*/)
			assert.match(output, /Coverage gate passed/)
		})
	})

	describe('given a large, untested addition', () => {
		it('when it runs then it fails and counts the added lines', () => {
			// Given
			const current = report('dumped', { lines: 480, linesCovered: 120, branches: 190, branchesCovered: 40 })

			// When
			const { code, output } = run([
				'--report', current, '--baseline', scaffolding, '--min-line', '80', '--min-branch', '70',
			])

			// Then
			assert.equal(code, 1)
			assert.match(output, /450 lines this branch adds/)
		})
	})

	describe('given an ordinary change that drops coverage', () => {
		it('when it runs then the ratio ratchet still catches it', () => {
			// Given — a big codebase, a small change, a deleted test suite
			const baseline = report('mature', { lines: 4000, linesCovered: 3800, branches: 900, branchesCovered: 800 })
			const current = report('regressed', { lines: 4020, linesCovered: 3400, branches: 900, branchesCovered: 800 })

			// When
			const { code, output } = run([
				'--report', current, '--baseline', baseline, '--min-line', '80', '--min-branch', '70',
			])

			// Then
			assert.equal(code, 1)
			assert.match(output, /Line coverage dropped: 95\.00% on main/)
		})
	})

	describe('given an unreadable baseline', () => {
		it('when it runs then the ratchet is skipped and the floor still applies', () => {
			// Given
			const current = report('fine', { lines: 400, linesCovered: 360, branches: 100, branchesCovered: 80 })

			// When
			const { code, output } = run([
				'--report', current, '--baseline', join(workspace, 'missing.xml'), '--min-line', '80',
			])

			// Then
			assert.equal(code, 0)
			assert.match(output, /No usable baseline/)
		})
	})
})
