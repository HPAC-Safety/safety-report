import { describe, it } from 'node:test'
import assert from 'node:assert/strict'
import { existsSync, mkdtempSync, mkdirSync, readFileSync, writeFileSync } from 'node:fs'
import { tmpdir } from 'node:os'
import { join } from 'node:path'

import { parseArgs, stubMissingKeys, main } from '../../tools/stub-missing-translations.mjs'

let counter = 0

/** A throwaway `locales/` directory holding whichever files a case needs. */
function locales(files) {
	const dir = join(mkdtempSync(join(tmpdir(), 'stub-translations-')), `case-${counter++}`)
	mkdirSync(dir, { recursive: true })
	for (const [name, content] of Object.entries(files)) {
		writeFileSync(join(dir, name), `${JSON.stringify(content, null, 2)}\n`)
	}
	return dir
}

const read = (dir, name) => JSON.parse(readFileSync(join(dir, name), 'utf8'))

describe('stubMissingKeys', () => {
	describe('given a target missing a key the source has', () => {
		it('when it stubs then the missing key is added prefixed with #', () => {
			// Given
			const source = { nav: { contact: 'Contact' } }
			const target = {}

			// When
			const { value, changed } = stubMissingKeys(source, target)

			// Then
			assert.equal(changed, true)
			assert.deepEqual(value, { nav: { contact: '#Contact' } })
		})
	})

	describe('given a target that already has every source key', () => {
		it('when it stubs then nothing changes', () => {
			// Given
			const source = { nav: { contact: 'Contact' } }
			const target = { nav: { contact: 'Contactez-nous' } }

			// When
			const { value, changed } = stubMissingKeys(source, target)

			// Then
			assert.equal(changed, false)
			assert.deepEqual(value, target)
		})
	})

	describe('given a target with a key the source does not have', () => {
		it('when it stubs then that key is preserved untouched', () => {
			// Given
			const source = { a: '1' }
			const target = { b: '2' }

			// When
			const { value, changed } = stubMissingKeys(source, target)

			// Then
			assert.equal(changed, true)
			assert.deepEqual(value, { a: '#1', b: '2' })
		})
	})
})

describe('parseArgs', () => {
	describe('given no --locales flag', () => {
		it('when it parses then it defaults to the locales directory', () => {
			// Given / When
			const { dir } = parseArgs([])

			// Then
			assert.equal(dir, 'locales')
		})
	})

	describe('given a --locales flag', () => {
		it('when it parses then it uses that directory', () => {
			// Given / When
			const { dir } = parseArgs(['--locales', 'custom-dir'])

			// Then
			assert.equal(dir, 'custom-dir')
		})
	})
})

describe('the stub command', () => {
	describe('given no en-CA.json yet', () => {
		it('when it runs then it notices and writes nothing', () => {
			// Given
			const dir = locales({})

			// When
			main(dir)

			// Then
			assert.equal(existsSync(join(dir, 'fr-CA.json')), false)
		})
	})

	describe('given English keys missing from French', () => {
		it('when it runs then French gets # stubs and English is untouched', () => {
			// Given
			const dir = locales({ 'en-CA.json': { nav: { contact: 'Contact' } } })

			// When
			main(dir)

			// Then
			assert.deepEqual(read(dir, 'fr-CA.json'), { nav: { contact: '#Contact' } })
			assert.deepEqual(read(dir, 'en-CA.json'), { nav: { contact: 'Contact' } })
		})
	})

	describe('given a French-only key missing from English', () => {
		it('when it runs then English gets a # stub for it', () => {
			// Given
			const dir = locales({
				'en-CA.json': { nav: { contact: 'Contact' } },
				'fr-CA.json': { nav: { contact: 'Contactez-nous' }, extra: { onlyFrench: 'Seulement en français' } },
			})

			// When
			main(dir)

			// Then
			assert.equal(read(dir, 'en-CA.json').extra.onlyFrench, '#Seulement en français')
			assert.equal(read(dir, 'fr-CA.json').nav.contact, 'Contactez-nous')
		})
	})

	describe('given every key already present on both sides', () => {
		it('when it runs then neither file is rewritten', () => {
			// Given
			const dir = locales({
				'en-CA.json': { nav: { contact: 'Contact' } },
				'fr-CA.json': { nav: { contact: 'Contactez-nous' } },
			})
			const before = { en: read(dir, 'en-CA.json'), fr: read(dir, 'fr-CA.json') }

			// When
			main(dir)

			// Then
			assert.deepEqual(read(dir, 'en-CA.json'), before.en)
			assert.deepEqual(read(dir, 'fr-CA.json'), before.fr)
		})
	})
})
