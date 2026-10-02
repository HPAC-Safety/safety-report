#!/usr/bin/env node
// Decides whether terraform.yml's `plan` job can run for one account, and
// writes `ready=true|false` to $GITHUB_OUTPUT. It never fails: a plan that
// cannot run is skipped with a notice, not a red check (ADR-0011, ADR-0032).
//
// A fork pull request gets no secrets and a read-only token, so it can neither
// assume a role nor post a comment. Checking explicitly rather than relying on
// that, because "GitHub happens not to hand it over" is not a security
// boundary anyone should have to look up.
//
// The role is hpac-safety-plan, NOT hpac-safety-deploy. The deploy role trusts
// only the release job's own environment (ADR-0158), and a pull_request run
// presents "repo:ORG@ID/REPO@ID:pull_request" — widening the deploy role so it
// could assume it is precisely what ADR-0032 forbids.
//
// Environment: PLAN_ROLE_ARN, STATE_BUCKET, SAME_REPO ('true' for a same-repo
// pull request), ACCOUNT (staging), ACCOUNT_UPPER (STAGING).
import { isMain, setOutput } from '../lib/actions.mjs'

/** `{ ready, notice }` for the inputs; `notice` is the annotation line to print, or null. */
export function decide({ planRoleArn, stateBucket, sameRepo, account, accountUpper }) {
	if (sameRepo !== 'true') {
		return {
			ready: false,
			notice:
				'::notice::Fork pull request. terraform plan needs an AWS role a fork cannot be given, so it is skipped. The infra job above still validated the configuration.',
		}
	}
	if (!planRoleArn || !stateBucket) {
		return {
			ready: false,
			notice: `::notice::AWS_PLAN_ROLE_ARN_${accountUpper} or TF_STATE_BUCKET_${accountUpper} is not set, so ${account} has not been bootstrapped yet. Skipping terraform plan. Creating it is #464/#465; infra/README.md has the runbook.`,
		}
	}
	return { ready: true, notice: null }
}

export function main({ env = process.env, log = console.log } = {}) {
	const { ready, notice } = decide({
		planRoleArn: env.PLAN_ROLE_ARN,
		stateBucket: env.STATE_BUCKET,
		sameRepo: env.SAME_REPO,
		account: env.ACCOUNT,
		accountUpper: env.ACCOUNT_UPPER,
	})
	if (notice) log(notice)
	setOutput('ready', ready ? 'true' : 'false', env)
	return 0
}

if (isMain(import.meta.url)) process.exit(main())
