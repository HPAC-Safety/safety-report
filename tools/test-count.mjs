#!/usr/bin/env node
/**
 * Test-count tables for the coverage PR comment.
 *
 * Prints three separate markdown tables, not one combined sum, because
 * "how many tests" means something different for each suite: xUnit
 * (per assembly), Reqnroll (per feature — its scenario tests are generated
 * at build time, one class per .feature file, so nothing to grep exists in
 * committed source), and Playwright (per feature — BDD specs generated from
 * @ui scenarios, plus the standalone smoke spec). ci.yml's `coverage` job
 * appends this output to the same comment.md the coverage gate writes, so
 * all three tables post alongside the coverage table. See #263.
 *
 * xUnit vs Reqnroll is read from each TRX `<UnitTest>`'s className: Reqnroll's
 * xUnit generator puts every scenario class under a `...Features.<Feature>.`
 * namespace segment, one per .feature file; anything else in the same
 * assembly (HpacSafety.Acceptance.Tests has both) is a hand-written xUnit
 * test. `total` (not just `passed`) is used, so a @ui-tagged scenario the
 * `Category!=ui` filter skips this run still counts: it is a real test, just
 * not one this run executed.
 *
 * `coverage` depends on `e2e` (see ci.yml) and downloads the e2e job's
 * fragment as an artifact rather than regenerating it, because only the e2e
 * job has actually run Playwright against a real browser.
 *
 * Usage:
 *   node tools/test-count.mjs --section csharp --trx-dir ./artifacts/coverage [--tests-dir tests]
 *   node tools/test-count.mjs --section e2e [--e2e-gen tests/e2e/.features-gen]
 *                                            [--smoke-spec tests/e2e/smoke.spec.ts]
 */

import { readdirSync, readFileSync, statSync } from 'node:fs'
import { join, relative } from 'node:path'

const args = new Map()
for (let i = 2; i < process.argv.length; i += 2) {
	args.set(process.argv[i].replace(/^--/, ''), process.argv[i + 1])
}

const section = args.get('section') ?? 'both'
const trxDir = args.get('trx-dir') ?? './artifacts/coverage'
const testsDir = args.get('tests-dir') ?? 'tests'
const e2eGenDir = args.get('e2e-gen') ?? 'tests/e2e/.features-gen'
const smokeSpec = args.get('smoke-spec') ?? 'tests/e2e/smoke.spec.ts'

function exists(path) {
	try {
		statSync(path)
		return true
	} catch {
		return false
	}
}

/** Every *.trx file under `dir`, recursively — coverlet nests per-run subfolders. */
function trxFiles(dir) {
	const out = []
	for (const entry of readdirSync(dir, { withFileTypes: true })) {
		const path = join(dir, entry.name)
		if (entry.isDirectory()) out.push(...trxFiles(path))
		else if (entry.name.endsWith('.trx')) out.push(path)
	}
	return out
}

/** The real casing of a project directory under tests/, from a lowercased dll name. */
function assemblyCasing(testsDir) {
	if (!exists(testsDir)) return new Map()
	return new Map(
		readdirSync(testsDir, { withFileTypes: true })
			.filter(e => e.isDirectory())
			.map(e => [e.name.toLowerCase(), e.name]),
	)
}

/**
 * Every `<UnitTest>` in one TRX file, classified as a Reqnroll scenario
 * (feature: its group name) or a plain xUnit test (feature: null). The
 * `storage` attribute carries the DLL path; `className` carries the
 * generated `...Features.<Feature>.<FeatureName>Feature` namespace Reqnroll
 * emits for a scenario, or the test's own namespace otherwise.
 */
function readTrxTests(path, casing) {
	const text = readFileSync(path, 'utf8')
	const definitions = /<TestDefinitions>([\s\S]*?)<\/TestDefinitions>/.exec(text)
	if (!definitions) return []

	const tests = []
	const unitTestPattern = /<UnitTest\b[^>]*>[\s\S]*?<\/UnitTest>/g
	for (const unitTest of definitions[1].match(unitTestPattern) ?? []) {
		const storage = /storage="([^"]*)"/.exec(unitTest)
		const className = /className="([^"]*)"/.exec(unitTest)
		if (!storage || !className) continue

		const lowerName = storage[1].split('/').pop().replace(/\.dll$/i, '')
		const assembly = casing.get(lowerName) ?? lowerName

		const feature = /\.Features\.([^.]+)\./.exec(className[1])
		tests.push({ assembly, feature: feature ? feature[1].replace(/_/g, ' ') : null })
	}
	return tests
}

/** Playwright's `test(` call count in a generated or hand-written spec file. */
function countTests(file) {
	const text = readFileSync(file, 'utf8')
	return (text.match(/^\s*test\(/gm) ?? []).length
}

function countBy(rows) {
	const counts = new Map()
	for (const key of rows) counts.set(key, (counts.get(key) ?? 0) + 1)
	return [...counts.entries()].sort((a, b) => a[0].localeCompare(b[0]))
}

function table(heading, columnLabel, rows) {
	if (rows.length === 0) return null
	const total = rows.reduce((sum, [, count]) => sum + count, 0)
	const lines = [`<details><summary>${heading} (${total} tests)</summary>`, '']
	lines.push(`| ${columnLabel} | Tests |`, '|---|---|')
	for (const [name, count] of rows) lines.push(`| ${name} | ${count} |`)
	lines.push('', '</details>', '')
	return lines
}

function main() {
	const lines = []
	if (section === 'both') lines.push('## Test counts', '')

	if (section === 'both' || section === 'csharp') {
		const allTests = exists(trxDir)
			? trxFiles(trxDir).flatMap(f => readTrxTests(f, assemblyCasing(testsDir)))
			: []
		const xunitRows = countBy(allTests.filter(t => t.feature === null).map(t => t.assembly))
		const reqnrollRows = countBy(allTests.filter(t => t.feature !== null).map(t => t.feature))

		const xunit = table('xUnit', 'Assembly', xunitRows)
		lines.push(...(xunit ?? ['_No .trx results found in this run._', '']))

		const reqnroll = table('Reqnroll', 'Feature', reqnrollRows)
		lines.push(...(reqnroll ?? ['_No Reqnroll scenario tests found in this run._', '']))
	}

	if (section === 'both' || section === 'e2e') {
		const rows = []
		if (exists(e2eGenDir)) {
			for (const entry of readdirSync(e2eGenDir, { withFileTypes: true })) {
				if (!entry.isDirectory()) continue
				const dir = join(e2eGenDir, entry.name)
				let count = 0
				for (const file of readdirSync(dir)) {
					if (file.endsWith('.spec.js')) count += countTests(join(dir, file))
				}
				rows.push([`${entry.name}.feature`, count])
			}
		}
		if (exists(smokeSpec)) rows.push([relative('.', smokeSpec), countTests(smokeSpec)])
		rows.sort((a, b) => a[0].localeCompare(b[0]))

		const playwright = table('Playwright (E2E)', 'Source', rows)
		lines.push(...(playwright ?? ['_Playwright suite not run in this job._', '']))
	}

	console.log(lines.join('\n'))
}

main()
