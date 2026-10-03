#!/usr/bin/env node
// Loads the release's built API and Worker image tarballs and pushes them to
// this account's ECR, writing `api_digest` and `worker_digest` to
// $GITHUB_OUTPUT (deploy-environment.yml).
//
// Tagged with the commit SHA (what a rollback names) and `latest` (what
// lambda.tf's `image_uri` names on a function's first creation; ignored after
// that — the functions are updated by digest afterwards).
//
// Environment: SHA, API_REPO, WORKER_REPO.
import { type Env, type Exec, exec as realExec, isMain, required, setOutput } from '../../lib/actions.ts'

/** `docker inspect` output (`repo@sha256:...`) reduced to the digest, as `cut -d@ -f2` does. */
export function digestOf(repoDigest: string): string {
	const parts = repoDigest.split('@')
	return parts.length > 1 ? parts[1] : repoDigest
}

export function main({ env = process.env, exec = realExec }: { env?: Env; exec?: Exec } = {}): number {
	const sha = required(env, 'SHA')
	const apiRepo = required(env, 'API_REPO')
	const workerRepo = required(env, 'WORKER_REPO')
	const docker = (...args: string[]) => exec('docker', args, { check: true, inherit: true })

	docker('load', '-i', 'artifacts/release/api-image/api-image.tar.gz')
	docker('load', '-i', 'artifacts/release/worker-image/worker-image.tar.gz')

	const apiImage = `${apiRepo}:${sha}`
	const workerImage = `${workerRepo}:${sha}`

	docker('tag', `hpac-safety-api:${sha}`, apiImage)
	docker('tag', `hpac-safety-api:${sha}`, `${apiRepo}:latest`)
	docker('tag', `hpac-safety-worker:${sha}`, workerImage)
	docker('tag', `hpac-safety-worker:${sha}`, `${workerRepo}:latest`)
	docker('push', apiImage)
	docker('push', `${apiRepo}:latest`)
	docker('push', workerImage)
	docker('push', `${workerRepo}:latest`)

	const inspect = (image: string) => exec('docker', ['inspect', '--format={{index .RepoDigests 0}}', image], { check: true }).stdout
	const apiDigest = digestOf(inspect(apiImage))
	const workerDigest = digestOf(inspect(workerImage))
	setOutput('api_digest', apiDigest, env)
	setOutput('worker_digest', workerDigest, env)
	return 0
}

if (isMain(import.meta.url)) process.exit(main())
