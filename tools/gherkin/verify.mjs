#!/usr/bin/env node
// Verifies every features/**/*.feature file parses as valid Gherkin, using
// the official @cucumber/gherkin parser (the same parser cucumber-js uses).
//
// This checks syntax only, not step definitions: these .feature files are
// specification documents, not an executable test suite, so there is no step
// implementation to run them against. See features/README.md.
import { readdirSync, readFileSync, statSync } from 'node:fs'
import { join, relative } from 'node:path'
import { AstBuilder, GherkinClassicTokenMatcher, Parser } from '@cucumber/gherkin'
import { IdGenerator } from '@cucumber/messages'

const root = 'features'

const findFeatureFiles = (dir) =>
  readdirSync(dir).flatMap((entry) => {
    const path = join(dir, entry)
    if (statSync(path).isDirectory()) return findFeatureFiles(path)
    return path.endsWith('.feature') ? [path] : []
  })

const makeParser = () =>
  new Parser(new AstBuilder(IdGenerator.uuid()), new GherkinClassicTokenMatcher())

const files = findFeatureFiles(root)
if (files.length === 0) {
  console.error(`::error::No .feature files found under ${root}/.`)
  process.exit(1)
}

let failures = 0
for (const file of files) {
  const source = readFileSync(file, 'utf8')
  try {
    makeParser().parse(source)
  } catch (error) {
    failures += 1
    const errors = error.errors ?? [error]
    for (const e of errors) {
      const line = e.location?.line ? `:${e.location.line}` : ''
      console.error(`::error file=${file}${line}::${e.message}`)
    }
  }
}

if (failures > 0) {
  console.error(`${failures} of ${files.length} feature file(s) failed to parse.`)
  process.exit(1)
}

console.log(`${files.length} feature file(s) under ${relative('.', root)}/ parsed cleanly.`)
