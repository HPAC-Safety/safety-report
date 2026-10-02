import { describe, it } from 'node:test'
import assert from 'node:assert/strict'

import { main, registryOf, repositoryUrl } from '../../../../tools/infra/deploy/read-image-registries.mjs'
import { fakeExec, tempFile } from '../fake-exec.mjs'

const state = JSON.stringify({
	values: {
		root_module: {
			resources: [
				{ address: 'aws_ecr_repository.this["api"]', values: { repository_url: '123.dkr.ecr.ca-central-1.amazonaws.com/api' } },
				{ address: 'aws_ecr_repository.this["worker"]', values: { repository_url: '123.dkr.ecr.ca-central-1.amazonaws.com/worker' } },
			],
		},
	},
})

describe('repositoryUrl', () => {
	it('finds a repository by its address', () => {
		assert.equal(repositoryUrl(state, 'worker'), '123.dkr.ecr.ca-central-1.amazonaws.com/worker')
	})

	it('is empty when the repository is absent, like jq printing nothing', () => {
		assert.equal(repositoryUrl(state, 'other'), '')
	})
})

describe('registryOf', () => {
	it('keeps everything before the first slash', () => {
		assert.equal(registryOf('host/a/b'), 'host')
		assert.equal(registryOf('nohost'), 'nohost')
	})
})

describe('read-image-registries', () => {
	it('writes registry, api_repo, and worker_repo', () => {
		const out = tempFile('out')
		const { exec } = fakeExec({ 'terraform -chdir=infra show -json': { stdout: state } })
		assert.equal(main({ env: { GITHUB_OUTPUT: out.file }, exec }), 0)
		assert.equal(
			out.read(),
			'registry=123.dkr.ecr.ca-central-1.amazonaws.com\napi_repo=123.dkr.ecr.ca-central-1.amazonaws.com/api\nworker_repo=123.dkr.ecr.ca-central-1.amazonaws.com/worker\n',
		)
	})
})
