import { describe, it } from 'node:test'
import assert from 'node:assert/strict'

import { TranslatorNotConfiguredError, createTranslator } from '../../tools/translator.mjs'

describe('the translator adapter', () => {
	describe('given no provider configuration at all', () => {
		it('when one is created then it says so rather than inventing a provider', () => {
			// Given / When / Then
			assert.throws(() => createTranslator({}), TranslatorNotConfiguredError)
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
			assert.throws(() => createTranslator({ provider: 'deepl' }), /deepl/)
		})
	})
})
