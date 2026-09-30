#!/usr/bin/env node
/**
 * ITranslator, for the CI locale job.
 *
 * It is the JavaScript counterpart of `HpacSafety.Core.ITranslator` — the same
 * port, the same direction-agnostic contract, the same prompt — because the two
 * live in different runtimes and cannot share a type. The .NET one runs in the
 * API and the Worker and translates question wording, answers, comments, and
 * summary drafts. This one runs in GitHub Actions and translates UI chrome out
 * of `locales/en-CA.json`. Both send the one prompt in
 * `locales/translation-prompt.v1.md` and the term list in
 * `locales/terms.json`, so the two produce the same French (ADR-0179).
 *
 * **A raw report never reaches either.** Nothing here ever sees report data —
 * its only input is a JSON file of UI labels that ships in the repository.
 *
 * ## The port
 *
 *   translate(items, { source, target, instructions }) -> Promise<Map<key, string>>
 *
 * where `items` is `[{ key, text }]` and `instructions` is an optional list of
 * plain-language rules the model must follow — today, one per term in
 * `locales/terms.json` (ADR-0102), built by `termInstructions`. Every key goes
 * in one call: the batching is the caller's, not the provider's.
 *
 * ## The contract
 *
 * The model is sent `{"texts": [...]}` and must answer `{"translations": [...]}`,
 * one string per text, in order. Position is the only thing tying a translation
 * to its key, so a wrong count, a reply that is not that object, an empty
 * translation of a non-empty string, or a string that loses or alters a
 * `{placeholder}` or a markup tag is refused. The error says what was wrong and
 * never quotes the reply.
 *
 * ## Providers
 *
 * | name | what it is |
 * |---|---|
 * | `gemini` | **The default.** Google Gemini through its OpenAI-compatible endpoint, `en-CA` to `fr-CA`. See ADR-0179. |
 * | `stub` | Offline stand-in for the test suite. Stamps `provider: "stub"`, which `--check` rejects, so its output can never reach `main`. |
 *
 * Configuration comes from the environment:
 *
 *   GEMINI_API_KEY               the paid Gemini key (the workflow passes GEMINI_API_KEY_DEV)
 *   TRANSLATION_MODEL            model; defaults to gemini-3.7-flash
 *   TRANSLATION_REASONING_EFFORT low (default) | medium | high
 *   TRANSLATION_ENDPOINT         overrides the endpoint
 *   TRANSLATION_PROVIDER         gemini (default) | stub
 */

import { readFileSync } from 'node:fs'

/** Raised when the job has no provider to call. Never a silent fallback. */
export class TranslatorNotConfiguredError extends Error {
	constructor(message) {
		super(message)
		this.name = 'TranslatorNotConfiguredError'
	}
}

/** The one current translation prompt. A behavior change is a new version file. */
export const PROMPT_FILE = 'translation-prompt.v1.md'

export const DEFAULT_MODEL = 'gemini-3.7-flash'
export const DEFAULT_REASONING_EFFORT = 'low'
const REASONING_EFFORTS = ['low', 'medium', 'high']
const DEFAULT_ENDPOINT = 'https://generativelanguage.googleapis.com/v1beta/openai/chat/completions'

/** What the prompt says each locale means. */
const LANGUAGES = {
	'en-CA':
		'Canadian English (en-CA), with Canadian spelling and usage: colour, centre, metre, licence for the noun',
	'fr-CA':
		'Canadian French (fr-CA), following Canadian usage and addressing the reader formally with "vous"',
}

/** Reads provider configuration out of the environment. */
export function configFromEnv(env = process.env) {
	return {
		provider: env.TRANSLATION_PROVIDER,
		endpoint: env.TRANSLATION_ENDPOINT,
		model: env.TRANSLATION_MODEL,
		reasoningEffort: env.TRANSLATION_REASONING_EFFORT,
		apiKey: env.GEMINI_API_KEY,
	}
}

/** The prompt file, read from the repository's `locales/`. */
export function readPromptTemplate() {
	return readFileSync(new URL(`../locales/${PROMPT_FILE}`, import.meta.url), 'utf8')
}

/**
 * The prompt with its language names and term list filled in. The .NET
 * translator does exactly the same substitution on exactly the same file.
 */
export function renderPrompt(template, { source, target, instructions = [] }) {
	const languageOf = (locale) => {
		const language = LANGUAGES[locale]
		if (!language) {
			throw new Error(`No translation language is configured for '${locale}'.`)
		}
		return language
	}

	const terms = instructions.length > 0 ? instructions.map((line) => `- ${line}`).join('\n') : '- (none)'

	return template
		.replaceAll('[[source_language]]', languageOf(source))
		.replaceAll('[[target_language]]', languageOf(target))
		.replaceAll('[[terms]]', terms)
}

/** Every `{placeholder}` and markup tag in a string, sorted. */
const PROTECTED_TOKEN = /\{[^{}]*\}|<\/?[A-Za-z][^<>]*>/g
const protectedTokens = (text) => (String(text).match(PROTECTED_TOKEN) ?? []).sort()

/**
 * Strips a markdown fence, if the model wrapped one round its JSON. A fenced
 * reply is a correct reply wrapped in decoration; anything that is not JSON at
 * all fails, because writing prose into a locale file is worse than a red
 * build.
 */
function unfence(content) {
	const text = String(content ?? '').trim()
	const fenced = text.match(/^```(?:json)?\s*\n([\s\S]*?)\n?```$/)
	return (fenced ? fenced[1] : text).trim()
}

/**
 * Reads the model's reply against the items it was sent, or throws.
 *
 * None of the messages quote the reply: it can echo the request, and a
 * failure must not carry provider output into a log.
 */
export function parseTranslations(content, items) {
	let parsed
	try {
		parsed = JSON.parse(unfence(content))
	} catch {
		throw new Error('The translation model did not return JSON.')
	}

	const translations = parsed?.translations
	if (parsed === null || typeof parsed !== 'object' || !Array.isArray(translations)) {
		throw new Error('The translation model returned JSON without a "translations" array.')
	}
	if (translations.length !== items.length) {
		throw new Error(
			`The translation model returned ${translations.length} translations for ${items.length} strings. ` +
				'Position is what maps a translation to its key, so this is not recoverable.',
		)
	}

	return new Map(
		items.map(({ key, text }, index) => {
			const translated = translations[index]
			if (typeof translated !== 'string') {
				throw new Error(`The translation for '${key}' is not a string.`)
			}
			if (String(text).trim() !== '' && translated.trim() === '') {
				throw new Error(`The translation for '${key}' is empty.`)
			}
			if (protectedTokens(text).join('\u0000') !== protectedTokens(translated).join('\u0000')) {
				throw new Error(`The translation for '${key}' changed a {placeholder} or a markup tag.`)
			}
			return [key, translated]
		}),
	)
}

/** Google Gemini, through its OpenAI-compatible chat-completions endpoint. */
function geminiTranslator({ endpoint, model, reasoningEffort, apiKey }) {
	if (!apiKey) {
		throw new TranslatorNotConfiguredError(
			'The Gemini translator needs GEMINI_API_KEY (the workflow passes the GEMINI_API_KEY_DEV secret). See ADR-0179.',
		)
	}

	const chosenModel = model || DEFAULT_MODEL
	const chosenEffort = (reasoningEffort || DEFAULT_REASONING_EFFORT).toLowerCase()
	if (!REASONING_EFFORTS.includes(chosenEffort)) {
		throw new TranslatorNotConfiguredError(
			`TRANSLATION_REASONING_EFFORT must be low, medium, or high, not '${reasoningEffort}'.`,
		)
	}
	const resolved = endpoint || DEFAULT_ENDPOINT
	const template = readPromptTemplate()

	// No temperature: Google recommends leaving Gemini 3 at its default, and
	// warns that lowering it can cause looping (ADR-0104).
	const buildRequest = (items, { source, target, instructions = [] }) => ({
		model: chosenModel,
		reasoning_effort: chosenEffort,
		response_format: { type: 'json_object' },
		messages: [
			{ role: 'system', content: renderPrompt(template, { source, target, instructions }) },
			{ role: 'user', content: JSON.stringify({ texts: items.map(({ text }) => text) }) },
		],
	})

	return {
		name: `gemini:${chosenModel}`,
		endpoint: resolved,
		buildRequest,
		parseResponse: parseTranslations,
		async translate(items, locales) {
			const response = await fetch(resolved, {
				method: 'POST',
				headers: {
					'content-type': 'application/json',
					authorization: `Bearer ${apiKey}`,
				},
				body: JSON.stringify(buildRequest(items, locales)),
			})

			if (!response.ok) {
				// The status only, never the body, and never the key.
				throw new Error(`The translation provider answered ${response.status} ${response.statusText}.`)
			}

			const payload = await response.json()
			return parseTranslations(payload?.choices?.[0]?.message?.content, items)
		},
	}
}

/**
 * Offline stand-in, for the test suite only.
 *
 * It deliberately does not weaken the guarantee the real adapter makes: it
 * stamps `provider: "stub"` in the provenance, and `translate-locale.mjs
 * --check` fails on that stamp. Stub French therefore cannot merge, whatever
 * anyone sets in a workflow.
 */
function stubTranslator() {
	return {
		name: 'stub',
		buildRequest: (items, { instructions = [] } = {}) => ({ model: 'stub', messages: [], items, instructions }),
		parseResponse: parseTranslations,
		async translate(items, { target }) {
			return new Map(items.map(({ key, text }) => [key, `[${target} STUB] ${text}`]))
		},
	}
}

/**
 * Builds the translator the environment asks for.
 *
 * @throws {TranslatorNotConfiguredError} when the key is missing or a setting
 *   is unusable. Gemini is the decided provider (ADR-0179), so defaulting to it
 *   is a recorded decision rather than a guess; what can still be missing is
 *   the credential, and that failure names it.
 */
export function createTranslator(config = {}) {
	const provider = config.provider || 'gemini'

	switch (provider) {
		case 'gemini':
			return geminiTranslator(config)
		case 'stub':
			return stubTranslator()
		default:
			throw new TranslatorNotConfiguredError(
				`Unknown TRANSLATION_PROVIDER '${provider}'. Known providers: gemini, stub.`,
			)
	}
}
