#!/usr/bin/env node
// The specification index, generated from the specification (ADR-0183).
//
// .spec/README.md lists every feature area with its scenario counts and
// supporting page, every constraint page, every decision record, and every
// lesson — each row read from the file it describes, so the index cannot drift
// from the tree the way a hand-kept table does. The lessons table it replaces
// had fallen behind more than once.
//
// Like the traceability matrix, nothing in it is counted across the whole tree
// (ADR-0106): every row derives from one file, so two branches that each carry
// a correct index merge into the index of the merged tree. Totals go to stdout.
//
//   node tools/spec/generate-spec-index.ts           write .spec/README.md
//   node tools/spec/generate-spec-index.ts --check   fail when the committed file differs
//
// Dependency-free, like every other tool in this directory. The exit code is
// the contract.
import { appendFileSync, existsSync, readFileSync, readdirSync, statSync, writeFileSync } from 'node:fs'
import { join, posix } from 'node:path'

import { parseFrontmatter } from '../docs/check-frontmatter.ts'
import { CONSTRAINT_PAGES, DECISIONS, FEATURES, LESSONS, SPEC_INDEX, SPEC_ROOT } from './spec-paths.ts'
import { readClaims, readConstraints } from './generate-traceability.ts'
import { isMain } from '../lib/actions.ts'

const ROOT = process.cwd()

/** The frontmatter of `text` as a plain object, quotes removed. */
export function frontmatter(text: string): Partial<Record<string, string>> {
	const { entries = [] } = parseFrontmatter(text)
	return Object.fromEntries(entries.map(({ key, value }) => [key, value.replace(/^(["'])(.*)\1$/, '$2')]))
}

/** A value made safe for one table cell. */
const cell = (text = ''): string => text.replace(/\s+/g, ' ').replace(/\|/g, '\\|').trim()

/** Where `path` (under the specification root) is linked from the index. */
const link = (path: string): string => posix.relative(SPEC_ROOT, path)

/** The body of a `## heading` section, up to the next heading of the same level. */
export function section(text: string, heading: string): string {
	const match = text.match(new RegExp(`^## ${heading}\\s*$([\\s\\S]*?)(?=^## |(?![\\s\\S]))`, 'm'))
	return match ? match[1] : ''
}

/**
 * What a lesson changed upstream: the claims under `## Scenario` and the skills
 * under `## Skill`, or `none` when it names neither.
 */
export function remedy(text: string): string {
	const claims = [...new Set(section(text, 'Scenario').match(/REQ-[A-Z]+-\d{3}/g) ?? [])]
	const skills = [...new Set([...section(text, 'Skill').matchAll(/`([a-z0-9]+(?:-[a-z0-9]+)+)`/g)].map((match) => match[1]))]
	const parts = [...claims, ...skills.map((skill) => `\`${skill}\``)]
	return parts.length > 0 ? parts.join(', ') : 'none'
}

/** One feature area's row. */
export interface Area {
	name: string
	title: string
	description: string | undefined
	feature: string
	readme: string | null
	scenarios: number
	planned: number
	browser: number
	prefix: string
}

/** One constraint page's row. */
export interface ConstraintPage {
	page: string
	title: string
	description: string | undefined
	count: number
	prefix: string
}

/** One decision or lesson file, with its frontmatter. */
export interface Numbered {
	path: string
	number: string
	text: string
	meta: Partial<Record<string, string>>
}

/** Every input the index is built from. */
export interface Inputs {
	areas: Area[]
	pages: ConstraintPage[]
	decisions: Numbered[]
	lessons: Numbered[]
}

function areas(root: string): Area[] {
	const directory = join(root, FEATURES)
	return readdirSync(directory)
		.filter((name) => statSync(join(directory, name)).isDirectory())
		.sort()
		.map((name) => {
			const feature = `${FEATURES}/${name}/${name}.feature`
			const readme = `${FEATURES}/${name}/README.md`
			const { claims } = readClaims(feature, readFileSync(join(root, feature), 'utf8'))
			const hasReadme = existsSync(join(root, readme))
			const meta = hasReadme ? frontmatter(readFileSync(join(root, readme), 'utf8')) : {}
			return {
				name,
				title: meta.title || name,
				description: meta.description,
				feature,
				readme: hasReadme ? readme : null,
				scenarios: claims.length,
				planned: claims.filter((claim) => claim.status === 'Planned').length,
				browser: claims.filter((claim) => claim.engine === 'playwright-bdd').length,
				prefix: claims[0]?.id.replace(/-\d{3}$/, '') ?? '',
			}
		})
}

function constraintPages(root: string): ConstraintPage[] {
	return CONSTRAINT_PAGES.map((page) => {
		const text = readFileSync(join(root, page), 'utf8')
		const { constraints } = readConstraints(page, text)
		const meta = frontmatter(text)
		return {
			page,
			title: meta.title || page,
			description: meta.description,
			count: constraints.length,
			prefix: constraints[0]?.id.replace(/-\d{3}$/, '') ?? '',
		}
	})
}

function numbered(root: string, directory: string, pattern: RegExp): Numbered[] {
	return readdirSync(join(root, directory))
		.flatMap((name) => {
			const number = pattern.exec(name)?.[1]
			return number ? [{ name, number }] : []
		})
		.sort((a, b) => b.number.localeCompare(a.number))
		.map(({ name, number }) => {
			const path = `${directory}/${name}`
			const text = readFileSync(join(root, path), 'utf8')
			return { path, number, text, meta: frontmatter(text) }
		})
}

/** Every input the index is built from. */
export function collect(root = ROOT): Inputs {
	return {
		areas: areas(root),
		pages: constraintPages(root),
		decisions: numbered(root, DECISIONS, /^ADR-(\d{4})-.+\.md$/),
		lessons: numbered(root, LESSONS, /^(\d{4})-.+\.md$/),
	}
}

/** The index, as markdown. */
export function render({ areas, pages, decisions, lessons }: Inputs): string {
	const lines = [
		'---',
		'title: Specification index',
		'description: Generated index of every feature area, constraint page, decision record, and lesson in the specification.',
		'type: readme',
		'---',
		'',
		'# Specification index',
		'',
		'> **Generated file — do not edit by hand.**',
		'> Regenerate with `node tools/spec/generate-spec-index.ts`. CI fails on a difference',
		'> ([ADR-0183](decisions/ADR-0183-the-specification-lives-in-a-spec-directory.md)).',
		'',
		'The authority rules, the product contract, and the simplicity guardrails are',
		'in [`features/README.md`](features/README.md). Every claim and constraint, with',
		'what verifies it, is in [`traceability.md`](traceability.md); the step definitions',
		'that bind each claim, in [`bindings.md`](bindings.md).',
		'',
		'## Feature areas',
		'',
		'Each area is one `.feature` file of scenarios and a supporting page with the',
		'detail Gherkin cannot hold, including what not to build.',
		'',
		'| Area | Claims | Scenarios | Planned (`@ignore`) | Browser (`@ui`) | Supporting detail |',
		'|---|---|---|---|---|---|',
	]
	for (const area of areas) {
		const detail = area.readme ? `[README](${link(area.readme)}) — ${cell(area.description)}` : '—'
		lines.push(`| [${cell(area.title)}](${link(area.feature)}) | \`${area.prefix}\` | ${area.scenarios} | ${area.planned} | ${area.browser} | ${detail} |`)
	}

	lines.push(
		'',
		'## Constraint pages',
		'',
		'Each normative constraint carries a `CON-*` ID naming the claims that verify it.',
		'',
		'| Page | Constraints | Count | Description |',
		'|---|---|---|---|',
	)
	for (const page of pages) {
		lines.push(`| [${cell(page.title)}](${link(page.page)}) | \`${page.prefix}\` | ${page.count} | ${cell(page.description)} |`)
	}

	lines.push(
		'',
		'## Decisions',
		'',
		'Architecture decision records, newest first. What an ADR is for:',
		'[`decisions/README.md`](decisions/README.md).',
		'',
		'| ADR | Title | Status | Date |',
		'|---|---|---|---|',
	)
	for (const { path, number, meta } of decisions) {
		lines.push(`| [${number}](${link(path)}) | ${cell(meta.title)} | ${cell(meta.status)} | ${cell(meta.date)} |`)
	}

	lines.push(
		'',
		'## Lessons',
		'',
		'What the specification should have said, newest first. When to write one:',
		'[`lessons/README.md`](lessons/README.md).',
		'',
		'| Lesson | Title | What it cost us | Remedy | Issue | Date | Status |',
		'|---|---|---|---|---|---|---|',
	)
	for (const { path, number, text, meta } of lessons) {
		const issue = /^\d+$/.test(meta.issue ?? '') ? `#${meta.issue}` : cell(meta.issue)
		lines.push(`| [${number}](${link(path)}) | ${cell(meta.title)} | ${cell(meta.description)} | ${remedy(text)} | ${issue} | ${cell(meta.date)} | ${cell(meta.status)} |`)
	}

	lines.push('')
	return lines.join('\n')
}

/** The whole-tree counts, reported rather than committed (ADR-0106). */
export function totals({ areas, pages, decisions, lessons }: Inputs): string {
	const scenarios = areas.reduce((sum, area) => sum + area.scenarios, 0)
	const constraints = pages.reduce((sum, page) => sum + page.count, 0)
	return `${areas.length} areas (${scenarios} scenarios), ${pages.length} constraint pages (${constraints} constraints), ${decisions.length} decisions, ${lessons.length} lessons.`
}

/**
 * Builds the index and writes or checks it, reporting the way the command line
 * does without exiting, so a test can exercise every outcome. Returns the exit
 * code.
 */
export function main(root = ROOT, { check = false }: { check?: boolean } = {}): number {
	const inputs = collect(root)
	const index = render(inputs)
	const target = join(root, SPEC_INDEX)

	if (check) {
		const committed = existsSync(target) ? readFileSync(target, 'utf8') : ''
		if (committed !== index) {
			console.error(`::error file=${SPEC_INDEX}::Out of date. Run 'node tools/spec/generate-spec-index.ts' and commit the result; on a same-repo pull request traceability.yml commits it for you (ADR-0183).`)
			return 1
		}
		console.log(`${SPEC_INDEX} matches the specification. ${totals(inputs)}`)
		return 0
	}

	writeFileSync(target, index)
	console.log(`${SPEC_INDEX} written. ${totals(inputs)}`)
	if (process.env.GITHUB_STEP_SUMMARY) appendFileSync(process.env.GITHUB_STEP_SUMMARY, `**Specification:** ${totals(inputs)}\n`)
	return 0
}

const runAsCommand = isMain(import.meta.url)
if (runAsCommand) process.exit(main(ROOT, { check: process.argv.includes('--check') }))
