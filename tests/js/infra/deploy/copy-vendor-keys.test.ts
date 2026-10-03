import { describe, it } from 'node:test'
import assert from 'node:assert/strict'

import { entry, main } from '../../../../tools/infra/deploy/copy-vendor-keys.ts'
import { fakeExec } from '../fake-exec.ts'

describe('entry', () => {
	it('reads a key, and says null for a missing one like jq -r', () => {
		assert.equal(entry('{"a":"arn:a"}', 'a'), 'arn:a')
		assert.equal(entry('{}', 'a'), 'null')
	})
})

describe('copy-vendor-keys', () => {
	it('puts each vendor key into its secret and never logs the values', () => {
		const { exec, calls } = fakeExec({
			'terraform -chdir=infra output -json secret_entries': { stdout: '{"gemini_api_key":"arn:g","deepl_api_key":"arn:d"}' },
		})
		const logs: string[] = []
		assert.equal(main({ env: { GEMINI_API_KEY: 'gk', DEEPL_API_KEY: 'dk' }, exec, log: (m) => logs.push(m) }), 0)
		assert.deepEqual(calls, [
			'terraform -chdir=infra output -json secret_entries',
			'terraform -chdir=infra output -json secret_entries',
			'aws secretsmanager put-secret-value --secret-id arn:g --secret-string gk',
			'aws secretsmanager put-secret-value --secret-id arn:d --secret-string dk',
		])
		assert.deepEqual(logs, ['Vendor keys refreshed in Secrets Manager. Values are never echoed.'])
	})

	it('fails when a put fails', () => {
		const { exec } = fakeExec({
			'terraform -chdir=infra output -json secret_entries': { stdout: '{"gemini_api_key":"arn:g","deepl_api_key":"arn:d"}' },
			'aws secretsmanager put-secret-value --secret-id arn:g --secret-string gk': { status: 1 },
		})
		assert.throws(() => main({ env: { GEMINI_API_KEY: 'gk', DEEPL_API_KEY: 'dk' }, exec, log() {} }))
	})
})
