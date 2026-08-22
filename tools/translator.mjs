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
 * | `chat-completions` | Any OpenAI-shaped `/chat/completions` endpoint. Endpoint, model, and key are configuration — this adapter names no vendor. |
 * | `stub` | Offline stand-in for the test suite. Stamps `provider: "stub"`, which `--check` rejects, so its output can never reach `main`. |
 *
 * GitHub Models was the provider named in ADR-0007. It was **fully retired on
 * 30 July 2026** — playground, catalogue, and inference API alike — so no
 * adapter for it ships here. See ADR-0022 for what replaces it and what is
 * still open.
 *
 * Configuration comes from the environment, so the workflow supplies it and no
 * vendor name is compiled in:
 *
 *   TRANSLATION_PROVIDER   chat-completions | stub
 *   TRANSLATION_ENDPOINT   full URL of the chat-completions endpoint
 *   TRANSLATION_MODEL      provider's model identifier
 *   TRANSLATION_API_KEY    bearer token
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
		apiKey: env.TRANSLATION_API_KEY,
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

/**
 * Builds the translator the environment asks for.
 *
 * @throws {TranslatorNotConfiguredError} when nothing is configured, or when a
 *   configured provider is missing a setting. There is no default provider: a
 *   job that quietly picked one would put unattributed French in front of a
 *   reviewer who had no way to know which machine wrote it.
 */
export function createTranslator(config = {}) {
	const { provider } = config

	if (!provider) {
		throw new TranslatorNotConfiguredError(
			'No TRANSLATION_PROVIDER is set, so there is no translator to build. See ADR-0022.',
		)
	}

	switch (provider) {
		case 'chat-completions':
			return chatCompletionsTranslator(config)
		case 'stub':
			return stubTranslator()
		default:
			throw new TranslatorNotConfiguredError(
				`Unknown TRANSLATION_PROVIDER '${provider}'. Known providers: chat-completions, stub.`,
			)
	}
}
