// Which step definitions bind each claim, read from the specification and the
// step definitions that execute it (ADR-0184). tools/spec/generate-traceability.ts
// writes what this finds into .spec/claims.json and the matrix.
//
// It mirrors the two runners rather than running them:
//   - Reqnroll (C#, every claim not tagged @ui) matches by keyword, reads a
//     pattern as a regex or a Cucumber Expression by its own rule, and prefers
//     a binding whose class is scoped to the feature;
//   - playwright-bdd (TypeScript, @ui claims) matches by text alone.
//
// The specification is the authority. A built claim with a step no binding
// matches fails the generator; a step definition no claim uses, a step two
// bindings match, and an @ignore claim whose steps are all bound are recorded,
// not failed.
//
// Dependency-free, like every other tool in this directory: traceability.yml
// runs the base branch's copy with no npm install. Step files are read as text
// and their patterns compiled as regular expressions; nothing in them is
// executed.
import { existsSync, readFileSync, readdirSync, statSync } from 'node:fs'
import { join, posix } from 'node:path'

import { FEATURES, PLAYWRIGHT_STEPS, REQNROLL_STEPS } from './spec-paths.ts'
import { type Claim, readClaims } from './read-claims.ts'
import { errorMessage } from '../lib/actions.ts'

const STEP = /^\s*(Given|When|Then|And|But|\*)\s+(.*?)\s*$/
const BLOCK = /^\s*(Feature|Background|Rule|Scenario|Scenario Outline|Scenario Template|Example|Examples|Scenarios):\s*(.*?)\s*$/

/** One step as its runner executes it: And/But already resolved to a real keyword. */
export interface Step {
	keyword: string
	text: string
	line: number
}

interface ExamplesTable {
	header: string[] // empty until the first row of the table is read
	rows: string[][]
}

interface ScenarioDraft {
	name: string
	rule: string | null
	line: number
	background: Step[]
	own: Step[]
	examples: ExamplesTable[]
}

/** A claim with the steps its scenario runs. */
export interface ReadScenario extends Claim {
	rule: string | null
	line: number
	steps: Step[]
}

/**
 * Every scenario in a feature file with the steps its runner executes:
 * Background steps first, And/But resolved to the keyword before them, and an
 * outline expanded once per Examples row. Claim facts (ID, area, engine,
 * status) come from readClaims, so they keep exactly one definition.
 */
export function readScenarios(path: string, source: string): { feature: string; scenarios: ReadScenario[]; problems: string[] } {
	const problems: string[] = []
	const lines = source.split('\n')
	let feature = ''
	let rule: string | null = null
	const featureBackground: Step[] = []
	let ruleBackground: Step[] = []
	const scenarios: ScenarioDraft[] = []

	let block: 'background' | 'scenario' | 'examples' | null = null
	let target: Step[] | null = null // the step list being filled
	let previous: string | null = null // the keyword And/But inherit
	let current: ScenarioDraft | null = null // the scenario being read
	let examples: ExamplesTable = { header: [], rows: [] } // the Examples table being read
	let docString: string | null = null

	for (const [index, raw] of lines.entries()) {
		const line = raw.trim()
		if (docString) {
			if (line.startsWith(docString)) docString = null
			continue
		}
		if (line.startsWith('"""') || line.startsWith('```')) {
			docString = line.slice(0, 3)
			continue
		}
		if (line === '' || line.startsWith('#') || line.startsWith('@')) continue

		const heading = line.match(BLOCK)
		if (heading) {
			const [, keyword, title] = heading
			if (keyword === 'Feature') {
				feature = title
				block = null
			} else if (keyword === 'Rule') {
				rule = title
				ruleBackground = []
				block = null
			} else if (keyword === 'Background') {
				block = 'background'
				target = rule === null ? featureBackground : ruleBackground
				previous = null
			} else if (keyword === 'Examples' || keyword === 'Scenarios') {
				if (!current) problems.push(`${path}:${index + 1}: Examples outside a scenario`)
				examples = { header: [], rows: [] }
				current?.examples.push(examples)
				block = 'examples'
			} else {
				current = { name: title, rule, line: index + 1, background: [...featureBackground, ...ruleBackground], own: [], examples: [] }
				scenarios.push(current)
				block = 'scenario'
				target = current.own
				previous = null
			}
			continue
		}

		if (line.startsWith('|')) {
			if (block === 'examples') {
				const cells = line.slice(1, line.endsWith('|') ? -1 : undefined).split('|').map((cell) => cell.trim())
				if (examples.header.length === 0) examples.header = cells
				else examples.rows.push(cells)
			}
			// Any other table is a step argument; the binding takes it as a
			// parameter, so it plays no part in matching.
			continue
		}

		const step = line.match(STEP)
		if (step && (block === 'background' || block === 'scenario')) {
			const [, stepKeyword, text] = step
			let keyword = stepKeyword
			if (keyword === 'And' || keyword === 'But' || keyword === '*') {
				if (previous === null) {
					problems.push(`${path}:${index + 1}: "${keyword} ${text}" has no step before it to take its keyword from`)
					continue
				}
				keyword = previous
			}
			previous = keyword
			target?.push({ keyword, text, line: index + 1 })
		}
	}

	const { claims, problems: claimProblems } = readClaims(path, source)
	problems.push(...claimProblems)
	const claimed = scenarios.filter((scenario) => claims.some((claim) => claim.scenario === scenario.name))
	if (claimed.length !== claims.length) {
		problems.push(`${path}: ${claims.length} claim(s) but ${claimed.length} scenario(s) read with steps — the two parsers disagree`)
	}

	const result = claims.map((claim, position): ReadScenario => {
		const scenario = claimed.at(position)
		if (!scenario || scenario.name !== claim.scenario) {
			problems.push(`${path}: claim ${claim.id} "${claim.scenario}" does not line up with a scenario`)
			return { ...claim, rule: null, line: 0, steps: [] }
		}
		return { ...claim, rule: scenario.rule, line: scenario.line, steps: expand(scenario) }
	})

	return { feature, scenarios: result, problems }
}

/** A scenario's steps; an outline's once per Examples row, deduplicated. */
function expand(scenario: ScenarioDraft): Step[] {
	const rows = scenario.examples.flatMap((table) =>
		table.rows.map((cells) => Object.fromEntries(table.header.map((name, at) => [name, cells[at] ?? '']))),
	)
	const steps: Step[] = []
	const seen = new Set<string>()
	const add = (step: Step): void => {
		const key = `${step.keyword} ${step.text}`
		if (!seen.has(key)) {
			seen.add(key)
			steps.push(step)
		}
	}
	for (const step of scenario.background) add(step)
	if (rows.length === 0) {
		for (const step of scenario.own) add(step)
	} else {
		for (const row of rows) {
			for (const step of scenario.own) {
				const text = step.text.replace(/<([^<>]+)>/g, (match: string, name: string) => (name in row ? row[name] : match))
				add({ ...step, text })
			}
		}
	}
	// A <name> that is not a column stays as written, as both runners leave it.
	return steps
}

// ---------------------------------------------------------------- C# steps --

const CLASS = /^\s*(?:(?:public|internal|private|protected|sealed|static|abstract|partial|file)\s+)*class\s+(\w+)/
const STEP_ATTRIBUTE = /\[(Given|When|Then)\(@"((?:[^"]|"")*)"\)\]/g
const ANY_STEP_ATTRIBUTE = /\[(Given|When|Then|StepDefinition)\(/g

/** A step definition in a C# source, read by Reqnroll. */
export interface ReqnrollBinding {
	engine: 'Reqnroll'
	keyword: string
	pattern: string
	file: string
	line: number
	scopes: Set<string>
}

/** A regular-expression step pattern, kept as source and flags. */
export interface RegExpPattern {
	source: string
	flags: string
}

/** A step definition in a TypeScript source, read by playwright-bdd. */
export interface PlaywrightBinding {
	engine: 'playwright-bdd'
	keyword: string
	pattern: string | RegExpPattern
	file: string
	line: number
}

export type Binding = ReqnrollBinding | PlaywrightBinding

/**
 * Every Reqnroll step definition in the given C# sources, with its class's
 * [Scope(Feature = …)] values. A partial class's scope, declared in one file,
 * applies to its steps in every file.
 */
export function readReqnrollBindings(sources: readonly { path: string; source: string }[]): { bindings: ReqnrollBinding[]; problems: string[] } {
	const problems: string[] = []
	const bindingClasses = new Set<string>()
	const scopes = new Map<string, Set<string>>()

	for (const { path, source } of sources) {
		let buffer: { text: string; line: number }[] = []
		for (const [index, line] of source.split('\n').entries()) {
			const trimmed = line.trim()
			if (trimmed.startsWith('[')) {
				buffer.push({ text: trimmed, line: index + 1 })
				continue
			}
			if (trimmed === '' || trimmed.startsWith('//')) continue
			const declared = line.match(CLASS)
			if (declared) {
				const attributes = buffer.map((entry) => entry.text).join(' ')
				if (/\bBinding\b/.test(attributes)) {
					bindingClasses.add(declared[1])
					const features = [...attributes.matchAll(/\bScope\(\s*Feature\s*=\s*"([^"]*)"\s*\)/g)].map((match) => match[1])
					let declaredScopes = scopes.get(declared[1])
					if (!declaredScopes) {
						declaredScopes = new Set()
						scopes.set(declared[1], declaredScopes)
					}
					for (const feature of features) declaredScopes.add(feature)
				}
			} else {
				for (const entry of buffer) {
					if (/\bScope\(/.test(entry.text)) problems.push(`${path}:${entry.line}: a method-level [Scope] is not supported — scope the class`)
				}
			}
			for (const entry of buffer) {
				if (/\bScope\(\s*(Tag|Scenario)\s*=/.test(entry.text)) problems.push(`${path}:${entry.line}: [Scope(Tag|Scenario = …)] is not supported — scope by Feature`)
			}
			buffer = []
		}
	}

	const bindings: ReqnrollBinding[] = []
	for (const { path, source } of sources) {
		let current: string | null = null
		for (const [index, line] of source.split('\n').entries()) {
			const declared = line.match(CLASS)
			if (declared && bindingClasses.has(declared[1])) current = declared[1]

			const found = [...line.matchAll(STEP_ATTRIBUTE)]
			const all = [...line.matchAll(ANY_STEP_ATTRIBUTE)]
			if (all.length !== found.length) {
				problems.push(`${path}:${index + 1}: a step attribute this tool cannot read — write it as [Given(@"…")] on one line`)
			}
			for (const [, keyword, escaped] of found) {
				if (current === null) {
					problems.push(`${path}:${index + 1}: a step definition outside a [Binding] class`)
					continue
				}
				bindings.push({
					engine: 'Reqnroll',
					keyword,
					pattern: escaped.replace(/""/g, '"'),
					file: path,
					line: index + 1,
					scopes: scopes.get(current) ?? new Set(),
				})
			}
		}
	}

	return { bindings, problems }
}

// ---------------------------------------------------------------- TS steps --

const TS_STEP = /^[ \t]*(Given|When|Then)\(\s*(?:"((?:[^"\\\n]|\\.)*)"|'((?:[^'\\\n]|\\.)*)'|\/((?:[^/\\\n[]|\\.|\[(?:[^\]\\\n]|\\.)*\])+)\/([dgimsuvy]*))\s*,/gm
const TS_ANY_STEP = /^[ \t]*(Given|When|Then)\(/gm

/** A JavaScript string literal's body, unescaped by hand — never evaluated. */
export function unescapeString(body: string): string {
	const simple: Partial<Record<string, string>> = { n: '\n', t: '\t', r: '\r', 0: '\0', b: '\b', f: '\f', v: '\v' }
	return body.replace(/\\(.)/g, (_match: string, character: string) => simple[character] ?? character)
}

/** Every playwright-bdd step definition in one TypeScript source. */
export function readPlaywrightBindings(path: string, source: string): { bindings: PlaywrightBinding[]; problems: string[] } {
	const problems: string[] = []
	const bindings: PlaywrightBinding[] = []
	const created = /const\s*\{([^}]*)\}\s*=\s*createBdd\(/.exec(source)
	if (!created) {
		if (TS_ANY_STEP.test(source)) problems.push(`${path}: steps are declared without "const { Given, When, Then } = createBdd()"`)
		TS_ANY_STEP.lastIndex = 0
		return { bindings, problems }
	}
	if (/:/.test(created[1])) problems.push(`${path}: createBdd() is destructured with an alias — this tool reads Given, When, and Then by name`)

	const lineAt = (offset: number): number => source.slice(0, offset).split('\n').length
	for (const match of source.matchAll(TS_STEP)) {
		const keyword = match[1]
		const groups: (string | undefined)[] = match.slice(2)
		const [double, single, regexSource, flags] = groups
		const line = lineAt(match.index + match[0].indexOf(keyword))
		if (regexSource !== undefined) bindings.push({ engine: 'playwright-bdd', keyword, pattern: { source: regexSource, flags: flags ?? '' }, file: path, line })
		else bindings.push({ engine: 'playwright-bdd', keyword, pattern: unescapeString(double ?? single ?? ''), file: path, line })
	}
	const declared = [...source.matchAll(TS_ANY_STEP)].length
	if (declared !== bindings.length) {
		problems.push(`${path}: ${declared} step definition(s) but ${bindings.length} read — write each pattern as a string or regex literal`)
	}
	return { bindings, problems }
}

// ------------------------------------------------------ Cucumber Expressions --

export class UnsupportedExpression extends Error {}

const PARAMETERS: Readonly<Record<string, string>> = {
	string: `("(?:[^"\\\\]*(?:\\\\.[^"\\\\]*)*)"|'(?:[^'\\\\]*(?:\\\\.[^'\\\\]*)*)')`,
	word: '([^\\s]+)',
	int: '((?:-?\\d+)|(?:\\d+))',
	'': '(.*)',
}

const escapeRegExp = (text: string): string => text.replace(/[.*+?^${}()|[\]\\/]/g, '\\$&')

/**
 * A Cucumber Expression as an anchored RegExp, following the official grammar
 * for the parts this repository uses: the parameter types string, word, int,
 * and the anonymous {}; optional text; alternation. Anything else throws, so a
 * new construct fails loudly instead of matching the wrong steps.
 */
interface Token {
	kind: 'text' | 'parameter' | 'optional' | 'space' | 'slash'
	value: string
}

export function cucumberToRegExp(expression: string): RegExp {
	// Tokens: text characters, whitespace, parameters, optionals, and slashes.
	const tokens: Token[] = []
	for (let at = 0; at < expression.length; at += 1) {
		const character = expression[at]
		if (character === '\\') {
			at += 1
			if (at >= expression.length) throw new UnsupportedExpression(`"${expression}" ends with a lone backslash`)
			tokens.push({ kind: 'text', value: expression[at] })
		} else if (character === '{') {
			const end = expression.indexOf('}', at)
			if (end === -1) throw new UnsupportedExpression(`"${expression}" opens a parameter it never closes`)
			const name = expression.slice(at + 1, end)
			if (!(name in PARAMETERS)) throw new UnsupportedExpression(`"${expression}" uses the parameter type {${name}}, which this tool does not support`)
			tokens.push({ kind: 'parameter', value: PARAMETERS[name] })
			at = end
		} else if (character === '(') {
			let text = ''
			let end = at + 1
			for (; end < expression.length && expression[end] !== ')'; end += 1) {
				if (expression[end] === '\\') {
					end += 1
					text += expression.charAt(end)
				} else if ('({/'.includes(expression[end])) {
					throw new UnsupportedExpression(`"${expression}" nests a parameter, optional, or alternative inside an optional`)
				} else {
					text += expression[end]
				}
			}
			if (end >= expression.length) throw new UnsupportedExpression(`"${expression}" opens an optional it never closes`)
			if (text === '') throw new UnsupportedExpression(`"${expression}" has an empty optional`)
			tokens.push({ kind: 'optional', value: text })
			at = end
		} else if (character === '/') {
			tokens.push({ kind: 'slash', value: '/' })
		} else if (/\s/.test(character)) {
			tokens.push({ kind: 'space', value: character })
		} else {
			tokens.push({ kind: 'text', value: character })
		}
	}

	// Alternation binds within a run of non-whitespace.
	let source = ''
	let run: Token[] = []
	const flush = (): void => {
		if (run.some((token) => token.kind === 'slash')) {
			const alternatives: Token[][] = [[]]
			for (const token of run) {
				if (token.kind === 'slash') alternatives.push([])
				else alternatives[alternatives.length - 1].push(token)
			}
			const parts = alternatives.map((alternative) => {
				if (alternative.length === 0) throw new UnsupportedExpression(`"${expression}" has an empty alternative`)
				if (alternative.some((token) => token.kind === 'parameter')) throw new UnsupportedExpression(`"${expression}" puts a parameter inside an alternative`)
				if (alternative.every((token) => token.kind === 'optional')) throw new UnsupportedExpression(`"${expression}" has an alternative that is only optional text`)
				return alternative.map(emit).join('')
			})
			source += `(?:${parts.join('|')})`
		} else {
			source += run.map(emit).join('')
		}
		run = []
	}
	const emit = (token: Token): string => {
		if (token.kind === 'text') return escapeRegExp(token.value)
		if (token.kind === 'parameter') return token.value
		return `(?:${escapeRegExp(token.value)})?`
	}
	for (const token of tokens) {
		if (token.kind === 'space') {
			flush()
			source += escapeRegExp(token.value)
		} else {
			run.push(token)
		}
	}
	flush()

	return new RegExp(`^${source}$`)
}

/**
 * How Reqnroll reads a step pattern: a regex when it is anchored or carries a
 * regex group; otherwise a Cucumber Expression. A regex is anchored at both
 * ends, as Reqnroll anchors it.
 */
export function reqnrollKind(pattern: string): 'regex' | 'cucumber' {
	if (pattern.startsWith('^') || pattern.endsWith('$')) return 'regex'
	if (/\{\w*\}/.test(pattern)) return 'cucumber'
	if (/(\([^)]+[*+]\)|\.\*)/.test(pattern)) return 'regex'
	return 'cucumber'
}

export function reqnrollMatcher(pattern: string): RegExp {
	if (reqnrollKind(pattern) === 'cucumber') return cucumberToRegExp(pattern)
	// Reqnroll anchors a regex at both ends, whichever end it already anchors.
	const inner = pattern.replace(/^\^/, '').replace(/(?<!\\)\$$/, '')
	return new RegExp(`^(?:${inner})$`)
}

export function playwrightMatcher(pattern: string | RegExpPattern): RegExp {
	if (typeof pattern === 'string') return cucumberToRegExp(pattern)
	return new RegExp(pattern.source, pattern.flags.replace(/[gy]/g, ''))
}

// ---------------------------------------------------------------- resolving --

/** Code-unit order, the same on every machine and locale. */
const compare = (a: string, b: string): number => Number(a > b) - Number(a < b)

/** A binding's pattern as written: a string, or a regex literal. */
export const display = (binding: Binding): string => (typeof binding.pattern === 'string' ? binding.pattern : `/${binding.pattern.source}/${binding.pattern.flags}`)

/** One feature file's scenarios, with its path and title. */
export interface FeatureScenarios {
	path: string
	feature: string
	scenarios: ReadScenario[]
}

/** A binding with its pattern compiled. */
type Compiled<B extends Binding> = B & { regex: RegExp }

/** One step with the files of the definitions that bind it. */
export interface BoundStep extends Step {
	files: string[]
	/** More than one definition matches it, which the runner fails. */
	ambiguous: boolean
}

/** A claim with the files that bind its steps and the steps nothing binds. */
export interface ResolvedClaim extends ReadScenario {
	path: string
	files: string[]
	unbound: Step[]
	bound: BoundStep[]
}

/** What `resolve` finds. */
export interface Resolution {
	claims: ResolvedClaim[]
	unused: Binding[]
	ambiguous: Map<string, Set<string>>
	problems: string[]
}

/**
 * Resolves every claim's steps to the bindings its runner would choose, and
 * reports what does not line up.
 */
export function resolve(features: readonly FeatureScenarios[], csBindings: readonly ReqnrollBinding[], tsBindings: readonly PlaywrightBinding[]): Resolution {
	const problems: string[] = []
	const compile = <B extends Binding>(bindings: readonly B[], matcher: (pattern: B['pattern']) => RegExp): Compiled<B>[] =>
		bindings.flatMap((binding) => {
			try {
				return [{ ...binding, regex: matcher(binding.pattern) }]
			} catch (error) {
				problems.push(`${binding.file}:${binding.line}: ${errorMessage(error)}`)
				return []
			}
		})
	const cs = compile(csBindings, reqnrollMatcher)
	const ts = compile(tsBindings, playwrightMatcher)

	const used = new Set<Binding>()
	const cache = new Map<string, Binding[]>()
	const ambiguous = new Map<string, Set<string>>()

	const candidates = (engine: Claim['engine'], feature: string, step: Step): Binding[] => {
		const key = `${engine}\u0000${feature}\u0000${step.keyword}\u0000${step.text}`
		const cached = cache.get(key)
		if (cached) return cached
		let found: Binding[]
		if (engine === 'playwright-bdd') {
			found = ts.filter((binding) => binding.regex.test(step.text))
		} else {
			const matching = cs.filter((binding) => binding.keyword === step.keyword && binding.regex.test(step.text))
			const allowed = matching.filter((binding) => binding.scopes.size === 0 || binding.scopes.has(feature))
			const scoped = allowed.filter((binding) => binding.scopes.size > 0)
			found = scoped.length > 0 ? scoped : allowed
		}
		cache.set(key, found)
		return found
	}

	const claims: ResolvedClaim[] = []
	for (const { feature, path, scenarios } of features) {
		for (const claim of scenarios) {
			const files = new Set<string>()
			const unbound: Step[] = []
			const bound: BoundStep[] = []
			for (const step of claim.steps) {
				const found = candidates(claim.engine, feature, step)
				bound.push({ ...step, files: [...new Set(found.map((binding) => binding.file))].sort(), ambiguous: found.length > 1 })
				if (found.length === 0) unbound.push(step)
				if (found.length > 1) {
					const label = `${step.keyword} ${step.text}`
					let labelled = ambiguous.get(label)
					if (!labelled) {
						labelled = new Set()
						ambiguous.set(label, labelled)
					}
					for (const binding of found) labelled.add(binding.file)
				}
				for (const binding of found) {
					used.add(binding)
					files.add(binding.file)
				}
			}
			claims.push({ ...claim, path, files: [...files].sort(), unbound, bound })
		}
	}

	const unused = [...cs, ...ts]
		.filter((binding) => !used.has(binding))
		.sort((a, b) => compare(a.file, b.file) || compare(display(a), display(b)))

	return { claims, unused, ambiguous, problems }
}

// ------------------------------------------------------------------- files --

function walk(root: string, directory: string, extension: string): string[] {
	const full = join(root, directory)
	if (!existsSync(full)) return []
	return readdirSync(full).flatMap((entry) => {
		if (entry === 'bin' || entry === 'obj' || entry === 'node_modules') return []
		const path = posix.join(directory, entry)
		if (statSync(join(root, path)).isDirectory()) return walk(root, path, extension)
		return path.endsWith(extension) ? [path] : []
	})
}

/** Every claim's steps resolved to the step definitions that bind them. */
export function collectBindings(root: string): Resolution {
	const problems: string[] = []
	const read = (path: string): string => readFileSync(join(root, path), 'utf8')

	const features = walk(root, FEATURES, '.feature')
		.sort()
		.map((path) => {
			const result = readScenarios(path, read(path))
			problems.push(...result.problems)
			return { path, feature: result.feature, scenarios: result.scenarios }
		})

	const cs = readReqnrollBindings(
		walk(root, REQNROLL_STEPS, '.cs')
			.sort()
			.map((path) => ({ path, source: read(path) })),
	)
	problems.push(...cs.problems)

	const ts = walk(root, PLAYWRIGHT_STEPS, '.ts')
		.sort()
		.flatMap((path) => {
			const result = readPlaywrightBindings(path, read(path))
			problems.push(...result.problems)
			return result.bindings
		})

	const resolved = resolve(features, cs.bindings, ts)
	problems.push(...resolved.problems)
	return { ...resolved, problems }
}
