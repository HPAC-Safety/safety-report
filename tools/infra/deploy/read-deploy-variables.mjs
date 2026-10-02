#!/usr/bin/env node
// Fans Terraform's `deploy_variables` output into $GITHUB_OUTPUT, one
// `KEY=value` per entry, for the later steps to read as step outputs of the `tf` step
// (deploy-environment.yml). tools/infra/check-terraform-outputs.mjs checks
// those reads against infra/outputs.tf.
import { exec as realExec, isMain, setOutput } from '../../lib/actions.mjs'

/** `jq`'s `\(.value)`: a string as is, anything else as compact JSON. */
export function render(value) {
	return typeof value === 'string' ? value : JSON.stringify(value)
}

export function main({ env = process.env, exec = realExec } = {}) {
	const json = exec('terraform', ['-chdir=infra', 'output', '-json', 'deploy_variables'], { check: true }).stdout
	for (const [key, value] of Object.entries(JSON.parse(json))) setOutput(key, render(value), env)
	return 0
}

if (isMain(import.meta.url)) process.exit(main())
