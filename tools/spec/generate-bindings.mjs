#!/usr/bin/env node
// The step-bindings map, generated from the specification and the step
// definitions that execute it (ADR-0184).
//
// The traceability matrix says which claims exist; nothing said which test
// proves each one, so a claim was a dead end in the knowledge graph and the
// code was never checked against the specification beyond "the test ran".
// This resolves every scenario step to the step definition its runner would
// bind, and writes .spec/bindings.md: per claim, the files that bind it, plus
// the places where the specification and the code disagree.
//
// It mirrors the two runners rather than running them:
//   - Reqnroll (C#, every claim not tagged @ui) matches by keyword, reads a
//     pattern as a regex or a Cucumber Expression by its own rule, and prefers
//     a binding whose class is scoped to the feature;
//   - playwright-bdd (TypeScript, @ui claims) matches by text alone.
//
// The specification is the authority. A built claim with a step no binding
// matches fails; a step definition no claim uses, a step two bindings match,
// and an @ignore claim whose steps are all bound are listed, not failed.
//
// Dependency-free, like traceability.mjs: traceability.yml runs the base
// branch's copy with no npm install. Step files are read as text and their
// patterns compiled as regular expressions; nothing in them is executed.
//
//   node tools/spec/generate-bindings.mjs              write .spec/bindings.md; exit 1 on a gap
//   node tools/spec/generate-bindings.mjs --check      fail when the committed file differs
//   node tools/spec/generate-bindings.mjs --no-fail    write it, never fail (hooks and the bot)
//
// The exit code is the contract.
import { appendFileSync, existsSync, readFileSync, readdirSync, statSync, writeFileSync } from 'node:fs'
import { join, posix } from 'node:path'

import { BINDINGS, FEATURES, PLAYWRIGHT_STEPS, REQNROLL_STEPS, SPEC_ROOT } from './spec-paths.mjs'
import { readClaims } from './generate-traceability.mjs'

const ROOT = process.cwd()

const STEP = /^\s*(Given|When|Then|And|But|\*)\s+(.*?)\s*$/
const BLOCK = /^\s*(Feature|Background|Rule|Scenario|Scenario Outline|Scenario Template|Example|Examples|Scenarios):\s*(.*?)\s*$/

/**
 * Every scenario in a feature file with the steps its runner executes:
 * Background steps first, And/But resolved to the keyword before them, and an
 * outline expanded once per Examples row. Claim facts (ID, area, engine,
 * status) come from readClaims, so they keep exactly one definition.
 */
export function readScenarios(path, source) {
	const problems = []
	const lines = source.split('\n')
	let feature = ''
	let rule = null
	const featureBackground = []
	let ruleBackground = []
	const scenarios = []

	let block = null // 'background' | 'scenario' | 'examples'
	let target = null // the step list being filled
	let previous = null // the keyword And/But inherit
	let current = null // the scenario being read
	let examples = null // the Examples table being read
	let docString = null

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
				examples = { header: null, rows: [] }
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
				if (examples.header === null) examples.header = cells
				else examples.rows.push(cells)
			}
			// Any other table is a step argument; the binding takes it as a
			// parameter, so it plays no part in matching.
			continue
		}

		const step = line.match(STEP)
		if (step && (block === 'background' || block === 'scenario')) {
			let [, keyword, text] = step
			if (keyword === 'And' || keyword === 'But' || keyword === '*') {
				if (previous === null) {
					problems.push(`${path}:${index + 1}: "${keyword} ${text}" has no step before it to take its keyword from`)
					continue
				}
				keyword = previous
			}
			previous = keyword
			target.push({ keyword, text, line: index + 1 })
		}
	}

	const { claims, problems: claimProblems } = readClaims(path, source)
	problems.push(...claimProblems)
	const claimed = scenarios.filter((scenario) => claims.some((claim) => claim.scenario === scenario.name))
	if (claimed.length !== claims.length) {
		problems.push(`${path}: ${claims.length} claim(s) but ${claimed.length} scenario(s) read with steps — the two parsers disagree`)
	}

	const result = claims.map((claim, position) => {
		const scenario = claimed[position]
		if (!scenario || scenario.name !== claim.scenario) {
			problems.push(`${path}: claim ${claim.id} "${claim.scenario}" does not line up with a scenario`)
			return { ...claim, rule: null, line: 0, steps: [] }
		}
		return { ...claim, rule: scenario.rule, line: scenario.line, steps: expand(scenario) }
	})

	return { feature, scenarios: result, problems }
}

/** A scenario's steps; an outline's once per Examples row, deduplicated. */
function expand(scenario) {
	const rows = scenario.examples.flatMap((table) =>
		(table.rows ?? []).map((cells) => Object.fromEntries((table.header ?? []).map((name, at) => [name, cells[at] ?? '']))),
	)
	const steps = []
	const seen = new Set()
	const add = (step) => {
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
				const text = step.text.replace(/<([^<>]+)>/g, (match, name) => (name in row ? row[name] : match))
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

/**
 * Every Reqnroll step definition in the given C# sources, with its class's
 * [Scope(Feature = …)] values. A partial class's scope, declared in one file,
 * applies to its steps in every file.
 */
export function readReqnrollBindings(sources) {
	const problems = []
	const bindingClasses = new Set()
	const scopes = new Map()

	for (const { path, source } of sources) {
		let buffer = []
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
					if (!scopes.has(declared[1])) scopes.set(declared[1], new Set())
					for (const feature of features) scopes.get(declared[1]).add(feature)
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

	const bindings = []
	for (const { path, source } of sources) {
		let current = null
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
export function unescapeString(body) {
	const simple = { n: '\n', t: '\t', r: '\r', 0: '\0', b: '\b', f: '\f', v: '\v' }
	return body.replace(/\\(.)/g, (match, character) => simple[character] ?? character)
}

/** Every playwright-bdd step definition in one TypeScript source. */
export function readPlaywrightBindings(path, source) {
	const problems = []
	const bindings = []
	if (!/const\s*\{[^}]*\}\s*=\s*createBdd\(/.test(source)) {
		if (TS_ANY_STEP.test(source)) problems.push(`${path}: steps are declared without "const { Given, When, Then } = createBdd()"`)
		TS_ANY_STEP.lastIndex = 0
		return { bindings, problems }
	}
	const destructured = source.match(/const\s*\{([^}]*)\}\s*=\s*createBdd\(/)[1]
	if (/:/.test(destructured)) problems.push(`${path}: createBdd() is destructured with an alias — this tool reads Given, When, and Then by name`)

	const lineAt = (offset) => source.slice(0, offset).split('\n').length
	for (const match of source.matchAll(TS_STEP)) {
		const [, keyword, double, single, regexSource, flags] = match
		const line = lineAt(match.index + match[0].indexOf(keyword))
		if (regexSource !== undefined) bindings.push({ engine: 'playwright-bdd', keyword, pattern: { source: regexSource, flags }, file: path, line })
		else bindings.push({ engine: 'playwright-bdd', keyword, pattern: unescapeString(double ?? single), file: path, line })
	}
	const declared = [...source.matchAll(TS_ANY_STEP)].length
	if (declared !== bindings.length) {
		problems.push(`${path}: ${declared} step definition(s) but ${bindings.length} read — write each pattern as a string or regex literal`)
	}
	return { bindings, problems }
}

// ------------------------------------------------------ Cucumber Expressions --

export class UnsupportedExpression extends Error {}

const PARAMETERS = {
	string: `("(?:[^"\\\\]*(?:\\\\.[^"\\\\]*)*)"|'(?:[^'\\\\]*(?:\\\\.[^'\\\\]*)*)')`,
	word: '([^\\s]+)',
	int: '((?:-?\\d+)|(?:\\d+))',
	'': '(.*)',
}

const escapeRegExp = (text) => text.replace(/[.*+?^${}()|[\]\\/]/g, '\\$&')

/**
 * A Cucumber Expression as an anchored RegExp, following the official grammar
 * for the parts this repository uses: the parameter types string, word, int,
 * and the anonymous {}; optional text; alternation. Anything else throws, so a
 * new construct fails loudly instead of matching the wrong steps.
 */
export function cucumberToRegExp(expression) {
	// Tokens: text characters, whitespace, parameters, optionals, and slashes.
	const tokens = []
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
					text += expression[end] ?? ''
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
			tokens.push({ kind: 'slash' })
		} else if (/\s/.test(character)) {
			tokens.push({ kind: 'space', value: character })
		} else {
			tokens.push({ kind: 'text', value: character })
		}
	}

	// Alternation binds within a run of non-whitespace.
	let source = ''
	let run = []
	const flush = () => {
		if (run.some((token) => token.kind === 'slash')) {
			const alternatives = [[]]
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
	const emit = (token) => {
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
export function reqnrollKind(pattern) {
	if (pattern.startsWith('^') || pattern.endsWith('$')) return 'regex'
	if (/\{\w*\}/.test(pattern)) return 'cucumber'
	if (/(\([^)]+[*+]\)|\.\*)/.test(pattern)) return 'regex'
	return 'cucumber'
}

export function reqnrollMatcher(pattern) {
	if (reqnrollKind(pattern) === 'cucumber') return cucumberToRegExp(pattern)
	// Reqnroll anchors a regex at both ends, whichever end it already anchors.
	const inner = pattern.replace(/^\^/, '').replace(/(?<!\\)\$$/, '')
	return new RegExp(`^(?:${inner})$`)
}

export function playwrightMatcher(pattern) {
	if (typeof pattern === 'string') return cucumberToRegExp(pattern)
	return new RegExp(pattern.source, pattern.flags.replace(/[gy]/g, ''))
}

// ---------------------------------------------------------------- resolving --

const display = (binding) => (typeof binding.pattern === 'string' ? binding.pattern : `/${binding.pattern.source}/${binding.pattern.flags}`)

/**
 * Resolves every claim's steps to the bindings its runner would choose, and
 * reports what does not line up.
 */
export function resolve(features, csBindings, tsBindings) {
	const problems = []
	const compile = (bindings, matcher) =>
		bindings.flatMap((binding) => {
			try {
				return [{ ...binding, regex: matcher(binding.pattern) }]
			} catch (error) {
				problems.push(`${binding.file}:${binding.line}: ${error.message}`)
				return []
			}
		})
	const cs = compile(csBindings, reqnrollMatcher)
	const ts = compile(tsBindings, playwrightMatcher)

	const used = new Set()
	const cache = new Map()
	const ambiguous = new Map()

	const candidates = (engine, feature, step) => {
		const key = `${engine}\u0000${feature}\u0000${step.keyword}\u0000${step.text}`
		if (cache.has(key)) return cache.get(key)
		let found
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

	const claims = []
	for (const { feature, path, scenarios } of features) {
		for (const claim of scenarios) {
			const files = new Set()
			const unbound = []
			for (const step of claim.steps) {
				const found = candidates(claim.engine, feature, step)
				if (found.length === 0) unbound.push(step)
				if (found.length > 1) {
					const label = `${step.keyword} ${step.text}`
					if (!ambiguous.has(label)) ambiguous.set(label, new Set())
					for (const binding of found) ambiguous.get(label).add(binding.file)
				}
				for (const binding of found) {
					used.add(binding)
					files.add(binding.file)
				}
			}
			claims.push({ ...claim, path, files: [...files].sort(), unbound })
		}
	}

	const unused = [...cs, ...ts]
		.filter((binding) => !used.has(binding))
		.sort((a, b) => a.file.localeCompare(b.file) || display(a).localeCompare(display(b)))

	return { claims, unused, ambiguous, problems }
}

// ---------------------------------------------------------------- rendering --

const byId = (a, b) => (a.id < b.id ? -1 : a.id > b.id ? 1 : 0)
const code = (text) => (text.includes('`') ? `\`\` ${text} \`\`` : `\`${text}\``)
const link = (file) => `[${posix.basename(file)}](${posix.relative(SPEC_ROOT, file)})`

/**
 * The map. Every block derives from one claim, one step, or one binding, and
 * nothing is counted across the tree, so two branches that each carry a
 * correct map merge into the map of the merged tree (ADR-0106).
 */
export function render({ claims, unused, ambiguous }) {
	const lines = [
		'---',
		'title: Step bindings',
		'description: Generated map from every claim to the step-definition files that bind its steps, with the places the specification and the code disagree.',
		'type: guide',
		'---',
		'',
		'# Step bindings',
		'',
		'> **Generated file — do not edit by hand.**',
		'> Regenerate with `node tools/spec/generate-bindings.mjs`. CI fails on a difference, and on a',
		'> built claim with a step no definition binds',
		'> ([ADR-0184](decisions/ADR-0184-a-generated-map-binds-every-claim-to-its-step-definitions.md)).',
		'> One block per claim, step, or definition, and no totals, so branches merge it',
		'> without conflicting',
		'> ([ADR-0106](decisions/ADR-0106-every-line-of-the-matrix-derives-from-one-source-item.md)).',
		'',
		'Each claim lists the step-definition files its runner binds: Reqnroll for a',
		'claim without `@ui`, playwright-bdd for one with it. An `Unbound` step is one no',
		'definition matches; a built claim may never have one.',
	]

	const areas = [...new Set(claims.map((claim) => claim.area))].sort()
	for (const area of areas) {
		lines.push('', `## Claims: ${area}`)
		for (const claim of claims.filter((each) => each.area === area).sort(byId)) {
			lines.push('', `### ${claim.id}`, '')
			for (const file of claim.files) lines.push(`- ${link(file)}`)
			for (const step of claim.unbound) lines.push(`- Unbound: ${code(`${step.keyword} ${step.text}`)}`)
			if (claim.files.length === 0 && claim.unbound.length === 0) lines.push('- No steps.')
		}
	}

	const stale = claims.filter((claim) => claim.status === 'Planned' && claim.unbound.length === 0 && claim.steps.length > 0).sort(byId)
	lines.push(
		'',
		'## Stale @ignore',
		'',
		'A claim still tagged `@ignore` whose every step a definition already binds. Drop',
		'the tag once the scenario passes, or say in the scenario why it stays.',
	)
	for (const claim of stale) lines.push('', `### ${claim.id}`, '', 'Every step is bound.')

	lines.push(
		'',
		'## Ambiguous steps',
		'',
		'A step more than one definition matches. The runner fails it; name one.',
	)
	for (const label of [...ambiguous.keys()].sort()) {
		lines.push('', `### ${code(label)}`, '', [...ambiguous.get(label)].sort().map(link).join(' · '))
	}

	lines.push(
		'',
		'## Unused step definitions',
		'',
		'A step definition no scenario step matches: test code the specification does',
		'not ask for. Delete it, or write the scenario it was for.',
	)
	for (const binding of unused) {
		lines.push('', `### ${code(display(binding))}`, '', `${link(binding.file)} — ${binding.engine}, ${binding.keyword}`)
	}

	lines.push('')
	return lines.join('\n')
}

/** The whole-tree counts, reported rather than committed (ADR-0106). */
export function totals({ claims, unused, ambiguous }) {
	const unbound = claims.filter((claim) => claim.unbound.length > 0)
	const stale = claims.filter((claim) => claim.status === 'Planned' && claim.unbound.length === 0 && claim.steps.length > 0)
	return (
		`${claims.length} claims: ${claims.length - unbound.length} fully bound, ${unbound.length} with an unbound step, ` +
		`${stale.length} stale @ignore. ${ambiguous.size} ambiguous steps, ${unused.length} unused step definitions.`
	)
}

// ------------------------------------------------------------------- files --

function walk(root, directory, extension) {
	const full = join(root, directory)
	if (!existsSync(full)) return []
	return readdirSync(full).flatMap((entry) => {
		if (entry === 'bin' || entry === 'obj' || entry === 'node_modules') return []
		const path = posix.join(directory, entry)
		if (statSync(join(root, path)).isDirectory()) return walk(root, path, extension)
		return path.endsWith(extension) ? [path] : []
	})
}

export function build(root = ROOT) {
	const problems = []
	const read = (path) => readFileSync(join(root, path), 'utf8')

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
	return { ...resolved, problems, map: render(resolved) }
}

/**
 * Builds the map and writes or checks it, reporting the way the command line
 * does without exiting, so a test can exercise every outcome. Returns the exit
 * code.
 */
export function main(root = ROOT, { check = false, fail = true } = {}) {
	const result = build(root)
	const target = join(root, BINDINGS)

	for (const problem of result.problems) console.error(`::error::${problem}`)

	const gaps = result.claims.filter((claim) => claim.status === 'Covered' && claim.unbound.length > 0)
	for (const claim of gaps) {
		for (const step of claim.unbound) {
			console.error(`::error file=${claim.path},line=${step.line}::${claim.id} step "${step.keyword} ${step.text}" matches no ${claim.engine} step definition`)
		}
	}

	if (check) {
		const committed = existsSync(target) ? readFileSync(target, 'utf8') : ''
		if (committed !== result.map) {
			console.error(`::error file=${BINDINGS}::Out of date. Run 'node tools/spec/generate-bindings.mjs' and commit the result; on a same-repo pull request traceability.yml commits it for you (ADR-0184).`)
			return 1
		}
	} else {
		writeFileSync(target, result.map)
	}

	console.log(`${BINDINGS} ${check ? 'checked' : 'written'}. ${totals(result)}`)
	if (!check && process.env.GITHUB_STEP_SUMMARY) appendFileSync(process.env.GITHUB_STEP_SUMMARY, `**Step bindings:** ${totals(result)}\n`)

	if (!fail) return 0
	return result.problems.length > 0 || gaps.length > 0 ? 1 : 0
}

const runAsCommand = String(process.argv[1]).endsWith('/generate-bindings.mjs')
if (runAsCommand) process.exit(main(ROOT, { check: process.argv.includes('--check'), fail: !process.argv.includes('--no-fail') }))
