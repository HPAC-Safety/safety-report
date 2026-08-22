#!/usr/bin/env node
/**
 * Coverage gate: an absolute floor, plus a ratchet against main.
 *
 * Reads a merged Cobertura report (ReportGenerator output) and fails when
 * either check is violated:
 *
 *   floor    coverage is below the configured minimum
 *   ratchet  this change dilutes coverage
 *
 * The floor stops the number sinking over years. The ratchet stops it sinking
 * in a single pull request that adds a lot of untested code while staying just
 * above the floor. Neither is sufficient alone.
 *
 * The ratchet has two modes, because comparing whole-repository ratios is only
 * meaningful when the repository is roughly the same size on both sides:
 *
 *   ratio    the branch's percentage against main's, for ordinary changes
 *   new code the coverage of the lines this branch *added*, when it adds
 *            enough to move the ratio on its own
 *
 * A scaffolding repository sits at 100% over a few dozen trivial lines. Under a
 * ratio comparison the first real feature can only pass by being perfect,
 * including the defensive branches no test can reach — which pushes people
 * toward deleting the guard rather than writing the test. Judging the added
 * lines answers the question the ratchet is actually asking: did this change
 * bring its tests with it. See ADR-0017.
 *
 * Coverage here is a floor, not a goal. See docs/testing-conventions.md — the
 * anonymization suite is what actually protects a reporter, and this number
 * must never become the thing anyone optimises for.
 *
 * Usage:
 *   node tools/coverage-gate.mjs --report <Cobertura.xml>
 *                                [--baseline <Cobertura.xml>]
 *                                [--min-line 80] [--min-branch 70]
 *                                [--tolerance 0.1]
 */

import { readFileSync, appendFileSync } from 'node:fs'

const args = new Map()
for (let i = 2; i < process.argv.length; i += 2) {
	args.set(process.argv[i].replace(/^--/, ''), process.argv[i + 1])
}

/** True when this file was run as a command rather than imported by a test. */
const runAsCommand = process.argv[1]?.endsWith('coverage-gate.mjs') ?? false

const reportPath = args.get('report')
const baselinePath = args.get('baseline')
const minLine = Number(args.get('min-line') ?? 80)
const minBranch = Number(args.get('min-branch') ?? 70)
// A refactor that deletes one covered and one uncovered line moves the ratio a
// hair. Tolerance keeps that from failing a build; it is deliberately far too
// small to hide a dropped test.
const tolerance = Number(args.get('tolerance') ?? 0.1)

if (runAsCommand && !reportPath) {
	console.error('::error::--report is required')
	process.exit(2)
}

/**
 * Cobertura's root element carries the totals. Reading the attributes off it
 * beats walking the tree: no XML dependency, and this repository ships no
 * bundler and no node_modules by design.
 */
function readTotals(path) {
	const head = readFileSync(path, 'utf8').slice(0, 4096)
	const root = /<coverage([^>]*)>/.exec(head)
	if (!root) throw new Error(`No <coverage> element in ${path}`)

	const attrs = Object.fromEntries(
		[...root[1].matchAll(/([\w-]+)="([^"]*)"/g)].map(([, k, v]) => [k, v]),
	)

	const pct = (covered, valid) => {
		const c = Number(covered)
		const v = Number(valid)
		// No branches at all is not 0% coverage, it is "nothing to cover".
		// Reporting 0 here would fail the gate on a codebase with no branching.
		return v === 0 ? 100 : (c / v) * 100
	}

	return {
		line: pct(attrs['lines-covered'], attrs['lines-valid']),
		branch: pct(attrs['branches-covered'], attrs['branches-valid']),
		linesValid: Number(attrs['lines-valid']),
		branchesValid: Number(attrs['branches-valid']),
		linesCovered: Number(attrs['lines-covered']),
		branchesCovered: Number(attrs['branches-covered']),
	}
}

/**
 * How much bigger a branch has to be than its baseline before the ratio
 * comparison stops describing it. A quarter is a lot of new code: below that,
 * the existing body of code still dominates the percentage and a drop really
 * does mean tests were dropped.
 */
const GROWTH_THRESHOLD = 0.25

/** Growth small enough that the ratio is still the honest comparison. */
export function isOrdinaryChange(current, baseline) {
	const added = current.linesValid - baseline.linesValid
	return added <= 0 || added < Math.max(50, baseline.linesValid * GROWTH_THRESHOLD)
}

/**
 * Coverage of the lines a branch added, inferred from the two totals. Exact
 * per-line attribution would need the two reports diffed against the diff
 * itself; this is the same question at the resolution the gate needs.
 */
export function addedCoverage(current, baseline) {
	const line = {
		added: current.linesValid - baseline.linesValid,
		covered: current.linesCovered - baseline.linesCovered,
	}
	const branch = {
		added: current.branchesValid - baseline.branchesValid,
		covered: current.branchesCovered - baseline.branchesCovered,
	}

	// Nothing added is not 0% coverage of nothing, it is "nothing to cover".
	const pct = ({ added, covered }) => (added <= 0 ? 100 : (covered / added) * 100)

	return { line: pct(line), branch: pct(branch), linesAdded: line.added, branchesAdded: branch.added }
}

function main() {
	const fmt = n => `${n.toFixed(2)}%`
	const current = readTotals(reportPath)

	let baseline = null
	if (baselinePath && baselinePath !== 'none') {
		try {
			baseline = readTotals(baselinePath)
		} catch (error) {
			console.log(`::notice::No usable baseline (${error.message}). Ratchet skipped.`)
		}
	}

	const failures = []

	// --- floor ------------------------------------------------------------------
	if (current.line < minLine) {
		failures.push(`Line coverage ${fmt(current.line)} is below the ${minLine}% floor.`)
	}
	if (current.branch < minBranch) {
		failures.push(`Branch coverage ${fmt(current.branch)} is below the ${minBranch}% floor.`)
	}

	// --- ratchet ----------------------------------------------------------------
	let added = null
	if (baseline) {
		if (isOrdinaryChange(current, baseline)) {
			if (current.line < baseline.line - tolerance) {
				failures.push(
					`Line coverage dropped: ${fmt(baseline.line)} on main, ${fmt(current.line)} here.`,
				)
			}
			if (current.branch < baseline.branch - tolerance) {
				failures.push(
					`Branch coverage dropped: ${fmt(baseline.branch)} on main, ${fmt(current.branch)} here.`,
				)
			}
		} else {
			added = addedCoverage(current, baseline)

			if (added.line < minLine) {
				failures.push(
					`The ${added.linesAdded} lines this branch adds are ${fmt(added.line)} covered, below the ${minLine}% floor.`,
				)
			}
			if (added.branchesAdded > 0 && added.branch < minBranch) {
				failures.push(
					`The ${added.branchesAdded} branches this branch adds are ${fmt(added.branch)} covered, below the ${minBranch}% floor.`,
				)
			}
		}
	}

	// --- report -----------------------------------------------------------------
	const delta = (now, was) => {
		if (!was) return 'n/a'
		const d = now - was
		if (Math.abs(d) < 0.005) return '±0.00'
		return `${d > 0 ? '+' : ''}${d.toFixed(2)}`
	}

	const rows = [
		`| Metric | This branch | main | Δ | Floor |`,
		`|---|---|---|---|---|`,
		`| Line | ${fmt(current.line)} | ${baseline ? fmt(baseline.line) : '—'} | ${delta(current.line, baseline?.line)} | ${minLine}% |`,
		`| Branch | ${fmt(current.branch)} | ${baseline ? fmt(baseline.branch) : '—'} | ${delta(current.branch, baseline?.branch)} | ${minBranch}% |`,
		'',
		`${current.linesValid} coverable lines, ${current.branchesValid} branches. Generated code, migrations, and \`[ExcludeFromCodeCoverage]\` are excluded — see \`coverlet.runsettings\`.`,
	]

	if (!baseline) {
		rows.push('', '> No main baseline was available, so the ratchet did not run. The floor still applied.')
	} else if (added) {
		rows.push(
			'',
			`> This branch adds ${added.linesAdded} coverable lines to main's ${baseline.linesValid}, so the ratchet judged the **added code** rather than the whole-repository ratio: ${fmt(added.line)} of added lines and ${fmt(added.branch)} of added branches are covered. See ADR-0017.`,
		)
	}

	const summary = ['## Coverage', '', ...rows].join('\n')
	console.log(summary)

	if (process.env.GITHUB_STEP_SUMMARY) {
		appendFileSync(process.env.GITHUB_STEP_SUMMARY, `${summary}\n`)
	}
	if (process.env.GITHUB_OUTPUT) {
		appendFileSync(
			process.env.GITHUB_OUTPUT,
			`line=${current.line.toFixed(2)}\nbranch=${current.branch.toFixed(2)}\n`,
		)
	}

	if (failures.length > 0) {
		for (const failure of failures) console.error(`::error::${failure}`)
		console.error('')
		console.error('Coverage is a floor, not a target. Add the test that pins down the')
		console.error('behaviour this change introduced — not a test that raises the number.')
		process.exit(1)
	}

	console.log('\nCoverage gate passed.')
}

if (runAsCommand) {
	main()
}
