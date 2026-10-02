import { describe, it } from 'node:test'
import assert from 'node:assert/strict'

import { main as digests, summary as digestSummary } from '../../../../tools/infra/deploy/report-image-digests.mjs'
import { main as dns, summary as dnsSummary } from '../../../../tools/infra/deploy/report-dns-records.mjs'
import { fakeExec, tempFile } from '../fake-exec.mjs'

describe('report-image-digests', () => {
	it('lists both digests under the environment heading', () => {
		assert.equal(
			digestSummary({ environmentName: 'hpac-safety-staging', apiDigest: 'sha256:a', workerDigest: 'sha256:w' }),
			'### Image digests — hpac-safety-staging\n- API: `sha256:a`\n- Worker: `sha256:w`\n',
		)
	})

	it('appends to the step summary', () => {
		const out = tempFile('summary')
		digests({ env: { GITHUB_STEP_SUMMARY: out.file, ENVIRONMENT_NAME: 'e', API_DIGEST: 'a', WORKER_DIGEST: 'w' } })
		assert.equal(out.read(), '### Image digests — e\n- API: `a`\n- Worker: `w`\n')
	})
})

describe('report-dns-records', () => {
	it('fences the records as JSON', () => {
		assert.equal(dnsSummary('{}'), '### DNS records to publish (issue #30 "Human work" H7)\n\n```json\n{}\n```\n')
	})

	it('appends the terraform output to the step summary', () => {
		const out = tempFile('summary')
		const { exec } = fakeExec({ 'terraform -chdir=infra output -json dns_records_to_publish': { stdout: '{"a":1}' } })
		dns({ env: { GITHUB_STEP_SUMMARY: out.file }, exec })
		assert.match(out.read(), /```json\n\{"a":1\}\n```\n$/)
	})
})
