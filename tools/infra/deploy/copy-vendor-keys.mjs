#!/usr/bin/env node
// Copies the vendor API keys into Secrets Manager (deploy-environment.yml).
//
// secrets.tf's `secret_entries` output is one map holding every entry
// Terraform created (issue #597 covers the Lambdas reading each by ARN at
// startup — this step only ever supplies the vendor keys' values, never sets
// anything on either function). Values are never echoed.
//
// Environment: GEMINI_API_KEY, DEEPL_API_KEY.
import { exec as realExec, isMain } from '../../lib/actions.mjs'

/** `jq -r '.name'`: the string, `null` text for a missing or null key. */
export function entry(json, name) {
	const value = JSON.parse(json)[name]
	return value === undefined || value === null ? 'null' : String(value)
}

export function main({ env = process.env, exec = realExec, log = console.log } = {}) {
	const entries = () => exec('terraform', ['-chdir=infra', 'output', '-json', 'secret_entries'], { check: true }).stdout
	const geminiSecretId = entry(entries(), 'gemini_api_key')
	const deeplSecretId = entry(entries(), 'deepl_api_key')
	const put = (id, value) =>
		exec('aws', ['secretsmanager', 'put-secret-value', '--secret-id', id, '--secret-string', value], { check: true })
	put(geminiSecretId, env.GEMINI_API_KEY ?? '')
	put(deeplSecretId, env.DEEPL_API_KEY ?? '')
	log('Vendor keys refreshed in Secrets Manager. Values are never echoed.')
	return 0
}

if (isMain(import.meta.url)) process.exit(main())
