#!/usr/bin/env node
/**
 * Test-count table for the coverage PR comment.
 *
 * Prints a markdown table: one row per C# test assembly (parsed from the
 * TRX files the coverage job's own `dotnet test` run already produces) plus
 * one row per Playwright source (BDD specs generated from @ui feature
 * scenarios, plus the standalone smoke spec). ci.yml's `coverage` job
 * appends this output to the same comment.md the coverage gate writes, so
 * both tables post together. See #263.
 *
 * The C# count is read from TRX rather than grepped from source because
 * Reqnroll's scenario tests (HpacSafety.Acceptance.Tests) are generated at
 * build time from .feature files — nothing to grep exists in committed
 * source. `total` (not just `passed`) is used, so a @ui-tagged scenario the
 * `Category!=ui` filter skips still counts: it is a real test, just not one
 * this run executed.
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

/**
 * One TRX file's assembly name and test count. The `storage` attribute on a
 * `<UnitTest>` carries the DLL path, lowercased by the toolchain; the
 * project's real casing comes from matching it against `tests/*` on disk.
 */
function readTrx(path, testsDir) {
	const text = readFileSync(path, 'utf8')
	const storage = /storage="([^"]*)"/.exec(text)
	const counters = /<Counters\b([^/]*)\/>/.exec(text)
	if (!storage || !counters) return null

	const lowerName = storage[1].split('/').pop().replace(/\.dll$/i, '')
	const projectDirs = exists(testsDir)
		? readdirSync(testsDir, { withFileTypes: true }).filter(e => e.isDirectory())
		: []
	const assembly = projectDirs.find(e => e.name.toLowerCase() === lowerName)?.name ?? lowerName

	const total = /total="(\d+)"/.exec(counters[1])
	return { assembly, total: total ? Number(total[1]) : 0 }
}

/** Playwright's `test(` call count in a generated or hand-written spec file. */
function countTests(file) {
	const text = readFileSync(file, 'utf8')
	return (text.match(/^\s*test\(/gm) ?? []).length
}

function table(rows, unitLabel) {
	if (rows.length === 0) return null
	const total = rows.reduce((sum, [, count]) => sum + count, 0)
	const lines = [`<details><summary>${unitLabel} (${total} tests)</summary>`, '']
	lines.push('| Source | Tests |', '|---|---|')
	for (const [name, count] of rows) lines.push(`| ${name} | ${count} |`)
	lines.push('', '</details>', '')
	return lines
}

function main() {
	const lines = []
	if (section === 'both') lines.push('## Test counts', '')

	if (section === 'both' || section === 'csharp') {
		const byAssembly = new Map()
		if (exists(trxDir)) {
			for (const file of trxFiles(trxDir)) {
				const result = readTrx(file, testsDir)
				if (!result) continue
				byAssembly.set(result.assembly, (byAssembly.get(result.assembly) ?? 0) + result.total)
			}
		}
		const rows = [...byAssembly.entries()].sort((a, b) => a[0].localeCompare(b[0]))
		const rendered = table(rows, 'C#')
		lines.push(...(rendered ?? ['_No .trx results found in this run._', '']))
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

		const rendered = table(rows, 'Playwright')
		lines.push(...(rendered ?? ['_Playwright suite not run in this job._', '']))
	}

	console.log(lines.join('\n'))
}

main()
