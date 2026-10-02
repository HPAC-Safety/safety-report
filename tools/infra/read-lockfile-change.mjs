#!/usr/bin/env node
// Writes `changed=true|false` to $GITHUB_OUTPUT: whether the relock run changed
// infra/.terraform.lock.hcl (terraform-relock.yml). `git diff --quiet` exits 0
// for no change and 1 for a change.
import { exec as realExec, isMain, setOutput } from '../lib/actions.mjs'

export function main({ env = process.env, exec = realExec } = {}) {
	const diff = exec('git', ['diff', '--quiet', '--', 'infra/.terraform.lock.hcl'])
	setOutput('changed', diff.status === 0 ? 'false' : 'true', env)
	return 0
}

if (isMain(import.meta.url)) process.exit(main())
