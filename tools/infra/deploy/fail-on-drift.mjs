#!/usr/bin/env node
// Re-plans after the apply and fails the job on drift (deploy-environment.yml;
// CON-INF-013 / ADR-0158).
//
// Fails the job if applying planned to (or, after applying, still would)
// destroy anything other than the NAT instance — a destroyed RDS instance,
// uploads bucket, secret, or log group is exactly what `prevent_destroy` and
// deletion protection exist to make impossible, and this is the release-time
// check that nothing slipped past them. `-detailed-exitcode`: 0 no changes,
// 1 error, 2 changes pending (drift).
//
// Environment: TFVARS_FILE (repository-relative), GITHUB_WORKSPACE. -var-file
// is absolute because Terraform resolves it after -chdir=infra (#615).
import { exec as realExec, isMain, required } from '../../lib/actions.mjs'

export const DRIFT_ERROR =
	'::error::terraform apply did not converge — a plan immediately afterwards is not empty. See ADR-0158 / CON-INF-013: every release re-plans after applying and fails on drift.'

/** The exit code for a `terraform plan -detailed-exitcode` status, and what to print. */
export function judge(code) {
	if (code === 0) return { exit: 0, message: 'Converged: a plan right after apply is empty.' }
	if (code === 2) return { exit: 1, message: DRIFT_ERROR }
	return { exit: code, message: null }
}

export function main({ env = process.env, exec = realExec, log = console.log } = {}) {
	const varFile = `${required(env, 'GITHUB_WORKSPACE')}/${required(env, 'TFVARS_FILE')}`
	const plan = exec('terraform', ['-chdir=infra', 'plan', '-no-color', '-detailed-exitcode', `-var-file=${varFile}`], { inherit: true })
	const { exit, message } = judge(plan.status)
	if (message) log(message)
	return exit
}

if (isMain(import.meta.url)) process.exit(main())
