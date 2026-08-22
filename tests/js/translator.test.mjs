import { describe, it } from 'node:test'
import assert from 'node:assert/strict'

import { TranslatorNotConfiguredError, createTranslator } from '../../tools/translator.mjs'

describe('the translator adapter', () => {
	describe('given no configuration at all', () => {
		it('when one is created then it defaults to DeepL and asks for the key it needs', () => {
			// Given / When / Then — DeepL is the decided provider (ADR-0022), so the
			// default is not a guess. The missing piece is the credential.
			assert.throws(() => createTranslator({}), TranslatorNotConfiguredError)
			assert.throws(() => createTranslator({}), /DEEPL_API_KEY/)
		})
	})

	describe('given a chat-completions provider missing its endpoint', () => {
		it('when one is created then the missing setting is named', () => {
			// Given
			const config = { provider: 'chat-completions', model: 'a-model', apiKey: 'k' }

			// When / Then
			assert.throws(() => createTranslator(config), /TRANSLATION_ENDPOINT/)
		})
	})

	describe('given a chat-completions provider missing its model', () => {
		it('when one is created then the missing setting is named', () => {
			// Given
			const config = { provider: 'chat-completions', endpoint: 'https://example.invalid', apiKey: 'k' }

			// When / Then
			assert.throws(() => createTranslator(config), /TRANSLATION_MODEL/)
		})
	})

	describe('given a fully configured chat-completions provider', () => {
		it('when one is created then it reports the provider and model it will use', () => {
			// Given
			const config = {
				provider: 'chat-completions',
				endpoint: 'https://example.invalid/chat/completions',
				model: 'vendor/a-model',
				apiKey: 'k',
			}

			// When
			const translator = createTranslator(config)

			// Then
			assert.equal(translator.name, 'chat-completions:vendor/a-model')
			assert.equal(typeof translator.translate, 'function')
		})
	})

	describe('given the offline stub provider', () => {
		it('when it translates then it returns one entry per key, marked as stub output', async () => {
			// Given
			const translator = createTranslator({ provider: 'stub' })

			// When
			const out = await translator.translate(
				[
					{ key: 'form.submit', text: 'Submit' },
					{ key: 'form.cancel', text: 'Cancel' },
				],
				{ source: 'en-CA', target: 'fr-CA' },
			)

			// Then
			assert.equal(translator.name, 'stub')
			assert.equal(out.size, 2)
			assert.match(out.get('form.submit'), /Submit/)
		})
	})

	describe('given a batch of keys', () => {
		it('when the request body is built then every key travels in one call', () => {
			// Given
			const translator = createTranslator({
				provider: 'chat-completions',
				endpoint: 'https://example.invalid/chat/completions',
				model: 'vendor/a-model',
				apiKey: 'k',
			})

			// When
			const body = translator.buildRequest(
				[
					{ key: 'form.submit', text: 'Submit' },
					{ key: 'form.cancel', text: 'Cancel' },
				],
				{ source: 'en-CA', target: 'fr-CA' },
			)

			// Then — one request, both keys, and the target locale stated
			assert.equal(body.model, 'vendor/a-model')
			assert.equal(body.messages.length, 2)
			const payload = body.messages[1].content
			assert.match(payload, /form\.submit/)
			assert.match(payload, /form\.cancel/)
			assert.match(body.messages[0].content, /fr-CA/)
		})
	})

	describe('given a provider response wrapped in a markdown fence', () => {
		it('when it is parsed then the JSON inside is still read', () => {
			// Given
			const translator = createTranslator({
				provider: 'chat-completions',
				endpoint: 'https://example.invalid/chat/completions',
				model: 'vendor/a-model',
				apiKey: 'k',
			})

			// When
			const out = translator.parseResponse('```json\n{"form.submit":"Envoyer"}\n```')

			// Then
			assert.equal(out.get('form.submit'), 'Envoyer')
		})
	})

	describe('given a provider response that is not JSON', () => {
		it('when it is parsed then it fails loudly rather than writing prose into the locale', () => {
			// Given
			const translator = createTranslator({
				provider: 'chat-completions',
				endpoint: 'https://example.invalid/chat/completions',
				model: 'vendor/a-model',
				apiKey: 'k',
			})

			// When / Then
			assert.throws(() => translator.parseResponse('I am happy to help!'), /JSON/)
		})
	})

	describe('given an unknown provider name', () => {
		it('when one is created then it refuses rather than falling back', () => {
			// Given / When / Then
			assert.throws(() => createTranslator({ provider: 'bing' }), /bing/)
		})
	})

	describe('given a DeepL free-tier key', () => {
		it('when one is created then it targets the free endpoint, by the :fx suffix', () => {
			// Given — DeepL identifies Free keys by a ':fx' suffix
			const translator = createTranslator({ provider: 'deepl', apiKey: 'abc-123:fx' })

			// Then
			assert.equal(translator.endpoint, 'https://api-free.deepl.com/v2/translate')
		})
	})

	describe('given a DeepL paid key', () => {
		it('when one is created then it targets the pro endpoint', () => {
			// Given
			const translator = createTranslator({ provider: 'deepl', apiKey: 'abc-123' })

			// Then
			assert.equal(translator.endpoint, 'https://api.deepl.com/v2/translate')
		})
	})

	describe('given DeepL and the two official locales', () => {
		it('when the request is built then it asks for Canadian French, not metropolitan', () => {
			// Given
			const translator = createTranslator({ provider: 'deepl', apiKey: 'k' })

			// When
			const body = translator.buildRequest(
				[
					{ key: 'form.submit', text: 'Submit' },
					{ key: 'form.cancel', text: 'Cancel' },
				],
				{ source: 'en-CA', target: 'fr-CA' },
			)

			// Then — FR-CA is a real DeepL target language, verified against their
			// supported-languages table. FR would be metropolitan French.
			assert.equal(body.target_lang, 'FR-CA')
			assert.equal(body.source_lang, 'EN')
			assert.deepEqual(body.text, ['Submit', 'Cancel'])
			assert.equal(body.preserve_formatting, true)
		})
	})

	describe('given DeepL and the default formality', () => {
		it('when the request is built then it prefers formal and can never 400 for it', () => {
			// Given / When
			const body = createTranslator({ provider: 'deepl', apiKey: 'k' }).buildRequest(
				[{ key: 'a', text: 'A' }],
				{ source: 'en-CA', target: 'fr-CA' },
			)

			// Then — 'more' would fail with HTTP 400 on a target that does not
			// support formality. 'prefer_more' degrades to default instead.
			assert.equal(body.formality, 'prefer_more')
		})
	})

	describe('given a DeepL response', () => {
		it('when it is parsed then translations map back to keys by position', () => {
			// Given — DeepL returns translations in the order requested, with no keys
			const translator = createTranslator({ provider: 'deepl', apiKey: 'k' })
			const items = [
				{ key: 'form.submit', text: 'Submit' },
				{ key: 'form.cancel', text: 'Cancel' },
			]

			// When
			const out = translator.parseResponse(
				{ translations: [{ text: 'Envoyer' }, { text: 'Annuler' }] },
				items,
			)

			// Then
			assert.equal(out.get('form.submit'), 'Envoyer')
			assert.equal(out.get('form.cancel'), 'Annuler')
		})
	})

	describe('given a DeepL response with the wrong number of translations', () => {
		it('when it is parsed then it refuses rather than mapping keys to the wrong French', () => {
			// Given — position is the only thing tying a translation to its key, so a
			// length mismatch silently shifts every key by one
			const translator = createTranslator({ provider: 'deepl', apiKey: 'k' })
			const items = [
				{ key: 'form.submit', text: 'Submit' },
				{ key: 'form.cancel', text: 'Cancel' },
			]

			// When / Then
			assert.throws(() => translator.parseResponse({ translations: [{ text: 'Envoyer' }] }, items), /2.*1|1.*2/)
		})
	})

	describe('given a DeepL translator', () => {
		it('when it names itself then the provenance says variant and formality', () => {
			// Given / When — DeepL has no model id, so the things that actually
			// determine the output are what get recorded
			const translator = createTranslator({ provider: 'deepl', apiKey: 'k' })

			// Then
			assert.equal(translator.name, 'deepl:FR-CA:prefer_more')
		})
	})

	describe('given a locale DeepL has no code for', () => {
		it('when the request is built then it refuses rather than guessing a code', () => {
			// Given
			const translator = createTranslator({ provider: 'deepl', apiKey: 'k' })

			// When / Then
			assert.throws(
				() => translator.buildRequest([{ key: 'a', text: 'A' }], { source: 'en-CA', target: 'de-DE' }),
				/de-DE/,
			)
		})
	})
})
