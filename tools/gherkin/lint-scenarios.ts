#!/usr/bin/env node
// Scenarios describe behavior, not implementation (CONV-004, #815).
//
// Parses every .spec/features/<area>/<area>.feature with the pinned
// @cucumber/gherkin parser and runs each rule below over it. A rule reads the
// parsed scenario, so a later rule can judge its shape (the number of When
// steps, the number of steps) as easily as its words.
//
// What the word rules read, per scenario:
//   title    the Feature, Rule, Background, and Scenario names;
//   step     every step's text;
//   example  each Examples body cell whose column a step or the title reads
//            through its <placeholder>: the step says it, so it is step text.
// Not read: tags, comments, descriptions, doc strings, a step's own data
// table, and an Examples column no step reads.
//
//   node tools/gherkin/lint-scenarios.ts
//
// The exit code is the contract.
import { existsSync, readFileSync, readdirSync, statSync } from 'node:fs'
import { join } from 'node:path'
import { AstBuilder, GherkinClassicTokenMatcher, Parser } from '@cucumber/gherkin'
import { IdGenerator, type Background, type Feature, type Scenario, type Step } from '@cucumber/messages'

import { FEATURES } from '../spec/spec-paths.ts'
import { isMain } from '../lib/actions.ts'

const ROOT = process.cwd()

/** Where a piece of text sits in a scenario. */
export type Kind = 'title' | 'step' | 'example'

/** One piece of text a word rule reads. */
export interface Text {
	kind: Kind
	line: number
	text: string
	/** An Examples cell every step reads inside "double quotes": a literal value, as quoted text is. */
	quoted?: boolean
}

/** One scenario (or Background) as the rules see it. */
export interface Unit {
	area: string
	file: string
	/** The Scenario, Scenario Outline, or Background name, for messages. */
	name: string
	line: number
	keyword: string
	steps: readonly Step[]
	texts: Text[]
}

/** One rule broken. */
export interface Violation {
	rule: string
	file: string
	/** The line of the Scenario or Background the violation is in. */
	scenario: number
	line: number
	found: string
	message: string
}

/** A rule: an ID, the one-line reason it gives, and the check. */
export interface Rule {
	id: string
	message: string
	check: (unit: Unit) => Array<{ line: number; found: string }>
}

/** Blank out what a word rule never reads: double-quoted interface copy. */
export function stripQuotes(text: string): string {
	return text.replace(/"[^"]*"/g, ' ').replace(/“[^”]*”/g, ' ')
}

/**
 * A rule that fails any match of `patterns` in the texts of `kinds`. Quoted
 * "interface copy" is skipped unless `quotes` is true; a `code span` never
 * is, because an identifier in backticks is still an identifier.
 */
export function wordRule(id: string, message: string, patterns: readonly RegExp[], { kinds = ['title', 'step', 'example'] as readonly Kind[], quotes = false } = {}): Rule {
	return {
		id,
		message,
		check: (unit) =>
			unit.texts
				.filter((text) => kinds.includes(text.kind) && (quotes || !text.quoted))
				.flatMap(({ line, text }) => {
					const read = quotes ? text : stripQuotes(text)
					return patterns.flatMap((pattern) => [...read.matchAll(new RegExp(pattern.source, pattern.flags.includes('g') ? pattern.flags : `${pattern.flags}g`))].map((match) => ({ line, found: match[0] })))
				}),
	}
}

/**
 * The HTTP status codes a step might name in place of an outcome. A number
 * followed by a unit ("250 MB", "501 characters") is a size, not a status.
 */
const HTTP_STATUS = /\b(?:20[0-46]|30[1-478]|40[0-9]|41[0-8]|42[2-9]|43[01]|451|50[0-4])\b(?!\s*(?:MB|KB|GB|bytes?|characters?|pixels?|px|ms|seconds?|minutes?|%))/

/**
 * Proper names that are written in camel or Pascal case but name a product or
 * a key on the keyboard, not a stored identifier.
 */
const PROPER_NAMES = ['CloudFront', 'QuickTime', 'YouTube', 'WhatsApp', 'JavaScript', 'TypeScript', 'GitHub', 'OpenAI', 'DeepL', 'PowerPoint', 'OpenDocument', 'LibreOffice', 'iPhone', 'iPad', 'iOS', 'macOS', 'ArrowUp', 'ArrowDown', 'ArrowLeft', 'ArrowRight', 'PageUp', 'PageDown']

export const RULES: readonly Rule[] = [
	wordRule('no-http-status', 'say the outcome in the glossary\'s words ("is refused as forbidden"), not as a status code; the code is asserted in the step definition', [HTTP_STATUS]),
	wordRule('no-storage-identifiers', 'name what the reader knows, not a table, column, enum, setting, or wire type; the identifier lives in the step definition and the CON claim', [
		// snake_case: two or more lowercase words joined by underscores.
		/\b[a-z][a-z0-9]*(?:_[a-z0-9]+)+\b/,
		// camelCase or PascalCase with two or more humps: AiSummaryEn, allowFutureDates.
		new RegExp(`\\b(?!(?:${PROPER_NAMES.join('|')})\\b)(?:[a-z]+|[A-Z][a-z0-9]+)(?:[A-Z][a-z0-9]+)+\\b`),
		// A configuration key: Translation:Model.
		/\b[A-Z]\w*:[A-Z]\w*\b/,
		/\b(?:Postgres(?:QL)?|SQL|columns?|join tables?|enums?)\b/i,
		/\b(?:database|table|immutability) triggers?\b/i,
		/\b(?:outbox(?:es)?|DTOs?|booleans?)\b/i,
		/\bJSON\b/,
	]),
	wordRule('no-transport-terms', 'say what happened to the request, not how it travelled; "the API" is not a step\'s subject', [
		/\bAPI\b/,
		/\bHTTPS?\b/,
		/\b(?:GET|POST|PUT|PATCH|DELETE|HEAD|OPTIONS)\b/,
		/\bend-?points?\b/i,
		/\b(?:request|response) bod(?:y|ies)\b/i,
		/\bquery strings?\b/i,
		/\b(?:request|response|HTTP|origin-secret) headers?\b/i,
		/\bheader\s+[A-Z][\w-]*-[\w-]+/,
		/\bX-[A-Z][\w-]+/,
		/\bhttps?:\/\/\S+/i,
		/(?<![\w.-])\/(?:api|admin|reports?)\b[\w\-{}/.<>]*/,
	]),
	wordRule('no-rationale', 'say what happens, not why; the reason belongs in the area README', [/\b(?:because|instead of|rather than|so that|in order to)\b/i, /,\s*since\b/i], { kinds: ['title', 'step'] }),
	wordRule('no-locale-codes', 'say English or French; a locale code belongs only in an Examples cell', [/\b(?:en|fr)-CA\b/], { kinds: ['title', 'step'] }),
]

/** The parsed feature of one file, or the parse error. */
export function parse(source: string): Feature | undefined {
	const parser = new Parser(new AstBuilder(IdGenerator.uuid()), new GherkinClassicTokenMatcher())
	return parser.parse(source).feature
}

/**
 * The placeholders a scenario's title and steps read, `<name>`, each mapped to
 * whether every use of it sits inside "double quotes".
 */
export function placeholders(scenario: Pick<Scenario, 'name' | 'steps'>): Map<string, boolean> {
	const read = new Map<string, boolean>()
	for (const text of [scenario.name, ...scenario.steps.map((step) => step.text)]) {
		const quoted = new Set([...text.matchAll(/"[^"]*"|“[^”]*”/g)].flatMap((match) => [...match[0].matchAll(/<([^>]+)>/g)].map((inner) => inner[1])))
		for (const match of text.matchAll(/<([^>]+)>/g)) read.set(match[1], (read.get(match[1]) ?? true) && quoted.has(match[1]))
	}
	return read
}

/** One scenario or Background, with every text its rules read. */
function unit(area: string, file: string, item: Scenario | Background, titles: readonly Text[]): Unit {
	const texts: Text[] = [...titles, { kind: 'title', line: item.location.line, text: item.name }]
	for (const step of item.steps) texts.push({ kind: 'step', line: step.location.line, text: step.text })
	if ('examples' in item) {
		const read = placeholders(item)
		for (const examples of item.examples) {
			const header = examples.tableHeader?.cells.map((cell) => cell.value) ?? []
			for (const row of examples.tableBody) {
				row.cells.forEach((cell, index) => {
					const quoted = read.get(header[index])
					if (quoted !== undefined) texts.push({ kind: 'example', line: row.location.line, text: cell.value, quoted })
				})
			}
		}
	}
	return { area, file, name: item.name, line: item.location.line, keyword: item.keyword, steps: item.steps, texts }
}

/** Every scenario and Background of a feature, Rule blocks included. */
export function units(area: string, file: string, feature: Feature): Unit[] {
	const found: Unit[] = []
	const featureTitle: Text = { kind: 'title', line: feature.location.line, text: feature.name }
	for (const child of feature.children) {
		if (child.background) found.push(unit(area, file, child.background, [featureTitle]))
		if (child.scenario) found.push(unit(area, file, child.scenario, [featureTitle]))
		if (child.rule) {
			const ruleTitle: Text = { kind: 'title', line: child.rule.location.line, text: child.rule.name }
			for (const inner of child.rule.children) {
				if (inner.background) found.push(unit(area, file, inner.background, [featureTitle, ruleTitle]))
				if (inner.scenario) found.push(unit(area, file, inner.scenario, [featureTitle, ruleTitle]))
			}
		}
	}
	// The Feature and Rule names repeat into every unit; report each once.
	const seen = new Set<number>()
	for (const item of found) {
		item.texts = item.texts.filter((text) => text.kind !== 'title' || text.line === item.line || !seen.has(text.line))
		for (const text of item.texts) if (text.kind === 'title') seen.add(text.line)
	}
	return found
}

/** Every violation of `rules` in one feature file's source. */
export function lintSource(area: string, file: string, source: string, rules: readonly Rule[] = RULES): Violation[] {
	const feature = parse(source)
	if (!feature) return []
	const violations: Violation[] = []
	for (const item of units(area, file, feature)) {
		for (const rule of rules) {
			for (const { line, found } of rule.check(item)) violations.push({ rule: rule.id, file, scenario: item.line, line, found, message: rule.message })
		}
	}
	return violations.sort((a, b) => a.line - b.line || a.rule.localeCompare(b.rule))
}

/** Every violation in every area's feature file under `root`. */
export function lintScenarios(root = ROOT, rules: readonly Rule[] = RULES): Violation[] {
	const features = join(root, FEATURES)
	const violations: Violation[] = []
	for (const area of readdirSync(features).sort()) {
		if (!statSync(join(features, area)).isDirectory()) continue
		const file = `${FEATURES}/${area}/${area}.feature`
		if (existsSync(join(root, file))) violations.push(...lintSource(area, file, readFileSync(join(root, file), 'utf8'), rules))
	}
	return violations
}

export function main(root = ROOT, log: (line: string) => void = console.log): number {
	const violations = lintScenarios(root)
	for (const { rule, file, line, found, message } of violations) log(`::error file=${file},line=${line}::${rule}: "${found}": ${message}`)
	if (violations.length > 0) {
		const counts = RULES.map((rule) => `${rule.id} ${violations.filter((violation) => violation.rule === rule.id).length}`).join(', ')
		log(`${violations.length} scenario lint violation(s): ${counts}.`)
		return 1
	}
	log(`The scenarios keep to ${RULES.map((rule) => rule.id).join(', ')}.`)
	return 0
}

if (isMain(import.meta.url)) process.exit(main())
