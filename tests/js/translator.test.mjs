import { describe, it } from 'node:test'
import assert from 'node:assert/strict'

import {
	DEFAULT_MODEL,
	PROMPT_FILE,
	TranslatorNotConfiguredError,
	configFromEnv,
	createTranslator,
	parseTranslations,
	readPromptTemplate,
	renderPrompt,
} from '../../tools/translator.mjs'

const gemini = (extra = {}) => createTranslator({ provider: 'gemini', apiKey: 'k', ...extra })
const locales = { source: 'en-CA', target: 'fr-CA' }
const two = [
	{ key: 'form.submit', text: 'Submit' },
	{ key: 'form.cancel', text: 'Cancel' },
]
const reply = (...translations) => JSON.stringify({ translations })

describe('the translator adapter', () => {
	describe('given no configuration at all', () => {
		it('when one is created then it defaults to Gemini and asks for the key it needs', () => {
			// Given / When / Then — Gemini is the decided provider (ADR-0179), so the
			// default is not a guess. The missing piece is the credential.
			assert.throws(() => createTranslator({}), TranslatorNotConfiguredError)
			assert.throws(() => createTranslator({}), /GEMINI_API_KEY/)
		})
	})

	describe('given the environment the workflow provides', () => {
		it('when the configuration is read then the Gemini key and the tuning settings are picked up', () => {
			// Given / When
			const config = configFromEnv({
				GEMINI_API_KEY: 'secret',
				TRANSLATION_MODEL: 'gemini-x',
				TRANSLATION_REASONING_EFFORT: 'medium',
			})

			// Then
			assert.equal(config.apiKey, 'secret')
			assert.equal(config.model, 'gemini-x')
			assert.equal(config.reasoningEffort, 'medium')
		})
	})

	describe('given an unknown provider name', () => {
		it('when one is created then it refuses rather than falling back', () => {
			// Given / When / Then
			assert.throws(() => createTranslator({ provider: 'deep-thought' }), /deep-thought/)
		})
	})

	describe('given a key and nothing else', () => {
		it('when one is created then it uses the default model at low reasoning and names itself', () => {
			// Given / When
			const translator = gemini()
			const body = translator.buildRequest(two, locales)

			// Then
			assert.equal(translator.name, `gemini:${DEFAULT_MODEL}`)
			assert.equal(DEFAULT_MODEL, 'gemini-3.7-flash')
			assert.equal(body.model, 'gemini-3.7-flash')
			assert.equal(body.reasoning_effort, 'low')
			assert.deepEqual(body.response_format, { type: 'json_object' })
			assert.equal(Object.hasOwn(body, 'temperature'), false)
		})
	})

	describe('given a model and reasoning effort in the settings', () => {
		it('when the request is built then they are what is asked for', () => {
			// Given / When
			const body = gemini({ model: 'gemini-x', reasoningEffort: 'HIGH' }).buildRequest(two, locales)

			// Then
			assert.equal(body.model, 'gemini-x')
			assert.equal(body.reasoning_effort, 'high')
		})

		it('when the effort is not low, medium, or high then it refuses to start', () => {
			// Given / When / Then
			assert.throws(() => gemini({ reasoningEffort: 'extreme' }), /low, medium, or high/)
		})
	})

	describe('given the offline stub provider', () => {
		it('when it translates then it returns one entry per key, marked as stub output', async () => {
			// Given
			const translator = createTranslator({ provider: 'stub' })

			// When
			const out = await translator.translate(two, locales)

			// Then
			assert.equal(translator.name, 'stub')
			assert.equal(out.size, 2)
			assert.match(out.get('form.submit'), /Submit/)
		})
	})

	describe('given a batch of keys', () => {
		it('when the request is built then every text travels in one call, in order, and nothing else does', () => {
			// Given / When
			const body = gemini().buildRequest(two, locales)

			// Then — one request, the texts by position, and the keys never sent
			assert.equal(body.messages.length, 2)
			assert.deepEqual(JSON.parse(body.messages[1].content), { texts: ['Submit', 'Cancel'] })
			assert.doesNotMatch(body.messages[1].content, /form\./)
		})
	})

	describe('given English to French', () => {
		it('when the request is built then the prompt asks for Canadian French', () => {
			// Given / When
			const prompt = gemini().buildRequest(two, { source: 'en-CA', target: 'fr-CA' }).messages[0].content

			// Then
			assert.match(prompt, /from Canadian English \(en-CA\).* into Canadian French \(fr-CA\)/s)
			assert.doesNotMatch(prompt, /\[\[/)
		})
	})

	describe('given French to English', () => {
		it('when the request is built then the prompt asks for Canadian English, not American or British', () => {
			// Given / When
			const prompt = gemini().buildRequest(two, { source: 'fr-CA', target: 'en-CA' }).messages[0].content

			// Then
			assert.match(prompt, /into Canadian English \(en-CA\), with Canadian spelling/)
			assert.doesNotMatch(prompt, /American|British|EN-US|EN-GB/)
		})
	})

	describe('given a locale nothing is configured for', () => {
		it('when the request is built then it refuses rather than guessing', () => {
			// Given / When / Then
			assert.throws(() => gemini().buildRequest(two, { source: 'en-CA', target: 'de-DE' }), /de-DE/)
		})
	})

	describe('given a required rendering for a term', () => {
		it('when the request is built then the system prompt states it', () => {
			// Given
			const instructions = ['"upload" (English, in any form) is "téléverser" in French. Never "télécharg…" in French.']

			// When
			const prompt = gemini().buildRequest(two, { ...locales, instructions }).messages[0].content

			// Then
			assert.match(prompt, /## Terminology[\s\S]*\n- "upload" \(English, in any form\) is "téléverser" in French\./)
		})

		it('when there is no term then the prompt says so rather than leaving a hole', () => {
			// Given / When
			const prompt = gemini().buildRequest(two, locales).messages[0].content

			// Then
			assert.match(prompt, /\n- \(none\)\n?$/)
		})
	})

	describe('given the prompt file', () => {
		it('when it is read then it is the one current version, with its three slots', () => {
			// Given / When
			const template = readPromptTemplate()

			// Then
			assert.equal(PROMPT_FILE, 'translation-prompt.v1.md')
			for (const slot of ['[[source_language]]', '[[target_language]]', '[[terms]]']) {
				assert.ok(template.includes(slot), `${slot} is missing from ${PROMPT_FILE}`)
			}
			assert.equal(renderPrompt(template, locales).includes('[['), false)
		})
	})

	describe('given a reply that is correct', () => {
		it('when it is parsed then translations map back to keys by position', () => {
			// Given / When
			const out = parseTranslations(reply('Envoyer', 'Annuler'), two)

			// Then
			assert.equal(out.get('form.submit'), 'Envoyer')
			assert.equal(out.get('form.cancel'), 'Annuler')
		})
	})

	describe('given a reply wrapped in a markdown fence', () => {
		it('when it is parsed then the JSON inside is still read', () => {
			// Given / When
			const out = parseTranslations('```json\n' + reply('Envoyer', 'Annuler') + '\n```', two)

			// Then
			assert.equal(out.get('form.submit'), 'Envoyer')
		})
	})

	describe('given a reply that is not JSON', () => {
		it('when it is parsed then it fails loudly, without quoting the reply', () => {
			// Given / When / Then
			assert.throws(() => parseTranslations('I am happy to help with Submit!', two), (error) => {
				assert.match(error.message, /JSON/)
				assert.doesNotMatch(error.message, /happy to help/)
				return true
			})
		})
	})

	describe('given JSON without a translations array', () => {
		it('when it is parsed then it is refused', () => {
			// Given / When / Then
			assert.throws(() => parseTranslations('{"form.submit":"Envoyer"}', two), /translations/)
		})
	})

	describe('given the wrong number of translations', () => {
		it('when it is parsed then it refuses rather than mapping keys to the wrong French', () => {
			// Given — position is the only thing tying a translation to its key, so a
			// length mismatch silently shifts every key by one
			for (const wrong of [reply('Envoyer'), reply('Envoyer', 'Annuler', 'Trop')]) {
				// When / Then
				assert.throws(() => parseTranslations(wrong, two), /2 strings/)
			}
		})
	})

	describe('given an empty or non-string translation', () => {
		it('when it is parsed then it is refused', () => {
			// Given / When / Then
			assert.throws(() => parseTranslations(reply('Envoyer', '  '), two), /empty/)
			assert.throws(() => parseTranslations(JSON.stringify({ translations: ['Envoyer', 7] }), two), /not a string/)
		})

		it('when the original was blank too then an empty translation is fine', () => {
			// Given / When
			const out = parseTranslations(reply(''), [{ key: 'a', text: '' }])

			// Then
			assert.equal(out.get('a'), '')
		})
	})

	describe('given a string carrying a {placeholder} and a markup tag', () => {
		const items = [{ key: 'a', text: 'Showing <b>{count}</b> reports' }]

		it('when the model keeps both then they come back intact', () => {
			// Given / When
			const out = parseTranslations(reply('Affichage de <b>{count}</b> signalements'), items)

			// Then
			assert.equal(out.get('a'), 'Affichage de <b>{count}</b> signalements')
		})

		it('when the model translates the placeholder then the reply is refused', () => {
			// Given — "{count}" once came back as "{compte}" from a translator
			// When / Then
			assert.throws(() => parseTranslations(reply('Affichage de <b>{compte}</b> signalements'), items), /placeholder/)
		})

		it('when the model drops the tag then the reply is refused, without quoting it', () => {
			// Given / When / Then
			assert.throws(() => parseTranslations(reply('Affichage de {count} signalements'), items), (error) => {
				assert.match(error.message, /\{placeholder\} or a markup tag/)
				assert.doesNotMatch(error.message, /Affichage/)
				return true
			})
		})
	})

	describe('given literal angle brackets and ampersands that are not tags', () => {
		it('when the model keeps them then the reply is accepted', () => {
			// Given
			const items = [{ key: 'a', text: 'Altitude < 500 feet & descending' }]

			// When
			const out = parseTranslations(reply('Altitude < 500 pieds & en descente'), items)

			// Then
			assert.equal(out.get('a'), 'Altitude < 500 pieds & en descente')
		})
	})
})
