#!/usr/bin/env node
// Generates the temporary interim issuer's one signing key, if it does not
// exist yet (deploy-environment.yml; issue #648, ADR-0172, TEMPORARY: deleted
// along with the whole feature once a real identity provider exists).
//
// Unlike the vendor keys, nobody types this value: it is an RSA private key PEM
// this step generates itself, put only if the entry has no AWSCURRENT version
// yet, and never read back or echoed. `interim_issuer_signing_key_secret` is
// empty where var.interim_issuer_enabled is false (production), so this is a
// no-op there. The deploy role can put a secret value but never get one
// (ADR-0171) — this step only ever writes.
//
// Environment: ENVIRONMENT_NAME.
import { exec as realExec, isMain } from '../../lib/actions.mjs'

/** `grep -c AWSCURRENT`: how many lines of `describe-secret` text name the current version. */
export function currentVersions(text) {
	return text.split('\n').filter((line) => line.includes('AWSCURRENT')).length
}

export function main({ env = process.env, exec = realExec, log = console.log } = {}) {
	const secretId = exec('terraform', ['-chdir=infra', 'output', '-raw', 'interim_issuer_signing_key_secret'], { check: true }).stdout
	if (secretId === '') {
		log(`Interim issuer disabled for ${env.ENVIRONMENT_NAME}; nothing to generate.`)
		return 0
	}
	// A failed describe counts as no current version (`|| true`).
	const described = exec('aws', ['secretsmanager', 'describe-secret', '--secret-id', secretId, '--query', 'VersionIdsToStages', '--output', 'text'])
	if (currentVersions(described.status === 0 ? described.stdout : '') > 0) {
		log('Interim issuer signing key already exists; leaving it as it is.')
		return 0
	}
	const keyPem = exec('openssl', ['genpkey', '-algorithm', 'RSA', '-pkeyopt', 'rsa_keygen_bits:2048'], { check: true }).stdout
	exec('aws', ['secretsmanager', 'put-secret-value', '--secret-id', secretId, '--secret-string', keyPem], { check: true })
	log('Interim issuer signing key generated. The value is never echoed or read back.')
	return 0
}

if (isMain(import.meta.url)) process.exit(main())
