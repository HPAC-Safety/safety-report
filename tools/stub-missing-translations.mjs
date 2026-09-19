#!/usr/bin/env node
/**
 * Fills in any key missing from one locale file with the other locale's
 * text, prefixed `#`, so a developer sees an unmistakable "not yet
 * translated" marker instead of a silent English fallback while working.
 *
 * Local only, no credential, no API call — the actual translating still
 * only happens in CI (`tools/translate-locale.mjs --generate`,
 * `.github/workflows/i18n-translate.yml`, ADR-0021). A `#`-stub has no
 * `fr-CA.meta.json` provenance stamp, so CI's own change-detection already
 * queues it for real translation the next time it runs — nothing here
 * needs to coordinate with that pipeline beyond leaving no stamp behind.
 * See ADR-0054.
 *
 * Usage:
 *   node tools/stub-missing-translations.mjs [--locales locales]
 */
import { existsSync, readFileSync, writeFileSync } from 'node:fs'
import { join } from 'node:path'

import { flatten, unflatten } from './translate-locale.mjs'

const SOURCE_LOCALE = 'en-CA'
const TARGET_LOCALE = 'fr-CA'
const STUB_PREFIX = '#'

const readJson = (path) => (existsSync(path) ? JSON.parse(readFileSync(path, 'utf8')) : {})
const writeJson = (path, value) => writeFileSync(path, `${JSON.stringify(value, null, 2)}\n`)

/**
 * Fills `target`'s missing keys from `source`, prefixed `#`. Returns the
 * updated `target` object and whether anything changed, so the caller can
 * skip an unnecessary write.
 */
export function stubMissingKeys(source, target) {
	const sourceEntries = flatten(source)
	const targetByKey = new Map(flatten(target))

	let changed = false
	for (const [key, text] of sourceEntries) {
		if (!targetByKey.has(key)) {
			targetByKey.set(key, `${STUB_PREFIX}${text}`)
			changed = true
		}
	}

	// Key order follows source first, then anything target-only already had.
	const ordered = [...sourceEntries.map(([key]) => key), ...targetByKey.keys()].filter(
		(key, index, all) => all.indexOf(key) === index,
	)
	const merged = ordered.map((key) => [key, targetByKey.get(key)])

	return { value: unflatten(merged), changed }
}

export function main(dir) {
	const englishPath = join(dir, `${SOURCE_LOCALE}.json`)
	const frenchPath = join(dir, `${TARGET_LOCALE}.json`)

	if (!existsSync(englishPath)) {
		console.log(`::notice::${englishPath} does not exist yet. Nothing to stub.`)
		return
	}

	const english = readJson(englishPath)
	const french = readJson(frenchPath)

	const forFrench = stubMissingKeys(english, french)
	const forEnglish = stubMissingKeys(french, english)

	if (forFrench.changed) writeJson(frenchPath, forFrench.value)
	if (forEnglish.changed) writeJson(englishPath, forEnglish.value)

	if (forFrench.changed || forEnglish.changed) {
		console.log('Stubbed missing translations with a # marker. CI replaces them with the real text on merge to main.')
	}
}

export function parseArgs(argv) {
	let dir = 'locales'
	for (let i = 0; i < argv.length; i += 1) {
		if (argv[i] === '--locales') {
			dir = argv[i + 1]
			i += 1
		}
	}
	return { dir }
}

const runAsCommand = String(process.argv[1]).endsWith('stub-missing-translations.mjs')
if (runAsCommand) main(parseArgs(process.argv.slice(2)).dir)
