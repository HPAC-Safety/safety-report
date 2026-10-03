import { describe, it } from 'node:test'
import assert from 'node:assert/strict'

import { digestOf, main } from '../../../../tools/infra/deploy/push-images.ts'
import { fakeExec, tempFile } from '../fake-exec.ts'

describe('digestOf', () => {
	it('takes the part after the @', () => {
		assert.equal(digestOf('repo@sha256:abc'), 'sha256:abc')
		assert.equal(digestOf('nothing'), 'nothing')
	})
})

describe('push-images', () => {
	const env = { SHA: 'abc123', API_REPO: 'r/api', WORKER_REPO: 'r/worker' }

	it('loads, tags, pushes in order, then records both digests', () => {
		const out = tempFile('out')
		const { exec, calls } = fakeExec({
			'docker inspect --format={{index .RepoDigests 0}} r/api:abc123': { stdout: 'r/api@sha256:aaa' },
			'docker inspect --format={{index .RepoDigests 0}} r/worker:abc123': { stdout: 'r/worker@sha256:bbb' },
		})
		assert.equal(main({ env: { ...env, GITHUB_OUTPUT: out.file }, exec }), 0)
		assert.deepEqual(calls, [
			'docker load -i artifacts/release/api-image/api-image.tar.gz',
			'docker load -i artifacts/release/worker-image/worker-image.tar.gz',
			'docker tag hpac-safety-api:abc123 r/api:abc123',
			'docker tag hpac-safety-api:abc123 r/api:latest',
			'docker tag hpac-safety-worker:abc123 r/worker:abc123',
			'docker tag hpac-safety-worker:abc123 r/worker:latest',
			'docker push r/api:abc123',
			'docker push r/api:latest',
			'docker push r/worker:abc123',
			'docker push r/worker:latest',
			'docker inspect --format={{index .RepoDigests 0}} r/api:abc123',
			'docker inspect --format={{index .RepoDigests 0}} r/worker:abc123',
		])
		assert.equal(out.read(), 'api_digest=sha256:aaa\nworker_digest=sha256:bbb\n')
	})

	it('stops at the first failing push', () => {
		const { exec, calls } = fakeExec({ 'docker push r/api:latest': { status: 1 } })
		assert.throws(() => main({ env, exec }))
		assert.equal(calls.at(-1), 'docker push r/api:latest')
	})
})
