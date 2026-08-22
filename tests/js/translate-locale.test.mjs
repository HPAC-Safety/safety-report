import { describe, it } from 'node:test'
import assert from 'node:assert/strict'

import {
	applyPlan,
	flatten,
	glossaryFrench,
	hashOf,
	planTranslation,
	unflatten,
	verifyLocales,
} from '../../tools/translate-locale.mjs'

/** The smallest English set that still has a nested shape. */
const english = () => ({
	form: { submit: 'Submit', cancel: 'Cancel' },
	admin: { queue: { title: 'Review queue' } },
})

/** Meta stamped as though every key above had already been translated. */
const metaFor = (source, provider = 'test-provider') => {
	const meta = {}
	for (const [key, text] of flatten(source)) {
		meta[key] = { source_hash: hashOf(text), provider, reviewed: false }
	}
	return meta
}

const frenchFor = (source) =>
	unflatten(flatten(source).map(([key, text]) => [key, `FR:${text}`]))

describe('the locale key flattener', () => {
	describe('given a nested English locale file', () => {
		it('when it is flattened then every leaf becomes one dotted key', () => {
			// Given
			const source = english()

			// When
			const flat = flatten(source)

			// Then
			assert.deepEqual(flat, [
				['form.submit', 'Submit'],
				['form.cancel', 'Cancel'],
				['admin.queue.title', 'Review queue'],
			])
		})
	})

	describe('given a flattened locale', () => {
		it('when it is unflattened then the original nesting is restored', () => {
			// Given
			const source = english()

			// When
			const round = unflatten(flatten(source))

			// Then
			assert.deepEqual(round, source)
		})
	})

	describe('given a locale file whose leaves are not strings', () => {
		it('when it is flattened then it refuses rather than translating a number', () => {
			// Given — a value that is not user-facing text
			const source = { form: { retries: 3 } }

			// When / Then
			assert.throws(() => flatten(source), /form\.retries/)
		})
	})
})

describe('the per-key content hash', () => {
	describe('given the same English text twice', () => {
		it('when it is hashed then the two hashes are equal', () => {
			// Given / When
			const first = hashOf('Serious injury')
			const second = hashOf('Serious injury')

			// Then
			assert.equal(first, second)
		})
	})

	describe('given English text that has been edited', () => {
		it('when it is hashed then the hash differs', () => {
			// Given / When / Then
			assert.notEqual(hashOf('Submit'), hashOf('Submit report'))
		})
	})
})

describe('the glossary', () => {
	describe('given a term pinned as a plain string', () => {
		it('when it is looked up then the official French is returned', () => {
			// Given
			const glossary = { 'form.submit': 'Envoyer' }

			// When
			const pinned = glossaryFrench('form.submit', glossary)

			// Then
			assert.equal(pinned, 'Envoyer')
		})
	})

	describe('given a term pinned as an annotated object', () => {
		it('when it is looked up then the fr-CA member is returned', () => {
			// Given
			const glossary = {
				_note: 'ignored',
				'form.submit': { 'fr-CA': 'Envoyer', note: 'HPAC official wording' },
			}

			// When
			const pinned = glossaryFrench('form.submit', glossary)

			// Then
			assert.equal(pinned, 'Envoyer')
		})
	})

	describe('given a key that is not pinned', () => {
		it('when it is looked up then nothing is returned', () => {
			// Given / When / Then
			assert.equal(glossaryFrench('form.cancel', { 'form.submit': 'Envoyer' }), undefined)
		})
	})
})

describe('the translation plan', () => {
	describe('given one new English key among many already translated', () => {
		it('when the plan is built then exactly that key is queued for translation', () => {
			// Given
			const before = english()
			const meta = metaFor(before)
			const french = frenchFor(before)
			const after = english()
			after.form.reset = 'Start over'

			// When
			const plan = planTranslation({ english: after, french, meta, glossary: {} })

			// Then
			assert.deepEqual(plan.translate, [{ key: 'form.reset', text: 'Start over' }])
			assert.equal(plan.unchanged.length, 3)
		})
	})

	describe('given an English key whose wording was edited', () => {
		it('when the plan is built then only that key is re-translated', () => {
			// Given
			const before = english()
			const meta = metaFor(before)
			const french = frenchFor(before)
			const after = english()
			after.form.submit = 'Submit report'

			// When
			const plan = planTranslation({ english: after, french, meta, glossary: {} })

			// Then
			assert.deepEqual(plan.translate, [{ key: 'form.submit', text: 'Submit report' }])
			assert.ok(plan.unchanged.includes('form.cancel'))
			assert.ok(plan.unchanged.includes('admin.queue.title'))
		})
	})

	describe('given nothing has changed in English', () => {
		it('when the plan is built then it queues no work at all', () => {
			// Given
			const source = english()

			// When
			const plan = planTranslation({
				english: source,
				french: frenchFor(source),
				meta: metaFor(source),
				glossary: {},
			})

			// Then
			assert.equal(plan.translate.length, 0)
			assert.equal(plan.pin.length, 0)
			assert.equal(plan.remove.length, 0)
		})
	})

	describe('given a glossary-pinned key that has never been rendered into French', () => {
		it('when the plan is built then it is pinned rather than sent to the translator', () => {
			// Given — the severity scale is close to a defined term
			const source = { form: { severity: 'Serious injury (secondary medical aid)' } }
			const glossary = { 'form.severity': 'Blessure grave (aide médicale secondaire)' }

			// When
			const plan = planTranslation({ english: source, french: {}, meta: {}, glossary })

			// Then
			assert.equal(plan.translate.length, 0)
			assert.deepEqual(plan.pin, [
				{
					key: 'form.severity',
					text: 'Blessure grave (aide médicale secondaire)',
					source: 'Serious injury (secondary medical aid)',
				},
			])
		})
	})

	describe('given a glossary-pinned key whose English wording was edited', () => {
		it('when the plan is built then it is still never sent to the translator', () => {
			// Given
			const glossary = { 'form.severity': 'Blessure grave (aide médicale secondaire)' }
			const french = { form: { severity: 'Blessure grave (aide médicale secondaire)' } }
			const meta = { 'form.severity': { source_hash: hashOf('Serious injury'), provider: 'glossary', reviewed: true } }

			// When — the English changed, which for any other key means re-translate
			const plan = planTranslation({
				english: { form: { severity: 'Serious injury (secondary medical aid)' } },
				french,
				meta,
				glossary,
			})

			// Then
			assert.equal(plan.translate.length, 0)
			assert.equal(plan.pin.length, 1)
		})
	})

	describe('given an English key that was deleted', () => {
		it('when the plan is built then the orphaned French key is removed', () => {
			// Given
			const before = english()
			const after = english()
			delete after.form.cancel

			// When
			const plan = planTranslation({
				english: after,
				french: frenchFor(before),
				meta: metaFor(before),
				glossary: {},
			})

			// Then
			assert.deepEqual(plan.remove, ['form.cancel'])
		})
	})
})

describe('applying a translation plan', () => {
	describe('given one translated key', () => {
		it('when the plan is applied then only that key moves and provenance is stamped', () => {
			// Given
			const before = english()
			const after = english()
			after.form.reset = 'Start over'
			const plan = planTranslation({
				english: after,
				french: frenchFor(before),
				meta: metaFor(before),
				glossary: {},
			})

			// When
			const result = applyPlan({
				french: frenchFor(before),
				meta: metaFor(before),
				plan,
				translations: new Map([['form.reset', 'Recommencer']]),
				provider: 'test-provider',
			})

			// Then
			assert.equal(result.french.form.reset, 'Recommencer')
			assert.equal(result.french.form.submit, 'FR:Submit')
			assert.deepEqual(result.meta['form.reset'], {
				source_hash: hashOf('Start over'),
				provider: 'test-provider',
				reviewed: false,
			})
		})
	})

	describe('given a glossary-pinned key', () => {
		it('when the plan is applied then HPAC official wording lands, stamped as reviewed', () => {
			// Given
			const source = { form: { severity: 'Serious injury (secondary medical aid)' } }
			const glossary = { 'form.severity': 'Blessure grave (aide médicale secondaire)' }
			const plan = planTranslation({ english: source, french: {}, meta: {}, glossary })

			// When
			const result = applyPlan({
				french: {},
				meta: {},
				plan,
				translations: new Map(),
				provider: 'test-provider',
			})

			// Then
			assert.equal(result.french.form.severity, 'Blessure grave (aide médicale secondaire)')
			assert.equal(result.meta['form.severity'].provider, 'glossary')
			assert.equal(result.meta['form.severity'].reviewed, true)
		})
	})

	describe('given a translator that returned nothing for a queued key', () => {
		it('when the plan is applied then it refuses rather than stamping a missing translation', () => {
			// Given
			const after = english()
			after.form.reset = 'Start over'
			const plan = planTranslation({ english: after, french: {}, meta: {}, glossary: {} })

			// When / Then
			assert.throws(
				() => applyPlan({ french: {}, meta: {}, plan, translations: new Map(), provider: 'p' }),
				/form\.submit/,
			)
		})
	})

	describe('given a deleted English key', () => {
		it('when the plan is applied then the French and its provenance both go', () => {
			// Given
			const before = english()
			const after = english()
			delete after.form.cancel
			const plan = planTranslation({
				english: after,
				french: frenchFor(before),
				meta: metaFor(before),
				glossary: {},
			})

			// When
			const result = applyPlan({
				french: frenchFor(before),
				meta: metaFor(before),
				plan,
				translations: new Map(),
				provider: 'test-provider',
			})

			// Then
			assert.equal(result.french.form.cancel, undefined)
			assert.equal(result.meta['form.cancel'], undefined)
		})
	})
})

describe('verifying the locales without translating', () => {
	describe('given English, French, and provenance that agree', () => {
		it('when they are verified then nothing is reported', () => {
			// Given
			const source = english()

			// When
			const result = verifyLocales({
				english: source,
				french: frenchFor(source),
				meta: metaFor(source),
				glossary: {},
			})

			// Then
			assert.equal(result.ok, true)
			assert.deepEqual(result.problems, [])
		})
	})

	describe('given an English key edited without regenerating French', () => {
		it('when it is verified then the drift is named', () => {
			// Given
			const before = english()
			const after = english()
			after.form.submit = 'Submit report'

			// When
			const result = verifyLocales({
				english: after,
				french: frenchFor(before),
				meta: metaFor(before),
				glossary: {},
			})

			// Then
			assert.equal(result.ok, false)
			assert.match(result.problems.join('\n'), /form\.submit/)
		})
	})

	describe('given a French file missing a key English has', () => {
		it('when it is verified then the missing key is named', () => {
			// Given
			const source = english()
			const french = frenchFor(source)
			delete french.form.cancel

			// When
			const result = verifyLocales({ english: source, french, meta: metaFor(source), glossary: {} })

			// Then
			assert.equal(result.ok, false)
			assert.match(result.problems.join('\n'), /form\.cancel/)
		})
	})

	describe('given a French file carrying a key English has dropped', () => {
		it('when it is verified then the orphan is named', () => {
			// Given
			const before = english()
			const after = english()
			delete after.form.cancel

			// When
			const result = verifyLocales({
				english: after,
				french: frenchFor(before),
				meta: metaFor(before),
				glossary: {},
			})

			// Then
			assert.equal(result.ok, false)
			assert.match(result.problems.join('\n'), /form\.cancel/)
		})
	})

	describe('given a glossary-pinned key overwritten with machine French', () => {
		it('when it is verified then it fails, because a machine must not decide that wording', () => {
			// Given
			const source = { form: { severity: 'Serious injury (secondary medical aid)' } }
			const glossary = { 'form.severity': 'Blessure grave (aide médicale secondaire)' }

			// When
			const result = verifyLocales({
				english: source,
				french: { form: { severity: 'Blessure sérieuse' } },
				meta: { 'form.severity': { source_hash: hashOf(source.form.severity), provider: 'a-model', reviewed: false } },
				glossary,
			})

			// Then
			assert.equal(result.ok, false)
			assert.match(result.problems.join('\n'), /glossary/i)
		})
	})

	describe('given French produced by the offline stub translator', () => {
		it('when it is verified then it fails, so stub output can never merge', () => {
			// Given
			const source = english()
			const meta = metaFor(source, 'stub')

			// When
			const result = verifyLocales({ english: source, french: frenchFor(source), meta, glossary: {} })

			// Then
			assert.equal(result.ok, false)
			assert.match(result.problems.join('\n'), /stub/)
		})
	})
})
