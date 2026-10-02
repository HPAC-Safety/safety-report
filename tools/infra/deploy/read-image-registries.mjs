#!/usr/bin/env node
// Reads the ECR repository URLs out of Terraform state and writes `registry`,
// `api_repo`, and `worker_repo` to $GITHUB_OUTPUT (deploy-environment.yml).
//
// Read from state, not from `deploy_variables`: that output also names the
// Lambda functions and buckets, which do not exist yet on a first release.
import { exec as realExec, isMain, setOutput } from '../../lib/actions.mjs'

/** `jq -r` of one repository's URL: the first match's value, `null` text when null, empty when absent. */
export function repositoryUrl(stateJson, key) {
	const resources = JSON.parse(stateJson).values.root_module.resources
	const address = `aws_ecr_repository.this["${key}"]`
	const found = resources.find((resource) => resource.address === address)
	if (!found) return ''
	const url = found.values.repository_url
	return url === null || url === undefined ? 'null' : String(url)
}

/** `${repo%%/*}`: the registry host, everything before the first slash. */
export function registryOf(repo) {
	const slash = repo.indexOf('/')
	return slash === -1 ? repo : repo.slice(0, slash)
}

export function main({ env = process.env, exec = realExec } = {}) {
	const state = exec('terraform', ['-chdir=infra', 'show', '-json'], { check: true }).stdout
	const apiRepo = repositoryUrl(state, 'api')
	const workerRepo = repositoryUrl(state, 'worker')
	setOutput('registry', registryOf(apiRepo), env)
	setOutput('api_repo', apiRepo, env)
	setOutput('worker_repo', workerRepo, env)
	return 0
}

if (isMain(import.meta.url)) process.exit(main())
