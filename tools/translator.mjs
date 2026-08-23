#!/usr/bin/env node
/**
 * ITranslator, for the CI locale job. **This is the one file to change to swap
 * translation provider.**
 *
 * It is the JavaScript counterpart of `HpacSafety.Core.SharedKernel.ITranslator`
 * — the same port, the same direction-agnostic contract — because the two live
 * in different runtimes and cannot share a type. The .NET one runs in the worker
 * and translates already-anonymized summaries and question wording. This one
 * runs in GitHub Actions and translates UI chrome out of `locales/en-CA.json`.
 *
 * **A raw report never reaches either.** Invariant 4 in `AGENTS.md`: raw reports
 * are never translated and never leave the system. Nothing here ever sees report
 * data — its only input is a JSON file of UI labels that ships in the repository.
 *
 * ## The port
 *
 *   translate(items, { source, target }) -> Promise<Map<key, string>>
 *
 * where `items` is `[{ key, text }]`. Every key in one call, one request: the
 * batching is the caller's, not the provider's, so a per-request-priced vendor
 * and a per-token one cost the same shape of money.
 *
 * ## Providers
 *
 * | name | what it is |
 * |---|---|
 * | `deepl` | **The default.** DeepL, targeting `FR-CA`. See ADR-0022. |
 * | `chat-completions` | Any OpenAI-shaped `/chat/completions` endpoint. Endpoint, model, and key are configuration — this adapter names no vendor. Kept so a second provider is a config change, not a rewrite. |
 * | `stub` | Offline stand-in for the test suite. Stamps `provider: "stub"`, which `--check` rejects, so its output can never reach `main`. |
 *
 * GitHub Models was the provider named in ADR-0007. It was **fully retired on
 * 30 July 2026** — playground, catalogue, and inference API alike — so no
 * adapter for it ships here. DeepL replaces it (ADR-0022).
 *
 * Configuration comes from the environment:
 *
 *   TRANSLATION_PROVIDER    deepl (default) | chat-completions | stub
 *   DEEPL_API_KEY           DeepL auth key. Free keys end in ":fx".
 *   TRANSLATION_FORMALITY   DeepL formality; defaults to prefer_more
 *   TRANSLATION_ENDPOINT    overrides the endpoint (chat-completions needs it)
 *   TRANSLATION_MODEL       chat-completions model identifier
 *   TRANSLATION_API_KEY     chat-completions bearer token
 */

/** Raised when the job has no provider to call. Never a silent fallback. */
export class TranslatorNotConfiguredError extends Error {
	constructor(message) {
		super(message)
		this.name = 'TranslatorNotConfiguredError'
	}
}

/** Reads provider configuration out of the environment. */
export function configFromEnv(env = process.env) {
	return {
		provider: env.TRANSLATION_PROVIDER,
		endpoint: env.TRANSLATION_ENDPOINT,
		model: env.TRANSLATION_MODEL,
		formality: env.TRANSLATION_FORMALITY,
		// DEEPL_API_KEY is the name the owner adds in repository settings, so it
		// says what it is. TRANSLATION_API_KEY stays as the provider-neutral name
		// the chat-completions adapter uses.
		apiKey: env.DEEPL_API_KEY || env.TRANSLATION_API_KEY,
	}
}

const SYSTEM_PROMPT = (source, target) =>
	[
		`You are translating user-interface strings for a Canadian aviation safety`,
		`reporting system from ${source} to ${target}.`,
		'',
		'Rules:',
		'- Reply with a single JSON object mapping each input key to its translation.',
		'- No prose, no explanation, no markdown fence.',
		'- Preserve placeholders such as {count} and {date} exactly, including case.',
		'- Preserve leading and trailing whitespace and any HTML tags.',
		'- These are short interface labels. Translate them as labels, not sentences.',
		'- Use Canadian French conventions.',
	].join('\n')

/**
 * Pulls the JSON object out of a model reply. Models fence their output more
 * often than not, and a fenced reply is a correct reply wrapped in decoration —
 * failing on it would burn a run for nothing. Anything that is not JSON at all
 * fails loudly instead, because writing prose into a locale file is worse than
 * a red build.
 */
function parseJsonObject(content) {
	const text = String(content ?? '').trim()
	const fenced = text.match(/^```(?:json)?\s*\n([\s\S]*?)\n?```$/)
	const candidate = (fenced ? fenced[1] : text).trim()

	let parsed
	try {
		parsed = JSON.parse(candidate)
	} catch {
		throw new Error(
			`The provider did not return JSON. First 200 characters: ${text.slice(0, 200)}`,
		)
	}

	if (parsed === null || typeof parsed !== 'object' || Array.isArray(parsed)) {
		throw new Error('The provider returned JSON that is not an object of key to translation.')
	}

	return new Map(Object.entries(parsed).map(([key, value]) => [key, String(value)]))
}

/** Any OpenAI-shaped `/chat/completions` endpoint. Names no vendor. */
function chatCompletionsTranslator({ endpoint, model, apiKey }) {
	const missing = []
	if (!endpoint) missing.push('TRANSLATION_ENDPOINT')
	if (!model) missing.push('TRANSLATION_MODEL')
	if (!apiKey) missing.push('TRANSLATION_API_KEY')
	if (missing.length > 0) {
		throw new TranslatorNotConfiguredError(
			`The chat-completions translator needs ${missing.join(', ')}. See ADR-0022.`,
		)
	}

	const buildRequest = (items, { source, target }) => ({
		model,
		// Deterministic where the provider honours it: the same English twice
		// should not produce two different French strings and a spurious diff.
		temperature: 0,
		messages: [
			{ role: 'system', content: SYSTEM_PROMPT(source, target) },
			{
				role: 'user',
				content: JSON.stringify(Object.fromEntries(items.map(({ key, text }) => [key, text]))),
			},
		],
	})

	return {
		name: `chat-completions:${model}`,
		buildRequest,
		parseResponse: parseJsonObject,
		async translate(items, locales) {
			const response = await fetch(endpoint, {
				method: 'POST',
				headers: {
					'content-type': 'application/json',
					authorization: `Bearer ${apiKey}`,
				},
				body: JSON.stringify(buildRequest(items, locales)),
			})

			if (!response.ok) {
				// The body can echo the request, and the request is UI labels, not
				// report data — but it can also echo a header. Only the status is
				// reported, never the body, and never the key.
				throw new Error(`The translation provider answered ${response.status} ${response.statusText}.`)
			}

			const payload = await response.json()
			return parseJsonObject(payload?.choices?.[0]?.message?.content)
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
		buildRequest: (items) => ({ model: 'stub', messages: [], items }),
		parseResponse: parseJsonObject,
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
const DEEPL_CODES = {
	'en-CA': 'EN',
	'fr-CA': 'FR-CA',
}

/** DeepL, the provider decided in ADR-0022. */
function deeplTranslator({ apiKey, endpoint, formality }) {
	if (!apiKey) {
		throw new TranslatorNotConfiguredError(
			'The DeepL translator needs DEEPL_API_KEY. Add it as a repository secret. See ADR-0022.',
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

	const buildRequest = (items, { source, target }) => ({
		text: items.map(({ text }) => text),
		source_lang: codeFor(source),
		target_lang: codeFor(target),
		formality: chosenFormality,
		// These are interface labels. DeepL "correcting" the capitalisation or
		// the trailing space of a label is a change nobody asked for.
		preserve_formatting: true,
	})

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
		return new Map(items.map(({ key }, index) => [key, String(translations[index].text)]))
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
 * Builds the translator the environment asks for.
 *
 * @throws {TranslatorNotConfiguredError} when nothing is configured, or when a
 *   configured provider is missing a setting. There is no default provider: a
 *   job that quietly picked one would put unattributed French in front of a
 *   reviewer who had no way to know which machine wrote it.
 */
export function createTranslator(config = {}) {
	// DeepL is the decided provider (ADR-0022), so defaulting to it is a recorded
	// decision rather than a guess. What can still be missing is the credential,
	// and that failure names the secret to add.
	const provider = config.provider || 'deepl'

	switch (provider) {
		case 'deepl':
			return deeplTranslator(config)
		case 'chat-completions':
			return chatCompletionsTranslator(config)
		case 'stub':
			return stubTranslator()
		default:
			throw new TranslatorNotConfiguredError(
				`Unknown TRANSLATION_PROVIDER '${provider}'. Known providers: deepl, chat-completions, stub.`,
			)
	}
}
