#!/usr/bin/env node
// Prints the coverage results to the log for tools/dev/ci-local.sh (ci.yml
// `coverage`, under act only).
//
// Temporary: act keeps no job summary, and its artifact server rejects
// upload-artifact@v7 (nektos/act#6022), so a local run prints the gate, the
// test counts, the per-assembly detail, and the report's totals to the log,
// where tools/dev/ci-local.sh finds them between the begin and end markers.
// Remove this step when act ships the fix. ADR-0145.
//
// The attachment copies, one per project in a GUID directory, are what the
// merge reads. Deduplicated by content, they are the distinct per-project
// reports; fewer than the test projects means a suite's coverage was lost.
// That count is the `ci-local:reports=` line.
//
// Every file is optional: a missing one prints nothing, as `cat ... || true`
// did. Output is written raw, so a file's own newlines are kept.
import { createHash } from 'node:crypto'
import { readdirSync, readFileSync } from 'node:fs'
import path from 'node:path'
import { isMain } from '../lib/actions.mjs'

export const COVERAGE_DIR = './artifacts/coverage'

function readOrEmpty(file) {
	try {
		return readFileSync(file, 'utf8')
	} catch {
		return ''
	}
}

/** Every file called `name` under `dir`, as `dir`-prefixed paths. */
export function findFiles(dir, name) {
	const found = []
	let entries
	try {
		entries = readdirSync(dir, { withFileTypes: true })
	} catch {
		return found
	}
	for (const entry of entries) {
		const full = `${dir}/${entry.name}`
		if (entry.isDirectory()) found.push(...findFiles(full, name))
		else if (entry.name === name) found.push(full)
	}
	return found
}

/** The `<coverage ...>` opening tags of a Cobertura report. */
export function coverageTags(xml) {
	return xml.split('\n').flatMap((line) => line.match(/<coverage [^>]*>/g) ?? [])
}

/** The sorted package names of a Cobertura report, each followed by a space. */
export function packageNames(xml) {
	return (xml.match(/<package name="[^"]*"/g) ?? [])
		.map((tag) => tag.slice('<package name="'.length, -1))
		.sort()
		.map((name) => `${name} `)
		.join('')
}

export function render({ comment, summary, cobertura, coverageDir = COVERAGE_DIR }) {
	const reports = findFiles(coverageDir, 'coverage.cobertura.xml').sort()
	const lines = ['ci-local:coverage:begin\n', comment, '\n', summary, '\n', 'Cobertura totals:\n']
	for (const tag of coverageTags(cobertura)) lines.push(`${tag}\n`)
	lines.push('\n', 'Per-project reports (packages each one carries):\n')
	for (const file of reports) lines.push(`${file}: ${packageNames(readOrEmpty(file))}\n`)
	const attachments = reports.filter((file) => path.posix.relative(coverageDir, file).split('/').length === 2)
	const distinct = new Set(attachments.map((file) => createHash('sha256').update(readFileSync(file)).digest('hex')))
	lines.push(`ci-local:reports=${distinct.size}\n`, 'ci-local:coverage:end\n')
	return lines.join('')
}

export function main({ write = (text) => process.stdout.write(text) } = {}) {
	write(
		render({
			comment: readOrEmpty('comment.md'),
			summary: readOrEmpty('./artifacts/report/SummaryGithub.md'),
			cobertura: readOrEmpty('./artifacts/report/Cobertura.xml'),
		}),
	)
	return 0
}

if (isMain(import.meta.url)) process.exit(main())
