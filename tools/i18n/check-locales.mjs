#!/usr/bin/env node
// Locale key parity.
//
// English is the source of truth; French is generated in CI and reviewed by a
// human, and `locales/fr-CA.json` is never hand-edited. This checks that the two
// files describe the same set of keys, so a key added to English without a
// French counterpart is caught before it reaches a page as a raw key name.
//
// Written alongside the first locale file rather than left for later, because
// the `i18n` CI job treats "en-CA.json exists but this tool does not" as an
// error. Issue #8 owns the wider i18n tooling and may well replace this with
// something richer; the contract it has to keep is the exit code.
import { readFileSync, existsSync } from 'node:fs'

const source = 'locales/en-CA.json'
const targets = ['locales/fr-CA.json']

const flatten = (value, prefix = '') =>
  Object.entries(value).flatMap(([key, entry]) => {
    const path = prefix ? `${prefix}.${key}` : key
    return entry !== null && typeof entry === 'object' && !Array.isArray(entry)
      ? flatten(entry, path)
      : [path]
  })

const read = (path) => {
  try {
    return flatten(JSON.parse(readFileSync(path, 'utf8')))
  } catch (error) {
    console.error(`::error file=${path}::${path} is not valid JSON: ${error.message}`)
    process.exit(1)
  }
}

const englishKeys = read(source)

if (englishKeys.length === 0) {
  console.error(`::error file=${source}::${source} defines no keys.`)
  process.exit(1)
}

const duplicates = englishKeys.filter((key, index) => englishKeys.indexOf(key) !== index)
if (duplicates.length > 0) {
  console.error(`::error file=${source}::Duplicate keys: ${duplicates.join(', ')}`)
  process.exit(1)
}

let failed = false

for (const target of targets) {
  if (!existsSync(target)) {
    console.log(`::notice::${target} does not exist yet. It is generated in CI, never hand-edited. Skipping parity.`)
    continue
  }

  const targetKeys = read(target)
  const missing = englishKeys.filter((key) => !targetKeys.includes(key))
  const extra = targetKeys.filter((key) => !englishKeys.includes(key))

  for (const key of missing) {
    console.error(`::error file=${target}::Missing key '${key}', present in ${source}.`)
    failed = true
  }
  for (const key of extra) {
    console.error(`::error file=${target}::Key '${key}' is not in ${source}. English is the source of truth.`)
    failed = true
  }
}

if (failed) {
  process.exit(1)
}

console.log(`${englishKeys.length} keys in ${source}; every locale present agrees.`)
