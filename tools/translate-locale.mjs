#!/usr/bin/env node
/**
 * Generates `locales/fr-CA.json` from `locales/en-CA.json`, translating only
 * what changed, and verifies the three files still agree.
 *
 * **Scope: UI chrome only.** The input is a JSON file of interface labels that
 * ships in the repository. A raw report never reaches this tool — invariant 4
 * in `AGENTS.md` — and neither does a summary; the worker handles those through
 * the .NET `ITranslator`, in production, with no Actions token in sight.
 *
 * ```mermaid
 * flowchart TD
 *     A["push to main"] --> B["hash every en-CA key"]
 *     B --> C{"any hash differs<br/>from fr-CA.meta.json?"}
 *     C -->|no| D["exit 0, open nothing"]
 *     C -->|yes| E["drop keys pinned<br/>in glossary.json"]
 *     E --> F["one batched provider call"]
 *     F --> G["merge into fr-CA.json,<br/>stamp fr-CA.meta.json"]
 *     G --> H["open a pull request"]
 * ```
 *
 * ## Two modes, and only one of them can spend money
 *
 *   --check      Verify parity and provenance. Reads files, calls nothing.
 *                This is what runs on `pull_request`.
 *   --generate   Translate what changed and write the files. Runs on `main`
 *                only, and opens a pull request rather than pushing.
 *
 * There is no default mode. A tool whose no-argument behaviour is the
 * side-effecting one gets run that way by accident exactly once. See ADR-0021.
 *
 * ## Change detection is a content hash per key
 *
 * Not a timestamp, which every checkout resets, and not a whole-file diff,
 * which re-translates 400 untouched keys because someone fixed one typo.
 * `fr-CA.meta.json` records the SHA-256 of the English each French string was
 * made from; a key is stale exactly when that hash no longer matches. See
 * ADR-0021.
 *
 * ## Glossary-pinned keys are never machine-translated
 *
 * The injury severity scale, rating names, certification classes, and the
 * consent question need HPAC's own official French. Keys named in
 * `locales/glossary.json` take their French from that file, are stamped
 * `provider: "glossary", reviewed: true`, and are never put in a request.
 *
 * Usage:
 *   node tools/translate-locale.mjs --check    [--locales locales]
 *   node tools/translate-locale.mjs --generate [--locales locales]
 */

import { createHash } from 'node:crypto'
import { appendFileSync, existsSync, readFileSync, unlinkSync, writeFileSync } from 'node:fs'
import { join } from 'node:path'

import { TranslatorNotConfiguredError, configFromEnv, createTranslator } from './translator.mjs'

const SOURCE_LOCALE = 'en-CA'
const TARGET_LOCALE = 'fr-CA'

/** Provenance stamp for a key whose French came from the glossary, not a model. */
const GLOSSARY_PROVIDER = 'glossary'

// --- pure functions, exercised directly by tests/js/translate-locale.test.mjs ---

/**
 * Turns a nested locale object into ordered `[dottedKey, text]` pairs.
 *
 * Order is source order, and it is preserved all the way back out again, so a
 * regenerated `fr-CA.json` diffs against its predecessor rather than being
 * reshuffled by the tool.
 *
 * @throws when a leaf is not a string. A number or a boolean in a locale file
 *   is a mistake, and translating it would turn it into one silently.
 */
export function flatten(value, prefix = '') {
	const entries = []

	for (const [name, child] of Object.entries(value ?? {})) {
		const key = prefix ? `${prefix}.${name}` : name

		if (typeof child === 'string') {
			entries.push([key, child])
		} else if (child !== null && typeof child === 'object' && !Array.isArray(child)) {
			entries.push(...flatten(child, key))
		} else {
			throw new Error(`${key} is ${Array.isArray(child) ? 'an array' : typeof child}; locale values must be strings.`)
		}
	}

	return entries
}

/** Rebuilds the nested shape from `[dottedKey, text]` pairs. */
export function unflatten(entries) {
	const root = {}

	for (const [key, text] of entries) {
		const parts = key.split('.')
		let node = root
		for (const part of parts.slice(0, -1)) {
			node[part] ??= {}
			node = node[part]
		}
		node[parts.at(-1)] = text
	}

	return root
}

/** The content hash a key's provenance is compared against. */
export function hashOf(text) {
	return createHash('sha256').update(text, 'utf8').digest('hex')
}

/**
 * HPAC's official French for a key, if it is pinned.
 *
 * A glossary entry is either the French string itself, or an object carrying
 * `fr-CA` plus whatever note explains where the wording came from. Keys
 * beginning `_` are file-level commentary and are not pins.
 */
export function glossaryFrench(key, glossary = {}) {
	if (key.startsWith('_')) return undefined

	const entry = glossary?.[key]
	if (entry === undefined || entry === null) return undefined
	if (typeof entry === 'string') return entry
	if (typeof entry === 'object' && typeof entry[TARGET_LOCALE] === 'string') return entry[TARGET_LOCALE]

	throw new Error(`glossary.json entry '${key}' has no ${TARGET_LOCALE} wording.`)
}

/**
 * Decides, per key, what has to happen — without doing any of it.
 *
 * @returns {{translate: {key: string, text: string}[],
 *            pin: {key: string, text: string}[],
 *            unchanged: string[],
 *            remove: string[]}}
 */
export function planTranslation({ english, french = {}, meta = {}, glossary = {} }) {
	const englishKeys = flatten(english)
	const frenchByKey = new Map(flatten(french))

	const plan = { translate: [], pin: [], unchanged: [], remove: [] }

	for (const [key, text] of englishKeys) {
		const pinned = glossaryFrench(key, glossary)
		const stamp = meta[key]

		if (pinned !== undefined) {
			// A pinned key is never sent to a translator, whatever changed. The
			// English wording may have been edited; the official French did not
			// change because of that, and only HPAC may say when it does.
			if (frenchByKey.get(key) !== pinned || stamp?.source_hash !== hashOf(text)) {
				plan.pin.push({ key, text: pinned, source: text })
			} else {
				plan.unchanged.push(key)
			}
			continue
		}

		if (frenchByKey.has(key) && stamp?.source_hash === hashOf(text)) {
			plan.unchanged.push(key)
		} else {
			plan.translate.push({ key, text })
		}
	}

	const wanted = new Set(englishKeys.map(([key]) => key))
	for (const key of frenchByKey.keys()) {
		if (!wanted.has(key)) plan.remove.push(key)
	}
	for (const key of Object.keys(meta)) {
		if (!wanted.has(key) && !plan.remove.includes(key)) plan.remove.push(key)
	}

	return plan
}

/**
 * The `{named}` placeholders a string carries, in order of appearance.
 *
 * A translator that drops one turns "Showing {count} reports" into a label with
 * a hole in it, and nothing downstream would notice — the key is present, the
 * hash says it is current, and only a French-reading user sees the damage. This
 * is checked for every provider, not inside one adapter, because it is a
 * property of the output rather than of any vendor.
 */
export function placeholdersIn(text) {
	return [...String(text).matchAll(/\{[^{}]*\}/g)].map((match) => match[0])
}

/** Compares placeholder multisets, ignoring order — French word order differs. */
function placeholdersAgree(english, french) {
	const sort = (list) => [...list].sort()
	const a = sort(placeholdersIn(english))
	const b = sort(placeholdersIn(french))
	return a.length === b.length && a.every((token, index) => token === b[index])
}

/**
 * Merges a plan's results into the French file and its provenance.
 *
 * @throws when the provider returned nothing for a key that was queued. A
 *   missing translation stamped as done is a key that never gets retried.
 */
export function applyPlan({ french = {}, meta = {}, plan, translations = new Map(), provider }) {
	const byKey = new Map(flatten(french))
	const nextMeta = { ...meta }

	for (const { key, text } of plan.translate) {
		const translated = translations.get(key)
		if (typeof translated !== 'string' || translated.length === 0) {
			throw new Error(`The provider returned no translation for ${key}.`)
		}
		if (!placeholdersAgree(text, translated)) {
			throw new Error(
				`The translation of ${key} does not carry the same placeholders as the English. ` +
					`Expected ${JSON.stringify(placeholdersIn(text))}, got ${JSON.stringify(placeholdersIn(translated))}.`,
			)
		}
		byKey.set(key, translated)
		nextMeta[key] = { source_hash: hashOf(text), provider, reviewed: false }
	}

	for (const { key, text, source } of plan.pin) {
		byKey.set(key, text)
		// `reviewed: true` because a human already wrote this French, by hand,
		// into glossary.json. Nothing here decided it. The hash is still of the
		// English, so a later edit to the English shows up as a normal diff
		// rather than as drift.
		nextMeta[key] = { source_hash: hashOf(source), provider: GLOSSARY_PROVIDER, reviewed: true }
	}

	for (const key of plan.remove) {
		byKey.delete(key)
		delete nextMeta[key]
	}

	// Key order follows English, so the generated file diffs cleanly.
	const ordered = [...byKey.entries()]
	return { french: unflatten(ordered), meta: sortKeys(nextMeta) }
}

function sortKeys(object) {
	return Object.fromEntries(Object.entries(object).sort(([a], [b]) => (a < b ? -1 : a > b ? 1 : 0)))
}

/**
 * Checks that English, French, and provenance still agree — reading only.
 *
 * This is what runs on a pull request. It never constructs a translator, so a
 * fork cannot make CI spend an inference call by opening one.
 */
export function verifyLocales({ english, french = {}, meta = {}, glossary = {} }) {
	const problems = []
	const englishKeys = flatten(english)
	const frenchByKey = new Map(flatten(french))
	const wanted = new Set(englishKeys.map(([key]) => key))

	for (const [key, text] of englishKeys) {
		const pinned = glossaryFrench(key, glossary)

		if (!frenchByKey.has(key)) {
			problems.push(`${TARGET_LOCALE} is missing '${key}'. Regenerate it — never hand-edit ${TARGET_LOCALE}.json.`)
			continue
		}

		if (pinned !== undefined) {
			if (frenchByKey.get(key) !== pinned) {
				problems.push(
					`'${key}' is pinned in glossary.json but ${TARGET_LOCALE}.json does not match it. ` +
						'A machine must not decide that wording.',
				)
			}
			continue
		}

		const stamp = meta[key]
		if (!stamp) {
			problems.push(`'${key}' has no provenance in ${TARGET_LOCALE}.meta.json.`)
		} else if (stamp.source_hash !== hashOf(text)) {
			problems.push(`'${key}' changed in ${SOURCE_LOCALE}.json after it was translated. Regenerate ${TARGET_LOCALE}.json.`)
		} else if (stamp.provider === 'stub') {
			problems.push(`'${key}' was translated by the offline stub translator, which is a test stand-in and must never reach main.`)
		}
	}

	for (const key of frenchByKey.keys()) {
		if (!wanted.has(key)) {
			problems.push(`${TARGET_LOCALE} has '${key}', which ${SOURCE_LOCALE} does not. Regenerate ${TARGET_LOCALE}.json.`)
		}
	}

	return { ok: problems.length === 0, problems }
}

// --- the command ------------------------------------------------------------

/** True when this file was run as a command rather than imported by a test. */
const runAsCommand = process.argv[1]?.endsWith('translate-locale.mjs') ?? false

function parseArgs(argv) {
	const flags = new Set()
	let dir = 'locales'

	for (let i = 0; i < argv.length; i += 1) {
		if (argv[i] === '--locales') {
			dir = argv[i + 1]
			i += 1
		} else if (argv[i].startsWith('--')) {
			flags.add(argv[i])
		}
	}

	return { check: flags.has('--check'), generate: flags.has('--generate'), dir }
}

const readJson = (path, fallback) => (existsSync(path) ? JSON.parse(readFileSync(path, 'utf8')) : fallback)

/** Writes JSON the way a human would, so the review diff is readable. */
const writeJson = (path, value) => writeFileSync(path, `${JSON.stringify(value, null, 2)}\n`)

function setOutput(pairs) {
	if (!process.env.GITHUB_OUTPUT) return
	const lines = Object.entries(pairs).map(([key, value]) => `${key}=${value}`)
	appendFileSync(process.env.GITHUB_OUTPUT, `${lines.join('\n')}\n`)
}

async function main() {
	const { check, generate, dir } = parseArgs(process.argv.slice(2))

	if (check === generate) {
		console.error('::error::Exactly one of --check or --generate is required.')
		process.exit(2)
	}

	const englishPath = join(dir, `${SOURCE_LOCALE}.json`)
	const frenchPath = join(dir, `${TARGET_LOCALE}.json`)
	const metaPath = join(dir, `${TARGET_LOCALE}.meta.json`)
	const glossaryPath = join(dir, 'glossary.json')
	const initialGenerationPath = join(dir, '.fr-CA.pending')

	if (!existsSync(englishPath)) {
		// #8 adds the locale files. Until it lands there is nothing to translate
		// and nothing to verify, and failing here would block every pull request
		// for a file this issue does not own.
		console.log(`::notice::${englishPath} does not exist yet — added by #8. Nothing to do.`)
		setOutput({ changed: 'false', keys: '0' })
		return
	}

	const english = readJson(englishPath, {})

	if (check && existsSync(initialGenerationPath) && !existsSync(frenchPath) && !existsSync(metaPath)) {
		console.log('::notice::The initial French generation is pending. Merge the translation workflow and let it open the generated-locale pull request.')
		setOutput({ changed: 'false', keys: '0' })
		return
	}

	const french = readJson(frenchPath, {})
	const meta = readJson(metaPath, {})
	const glossary = readJson(glossaryPath, {})

	if (check) {
		const { ok, problems } = verifyLocales({ english, french, meta, glossary })
		if (!ok) {
			for (const problem of problems) console.error(`::error::${problem}`)
			console.error('')
			console.error(`${SOURCE_LOCALE} is the source of truth and ${TARGET_LOCALE}.json is generated.`)
			console.error('This job never translates on a pull request — merge to main and let the')
			console.error('translation workflow open one with the French in it. See ADR-0021.')
			process.exit(1)
		}
		console.log(`${SOURCE_LOCALE}, ${TARGET_LOCALE}, and their provenance are in step.`)
		return
	}

	const plan = planTranslation({ english, french, meta, glossary })

	const total = plan.translate.length + plan.pin.length + plan.remove.length
	if (total === 0) {
		console.log('Nothing to translate. Every English key is already in step with its French.')
		setOutput({ changed: 'false', keys: '0' })
		return
	}

	if (plan.pin.length > 0) {
		console.log(`${plan.pin.length} key(s) pinned in glossary.json — taken verbatim, never sent to a provider:`)
		for (const { key } of plan.pin) console.log(`  ${key}`)
	}
	if (plan.remove.length > 0) {
		console.log(`${plan.remove.length} key(s) removed from ${SOURCE_LOCALE} — dropping their French:`)
		for (const key of plan.remove) console.log(`  ${key}`)
	}

	let translations = new Map()
	let provider = GLOSSARY_PROVIDER

	if (plan.translate.length > 0) {
		let translator
		try {
			translator = createTranslator(configFromEnv())
		} catch (error) {
			if (!(error instanceof TranslatorNotConfiguredError)) throw error
			// Deliberately not a failure. The provider is decided — DeepL, in
			// ADR-0022 — but the credential is added by a human in repository
			// settings, and until it exists this job reports what it would have
			// done and changes nothing. A red build on every push to main while
			// someone gets round to adding a secret would just get muted, and
			// then the next real failure is invisible too.
			console.log(`::warning::${error.message}`)
			console.log(`${plan.translate.length} key(s) are waiting for a translation provider:`)
			for (const { key } of plan.translate) console.log(`  ${key}`)
			setOutput({ changed: 'false', keys: '0' })
			return
		}

		console.log(`Translating ${plan.translate.length} key(s) via ${translator.name}, in one request:`)
		for (const { key } of plan.translate) console.log(`  ${key}`)

		translations = await translator.translate(plan.translate, {
			source: SOURCE_LOCALE,
			target: TARGET_LOCALE,
		})
		provider = translator.name
	}

	const result = applyPlan({ french, meta, plan, translations, provider })

	writeJson(frenchPath, result.french)
	writeJson(metaPath, result.meta)
	if (existsSync(initialGenerationPath)) unlinkSync(initialGenerationPath)

	const summary =
		`${plan.translate.length} translated, ${plan.pin.length} pinned, ${plan.remove.length} removed`
	console.log(`\n${summary}.`)
	setOutput({ changed: 'true', keys: String(total), summary })
}

if (runAsCommand) {
	main().catch((error) => {
		console.error(`::error::${error.message}`)
		process.exit(1)
	})
}
