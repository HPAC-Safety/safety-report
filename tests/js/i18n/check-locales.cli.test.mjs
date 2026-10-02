import { describe, it } from 'node:test'
import assert from 'node:assert/strict'
import { mkdirSync, mkdtempSync, rmSync, writeFileSync } from 'node:fs'
import { tmpdir } from 'node:os'
import { join } from 'node:path'
import { spawnSync } from 'node:child_process'
import { fileURLToPath } from 'node:url'

const SCRIPT = fileURLToPath(new URL('../../../tools/i18n/check-locales.mjs', import.meta.url))

const run = (files) => {
	const dir = mkdtempSync(join(tmpdir(), 'check-locales-'))
	mkdirSync(join(dir, 'locales'))
	for (const [name, value] of Object.entries(files)) writeFileSync(join(dir, 'locales', name), typeof value === 'string' ? value : JSON.stringify(value))
	const result = spawnSync('node', [SCRIPT], { cwd: dir, encoding: 'utf8' })
	rmSync(dir, { recursive: true })
	return result
}

describe('check-locales', () => {
	it('skips with a notice when there is no English file yet', () => {
		const result = run({})
		assert.equal(result.status, 0)
		assert.match(result.stdout, /::notice::No locale files yet/)
	})

	it('passes when French has every English key', () => {
		const result = run({ 'en-CA.json': { a: { b: 'x' } }, 'fr-CA.json': { a: { b: 'y' } } })
		assert.equal(result.status, 0)
		assert.match(result.stdout, /1 keys in locales\/en-CA\.json/)
	})

	it('fails on a key missing from French and on one French added', () => {
		const result = run({ 'en-CA.json': { a: 'x', b: 'y' }, 'fr-CA.json': { a: 'x', c: 'z' } })
		assert.equal(result.status, 1)
		assert.match(result.stderr, /Missing key 'b'/)
		assert.match(result.stderr, /Key 'c' is not in locales\/en-CA\.json/)
	})

	it('fails when a locale file is not valid JSON', () => {
		const result = run({ 'en-CA.json': '{ nope' })
		assert.equal(result.status, 1)
		assert.match(result.stderr, /is not valid JSON/)
	})

	it('fails when English defines no keys', () => {
		const result = run({ 'en-CA.json': {} })
		assert.equal(result.status, 1)
		assert.match(result.stderr, /defines no keys/)
	})

	it('fails on a key that flattens to the same path as another', () => {
		const result = run({ 'en-CA.json': { 'a.b': 'x', a: { b: 'y' } } })
		assert.equal(result.status, 1)
		assert.match(result.stderr, /Duplicate keys: a\.b/)
	})

	it('skips the parity check when French does not exist yet', () => {
		const result = run({ 'en-CA.json': { a: 'x' } })
		assert.equal(result.status, 0)
		assert.match(result.stdout, /fr-CA\.json does not exist yet/)
	})

	it('treats arrays and nulls as leaf values', () => {
		const result = run({ 'en-CA.json': { a: [1], b: null }, 'fr-CA.json': { a: [2], b: null } })
		assert.equal(result.status, 0)
		assert.match(result.stdout, /2 keys in/)
	})
})
