#!/usr/bin/env node
// Scenarios describe behavior, not implementation (CONV-004), one behavior
// each (CONV-006, #815).
//
// Parses every .spec/features/<area>/<area>.feature with the pinned
// @cucumber/gherkin parser and runs each rule below over it. A rule reads the
// parsed scenario, so a later rule can judge its shape (the number of When
// steps, the number of steps) as easily as its words.
//
// What the word rules read, per scenario:
//   title        the Feature, Rule, Background, and Scenario names;
//   description  the free text under each of them, line by line, for the
//                rules that name it (no-transport-terms, no-rationale);
//   step         every step's text;
//   example  each Examples body cell whose column a step or the title reads
//            through its <placeholder>: the step says it, so it is step text.
// Not read: tags, comments, doc strings, a step's own data table, and an
// Examples column no step reads. Every .feature file under .spec/features is
// linted, its area being the directory that holds it.
//
//   node tools/gherkin/lint-scenarios.ts
//
// The exit code is the contract.
import { existsSync, readFileSync, readdirSync, statSync } from 'node:fs'
import { basename, dirname, join } from 'node:path'
import { AstBuilder, GherkinClassicTokenMatcher, Parser } from '@cucumber/gherkin'
import { IdGenerator, StepKeywordType, type Background, type Feature, type Scenario, type Step } from '@cucumber/messages'

import { FEATURES } from '../spec/spec-paths.ts'
import { isMain } from '../lib/actions.ts'

const ROOT = process.cwd()

/** Where a piece of text sits in a scenario. */
export type Kind = 'title' | 'description' | 'step' | 'example'

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

/** The most steps a scenario may have, its Background not counted (J15). */
export const MAX_STEPS = 8

/** Whether a unit is a Background, which the shape rules do not judge. */
function isBackground(unit: Unit): boolean {
	return unit.keyword.trim() === 'Background'
}

/**
 * At most one action step, and no action after an outcome (J14). A When is an
 * action, and an And or But takes the type of the step before it; a scenario
 * with no When at all is allowed.
 */
export function oneWhen(unit: Unit): Array<{ line: number; found: string }> {
	if (isBackground(unit)) return []
	const found: Array<{ line: number; found: string }> = []
	let type = StepKeywordType.CONTEXT
	let actions = 0
	let outcome = false
	for (const step of unit.steps) {
		if (step.keywordType !== undefined && step.keywordType !== StepKeywordType.CONJUNCTION && step.keywordType !== StepKeywordType.UNKNOWN) type = step.keywordType
		if (type === StepKeywordType.OUTCOME) outcome = true
		if (type !== StepKeywordType.ACTION) continue
		actions += 1
		if (outcome) found.push({ line: step.location.line, found: `${step.keyword.trim()} ${step.text} (after a Then)` })
		else if (actions > 1) found.push({ line: step.location.line, found: `${step.keyword.trim()} ${step.text} (action ${actions})` })
	}
	return found
}

/**
 * How a browser is driven, which a step never says: pointer and keyboard
 * mechanics, element roles and selectors, CSS, and pixel sizes. Quoted
 * interface copy is skipped. An Examples cell a step reads is step text, so it
 * is held to the same patterns: it names an input ("the Escape key", "the
 * pointer outside the question"), never a verb ("pressing Escape").
 */
const UI_MECHANICS: readonly RegExp[] = [
	/\b(?:click|double-click|right-click|tap|hover|scroll|drag|press)(?:s|es|ed|ing)?\b/i,
	/\bkey(?:stroke|press)s?\b/i,
	/\btabs?\s+(?:to|through|past|into|out|away|back)\b|\btabb(?:ed|ing)\b/i,
	/\bmouse\b/i,
	/\b(?:aria|data)-[a-z][\w-]*/i,
	/\brole\s*=|\b(?:combobox|listbox|textbox|spinbutton|menuitem|tablist|tabpanel)(?:es|s)?\b/i,
	// DOM, but not a claim ID that happens to carry it (REQ-DOM-007).
	/(?<!-)\b(?:selector|DOM|CSS|class name|z-index|viewport)s?\b(?!-)/i,
	/\b\d+\s*(?:pixels?|px)\b|\bpixels?\b/i,
	// Typing as a verb ("types into", "the reporter types"), not the noun ("content type").
	/^types?\b(?!-)|\b(?:visitor|member|reporter|reviewer|officer|administrator|they|who)\s+types?\b|\b(?:has|have|had)\s+typed\b|\btyping\b|\btypes?\s+into\b|\btypes\s+in\b/i,
]

/**
 * A key's name, which only an Examples cell carries ("the Enter key", "the
 * down arrow key twice"): a step or a title reads it through a placeholder.
 */
const KEY_NAMES: readonly RegExp[] = [
	/\b(?:Tab|Enter|Escape|Esc|Space(?:bar)?|Home|End|Shift|Backspace|Delete key|ArrowUp|ArrowDown|ArrowLeft|ArrowRight|PageUp|PageDown|(?:up|down|left|right) arrow)\b/,
]

const UI_MESSAGE = 'say what the actor does or sees, not how the browser is driven; a key name belongs only in an Examples cell, as a noun phrase ("the Escape key")'
const UI_STEPS = wordRule('no-ui-mechanics', UI_MESSAGE, [...UI_MECHANICS, ...KEY_NAMES], { kinds: ['title', 'step'] })
const UI_CELLS = wordRule('no-ui-mechanics', UI_MESSAGE, UI_MECHANICS, { kinds: ['example'] })

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
		/\bAPI\b/i,
		/\bHTTPS?\b/,
		/\b(?:GET|POST|PUT|PATCH|DELETE|HEAD|OPTIONS)\b/,
		/\bend-?points?\b/i,
		/\b(?:request|response) bod(?:y|ies)\b/i,
		/\bquery strings?\b/i,
		/\b(?:request|response|HTTP|origin-secret) headers?\b/i,
		/\bheader\s+[A-Z][\w-]*-[\w-]+/,
		/\bX-[A-Z][\w-]+/,
		/\bhttps?:\/\/\S+/i,
		// A route or path: /report, /api/v1/reports. A slash inside a word (yes/no) is not one.
		/(?<![\w.:/<-])\/[a-z][\w\-{}/.<>]*/,
	], { kinds: ['title', 'description', 'step', 'example'] }),
	wordRule('no-rationale', 'say what happens, not why; the reason belongs in the area README', [/\b(?:because|instead of|rather than|so that|in order to)\b/i, /,\s*since\b/i], { kinds: ['title', 'description', 'step', 'example'] }),
	wordRule('no-locale-codes', 'say English or French; a locale code belongs only in an Examples cell', [/\b(?:en|fr)-CA\b/], { kinds: ['title', 'step'] }),
	{
		id: 'one-when',
		message: 'one behavior per scenario: at most one action (a When and the And/But steps that continue it), never after a Then; split the rest into their own scenarios',
		check: oneWhen,
	},
	{
		id: 'max-steps',
		message: `at most ${MAX_STEPS} steps in a scenario, its Background not counted; split it or move the setup into one Given`,
		check: (unit) => (isBackground(unit) || unit.steps.length <= MAX_STEPS ? [] : [{ line: unit.line, found: `${unit.steps.length} steps` }]),
	},
	{ id: 'no-ui-mechanics', message: UI_MESSAGE, check: (unit) => [...UI_STEPS.check(unit), ...UI_CELLS.check(unit)] },
]

/** The parsed feature of one file; a syntax error throws. */
export function parse(source: string): Feature | undefined {
	const parser = new Parser(new AstBuilder(IdGenerator.uuid()), new GherkinClassicTokenMatcher())
	return parser.parse(source).feature
}

/**
 * The placeholders a scenario's title and steps read, `<name>`, each mapped to
 * whether every use of it sits inside "double quotes".
 */
export function placeholders(scenario: { name: string; steps: ReadonlyArray<{ text: string }> }): Map<string, boolean> {
	const read = new Map<string, boolean>()
	for (const text of [scenario.name, ...scenario.steps.map((step) => step.text)]) {
		const quoted = new Set([...text.matchAll(/"[^"]*"|“[^”]*”/g)].flatMap((match) => [...match[0].matchAll(/<([^>]+)>/g)].map((inner) => inner[1])))
		for (const match of text.matchAll(/<([^>]+)>/g)) read.set(match[1], (read.get(match[1]) ?? true) && quoted.has(match[1]))
	}
	return read
}

/** The free text under a keyword, one Text per line; it starts on the line after the keyword's. */
function description(owner: { location: { line: number }; description: string }): Text[] {
	if (owner.description.trim() === '') return []
	return owner.description.split('\n').map((text, index) => ({ kind: 'description' as const, line: owner.location.line + 1 + index, text }))
}

/** One scenario or Background, with every text its rules read. */
function unit(area: string, file: string, item: Scenario | Background, titles: readonly Text[]): Unit {
	const texts: Text[] = [...titles, { kind: 'title', line: item.location.line, text: item.name }, ...description(item)]
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
	const featureTitle: Text[] = [{ kind: 'title', line: feature.location.line, text: feature.name }, ...description(feature)]
	for (const child of feature.children) {
		if (child.background) found.push(unit(area, file, child.background, featureTitle))
		if (child.scenario) found.push(unit(area, file, child.scenario, featureTitle))
		if (child.rule) {
			const ruleTitle: Text[] = [{ kind: 'title', line: child.rule.location.line, text: child.rule.name }, ...description(child.rule)]
			for (const inner of child.rule.children) {
				if (inner.background) found.push(unit(area, file, inner.background, [...featureTitle, ...ruleTitle]))
				if (inner.scenario) found.push(unit(area, file, inner.scenario, [...featureTitle, ...ruleTitle]))
			}
		}
	}
	// The Feature and Rule names and descriptions repeat into every unit; report each once.
	const shared = (text: Text) => text.kind === 'title' || text.kind === 'description'
	const seen = new Set<number>()
	for (const item of found) {
		item.texts = item.texts.filter((text) => !shared(text) || !seen.has(text.line))
		for (const text of item.texts) if (shared(text)) seen.add(text.line)
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

/** Every .feature file under `dir`, relative to `root`, sorted. */
function featureFiles(root: string, dir: string): string[] {
	return readdirSync(join(root, dir))
		.sort()
		.flatMap((entry) => {
			const path = `${dir}/${entry}`
			if (statSync(join(root, path)).isDirectory()) return featureFiles(root, path)
			return path.endsWith('.feature') ? [path] : []
		})
}

/** Every violation in every .feature file under `root`'s features; a file that does not parse is one violation. */
export function lintScenarios(root = ROOT, rules: readonly Rule[] = RULES): Violation[] {
	if (!existsSync(join(root, FEATURES))) return []
	const violations: Violation[] = []
	for (const file of featureFiles(root, FEATURES)) {
		const area = basename(dirname(file))
		try {
			violations.push(...lintSource(area, file, readFileSync(join(root, file), 'utf8'), rules))
		} catch (error) {
			violations.push({ rule: 'parse', file, scenario: 0, line: 0, found: '', message: `does not parse: ${(error as Error).message.split('\n')[0]}` })
		}
	}
	return violations
}

export function main(root = ROOT, log: (line: string) => void = console.log): number {
	const violations = lintScenarios(root)
	for (const { rule, file, line, found, message } of violations) log(`::error file=${file},line=${line}::${rule}: ${found === '' ? '' : `"${found}": `}${message}`)
	if (violations.length > 0) {
		const counts = RULES.map((rule) => `${rule.id} ${violations.filter((violation) => violation.rule === rule.id).length}`).join(', ')
		log(`${violations.length} scenario lint violation(s): ${counts}.`)
		return 1
	}
	log(`The scenarios keep to ${RULES.map((rule) => rule.id).join(', ')}.`)
	return 0
}

if (isMain(import.meta.url)) process.exit(main())
