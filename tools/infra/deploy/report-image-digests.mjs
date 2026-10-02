#!/usr/bin/env node
// Records this environment's image digests in the job summary
// (deploy-environment.yml).
//
// Same content pushed by every environment (the build job produced one tarball
// each, loaded unchanged here) — same manifest digest in every account's ECR.
// This is what "the same image digests, never a rebuild" (CON-INF-012)
// actually rests on: content addressing, not trust.
//
// Environment: ENVIRONMENT_NAME, API_DIGEST, WORKER_DIGEST.
import { appendSummary, isMain } from '../../lib/actions.mjs'

export function summary({ environmentName, apiDigest, workerDigest }) {
	return [`### Image digests — ${environmentName}`, `- API: \`${apiDigest}\``, `- Worker: \`${workerDigest}\``, ''].join('\n')
}

export function main({ env = process.env } = {}) {
	appendSummary(summary({ environmentName: env.ENVIRONMENT_NAME, apiDigest: env.API_DIGEST, workerDigest: env.WORKER_DIGEST }), env)
	return 0
}

if (isMain(import.meta.url)) process.exit(main())
