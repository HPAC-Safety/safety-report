#!/usr/bin/env node
// Puts the permanent Super Administrator's value into Secrets Manager
// (deploy-environment.yml; #909, ADR-0200, CON-INF-028).
//
// Optional: the SUPER_ADMIN environment secret may be absent or blank. Then a
// single space is put instead of skipping, so removing the GitHub secret retires
// the Super Administrator at the next deploy (the API treats a blank value as
// none: SuperAdminSecret.ResolveAsync; Secrets Manager refuses an empty string).
// The entry itself is created by Terraform (secrets.tf's
// `secret_entries.super_admin`). The value travels on stdin, never in argv, and
// is never echoed, logged, read back, or put in an error message.
//
// Environment: SUPER_ADMIN.
import { type Env, type Exec, exec as realExec, isMain } from '../../lib/actions.ts'

/** `jq -r '.name'`: the string, `null` text for a missing or null key. */
export function entry(json: string, name: string): string {
	const value = (JSON.parse(json) as Record<string, string | number | boolean | null | undefined>)[name]
	return value === undefined || value === null ? 'null' : String(value)
}

export function main({ env = process.env, exec = realExec, log = console.log }: { env?: Env; exec?: Exec; log?: (line: string) => void } = {}): number {
	const value = (env.SUPER_ADMIN ?? '').trim()
	const secretString = value === '' ? ' ' : value
	const entries = exec('terraform', ['-chdir=infra', 'output', '-json', 'secret_entries'], { check: true }).stdout
	const secretId = entry(entries, 'super_admin')
	// No `check`: exec's own error would carry the arguments' text and stderr.
	let status: number
	try {
		status = exec('aws', ['secretsmanager', 'put-secret-value', '--secret-id', secretId, '--secret-string', 'file:///dev/stdin'], { input: secretString }).status
	} catch {
		throw new Error('Putting the Super Administrator into Secrets Manager failed.')
	}
	if (status !== 0) throw new Error(`Putting the Super Administrator into Secrets Manager failed (aws exited ${status}).`)
	log(value === '' ? 'No SUPER_ADMIN secret set; the Super Administrator entry is now blank.' : 'Super Administrator refreshed in Secrets Manager. The value is never echoed.')
	return 0
}

if (isMain(import.meta.url)) process.exit(main())
