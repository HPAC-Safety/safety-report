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
 * | adapter | what it is |
 * |---|---|
 * | `gemini-…` model | **The default**, chosen by the model name, not a setting: Google Gemini through its OpenAI-compatible endpoint, `en-CA` to `fr-CA`. See ADR-0179. |
 * | `deepl` | **Kept, dormant** (ADR-0179). DeepL, targeting `FR-CA`; used only when `TRANSLATION_PROVIDER=deepl`, so it can be switched back. |
 * | `stub` | Offline stand-in for the test suite. Stamps `provider: "stub"`, which `--check` rejects, so its output can never reach `main`. |
 *
 * Configuration comes from the environment:
 *
 *   GEMINI_API_KEY               the paid Gemini key (the workflow passes GEMINI_API_KEY_DEV)
 *   TRANSLATION_MODEL            model; its prefix picks the provider, gemini- is Gemini; defaults to gemini-3.7-flash
 *   TRANSLATION_REASONING_EFFORT low (default) | medium | high
 *   TRANSLATION_ENDPOINT         overrides the endpoint
 *   TRANSLATION_PROVIDER         only deepl (dormant) | stub; the model name picks any other provider
 *   DEEPL_API_KEY                the dormant DeepL adapter's key. Free keys end in ":fx".
 *   TRANSLATION_FORMALITY        the dormant DeepL adapter's formality; defaults to prefer_more
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
		// Kept, dormant (ADR-0179): read only when the provider is 'deepl'.
		deeplApiKey: env.DEEPL_API_KEY,
		formality: env.TRANSLATION_FORMALITY,
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
			'The Gemini CI translator needs GEMINI_API_KEY (the workflow passes the GEMINI_API_KEY_DEV secret). See ADR-0179.',
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

// --- DeepL ------------------------------------------------------------------

/**
 * This system's locale codes, mapped to DeepL's.
 *
 * `FR-CA` is a real DeepL target language — verified against their
 * supported-languages table, where it is listed as "French (Canadian),
 * Translation: Target Only". So `locales/fr-CA.json` really does get Canadian
 * French, not metropolitan French under a Canadian name. That was worth
 * checking; `FR` would have been the quiet wrong answer.
 *
 * "Target Only" means FR-CA cannot be a *source* language. It never is here:
 * English is the source of truth for UI chrome.
 */
/** DeepL's ceiling on custom instructions per request, and on each one's length. */
export const DEEPL_MAX_INSTRUCTIONS = 10
export const DEEPL_MAX_INSTRUCTION_LENGTH = 300

const DEEPL_CODES = {
	'en-CA': 'EN',
	'fr-CA': 'FR-CA',
}

/**
 * Wraps each `{placeholder}` in a tag DeepL is told to leave untouched, so
 * "Showing {count} reports" cannot come back as "Showing {compte} reports".
 * `tag_handling: 'xml'` makes DeepL parse the string as XML, so any literal
 * `&`, `<`, `>` outside a placeholder is escaped first — otherwise it would
 * be read as markup rather than text.
 */
function protectPlaceholders(text) {
	const escaped = String(text)
		.replaceAll('&', '&amp;')
		.replaceAll('<', '&lt;')
		.replaceAll('>', '&gt;')
	return escaped.replace(/\{[^{}]*\}/g, (placeholder) => `<ph>${placeholder}</ph>`)
}

/** Reverses {@link protectPlaceholders} on a translated string. */
function unprotectPlaceholders(text) {
	return String(text)
		.replace(/<ph>([^<]*)<\/ph>/g, '$1')
		.replaceAll('&lt;', '<')
		.replaceAll('&gt;', '>')
		.replaceAll('&amp;', '&')
}

/**
 * DeepL: kept, dormant (ADR-0179). Gemini is the provider; this adapter is
 * selected only by an explicit `TRANSLATION_PROVIDER=deepl`, so DeepL can be
 * switched back without a rewrite. It keeps DeepL's own ceilings on custom
 * instructions, which the Gemini path does not have.
 */
function deeplTranslator({ apiKey, endpoint, formality }) {
	if (!apiKey) {
		throw new TranslatorNotConfiguredError(
			'The DeepL translator needs DEEPL_API_KEY. Add it as a repository secret. Kept, dormant: see ADR-0179.',
		)
	}

	// DeepL identifies Free-tier keys by a ':fx' suffix, and the two tiers have
	// different hosts. Getting this wrong is a 403 that reads like a bad key.
	const resolved =
		endpoint || (apiKey.endsWith(':fx')
			? 'https://api-free.deepl.com/v2/translate'
			: 'https://api.deepl.com/v2/translate')

	// 'more' and 'less' fail with HTTP 400 on a target language that does not
	// support formality; the 'prefer_' variants degrade to default instead. FR
	// supports formality, FR-CA is not documented as doing so, and a whole run
	// lost to a 400 over a nicety is not a trade worth making.
	//
	// Formal is the right default regardless: a national safety authority
	// addressing pilots uses "vous". Override with TRANSLATION_FORMALITY.
	const chosenFormality = formality || 'prefer_more'

	const codeFor = (locale) => {
		const code = DEEPL_CODES[locale]
		if (!code) {
			throw new Error(`DeepL has no configured language code for '${locale}'.`)
		}
		return code
	}

	const buildRequest = (items, { source, target, instructions = [] }) => {
		// Refused rather than dropped: silently losing one would leave that
		// term unprotected.
		if (instructions.length > DEEPL_MAX_INSTRUCTIONS) {
			throw new Error(
				`${instructions.length} term instructions; DeepL accepts at most ${DEEPL_MAX_INSTRUCTIONS} per request.`,
			)
		}
		const tooLong = instructions.find((line) => line.length > DEEPL_MAX_INSTRUCTION_LENGTH)
		if (tooLong !== undefined) {
			throw new Error(
				`A term instruction is ${tooLong.length} characters; DeepL accepts at most ${DEEPL_MAX_INSTRUCTION_LENGTH}.`,
			)
		}
		return {
		text: items.map(({ text }) => protectPlaceholders(text)),
		source_lang: codeFor(source),
		target_lang: codeFor(target),
		formality: chosenFormality,
		// These are interface labels. DeepL "correcting" the capitalisation or
		// the trailing space of a label is a change nobody asked for.
		preserve_formatting: true,
		// Required for `ignore_tags` to take effect. The `<ph>` tags come from
		// protectPlaceholders above, wrapping every `{placeholder}` token.
		tag_handling: 'xml',
		ignore_tags: ['ph'],
		// The required rendering of each term in locales/terms.json (ADR-0102).
		// Inline rather than a stored DeepL glossary: DeepL documents custom
		// instructions for French "and its variants", but documents glossary
		// variant support only for EN, PT, and ZH, and a stored glossary is
		// account state this job would have to create, find, and clean up.
		// Omitted when empty, so a run with no terms sends what it always did.
		...(instructions.length > 0 ? { custom_instructions: instructions } : {}),
		}
	}

	/**
	 * DeepL returns translations positionally, with no keys, so position is the
	 * only thing tying a translation to its key. A length mismatch would shift
	 * every key by one and stamp each with a hash that says it is correct —
	 * silently wrong French on every label after the gap.
	 */
	const parseResponse = (payload, items) => {
		const translations = payload?.translations
		if (!Array.isArray(translations)) {
			throw new Error('DeepL returned no translations array.')
		}
		if (translations.length !== items.length) {
			throw new Error(
				`DeepL returned ${translations.length} translations for ${items.length} strings. ` +
					'Position is what maps a translation to its key, so this is not recoverable.',
			)
		}
		return new Map(
			items.map(({ key }, index) => [key, unprotectPlaceholders(translations[index].text)]),
		)
	}

	return {
		// DeepL has no model id, so the provenance records what actually
		// determines the output instead: the target variant and the formality.
		name: `deepl:${DEEPL_CODES['fr-CA']}:${chosenFormality}`,
		endpoint: resolved,
		buildRequest,
		parseResponse,
		async translate(items, locales) {
			const response = await fetch(resolved, {
				method: 'POST',
				headers: {
					'content-type': 'application/json',
					authorization: `DeepL-Auth-Key ${apiKey}`,
				},
				body: JSON.stringify(buildRequest(items, locales)),
			})

			if (!response.ok) {
				// Status only, never the body — see the note in the adapter above.
				throw new Error(`DeepL answered ${response.status} ${response.statusText}.`)
			}

			return parseResponse(await response.json(), items)
		},
	}
}

/**
 * The OpenAI-compatible adapters, by the model-name prefix each claims. The model
 * is the only thing that picks one (ADR-0179, as in the .NET translator): a new
 * OpenAI-compatible provider is one adapter and one entry here.
 */
const MODEL_ADAPTERS = [{ prefix: 'gemini-', create: geminiTranslator }]

/**
 * Builds the translator the environment asks for.
 *
 * The adapter follows the model name: `TRANSLATION_MODEL` (default
 * `gemini-3.7-flash`) is matched to an adapter by prefix, and `gemini-` is the
 * Gemini OpenAI-compatible endpoint. `TRANSLATION_PROVIDER` does not choose among
 * those; it exists only to select the offline `stub` or the dormant `deepl`
 * adapter explicitly.
 *
 * @throws {TranslatorNotConfiguredError} when the key is missing, a setting is
 *   unusable, or no adapter claims the model. Gemini is the decided provider
 *   (ADR-0179), so its default model is a recorded decision rather than a guess;
 *   what can still be missing is the credential, and that failure names it.
 */
export function createTranslator(config = {}) {
	switch (config.provider) {
		case 'stub':
			return stubTranslator()
		case 'deepl':
			return deeplTranslator({ ...config, apiKey: config.deeplApiKey })
		case undefined:
		case '':
			break
		default:
			throw new TranslatorNotConfiguredError(
				`Unknown TRANSLATION_PROVIDER '${config.provider}'. It only selects the dormant deepl adapter or the offline stub; the model name picks the provider.`,
			)
	}

	const model = config.model || DEFAULT_MODEL
	const adapter = MODEL_ADAPTERS.find(({ prefix }) => model.toLowerCase().startsWith(prefix))
	if (!adapter) {
		throw new TranslatorNotConfiguredError(
			`TRANSLATION_MODEL '${model}' names no provider. A model name must start with: ${MODEL_ADAPTERS.map(({ prefix }) => `'${prefix}'`).join(', ')}.`,
		)
	}

	return adapter.create(config)
}
