#!/usr/bin/env node
// Prints the DNS records to publish in the job summary (deploy-environment.yml;
// production only, via the step's `if:`).
//
// Meaningful only the first time a production account is stood up (issue #30
// "Human work" H7), and harmless every other run: printing it again costs
// nothing and needs no "is this the first run" check.
import { appendSummary, exec as realExec, isMain } from '../../lib/actions.mjs'

export function summary(recordsJson) {
	return ['### DNS records to publish (issue #30 "Human work" H7)', '', '```json', recordsJson, '```', ''].join('\n')
}

export function main({ env = process.env, exec = realExec } = {}) {
	const records = exec('terraform', ['-chdir=infra', 'output', '-json', 'dns_records_to_publish'], { check: true }).stdout
	appendSummary(summary(records), env)
	return 0
}

if (isMain(import.meta.url)) process.exit(main())
