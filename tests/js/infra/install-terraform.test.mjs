import { describe, it } from 'node:test'
import assert from 'node:assert/strict'

import { main, readVersion, terraformUrl, tflintUrl } from '../../../tools/infra/install-terraform.mjs'
import { fakeExec } from './fake-exec.mjs'

const files = { 'infra/.terraform-version': '1.9.8\n', 'infra/.tflint-version': ' v0.55.0\n' }
const read = (file) => files[file]

describe('readVersion', () => {
	it('strips every whitespace character', () => {
		assert.equal(readVersion('v', () => ' 1.2.3 \n\t'), '1.2.3')
	})
})

describe('install-terraform', () => {
	it('installs Terraform only, then prints its version', () => {
		const { exec, calls } = fakeExec()
		const logs = []
		assert.equal(main({ argv: [], exec, log: (m) => logs.push(m), read }), 0)
		assert.deepEqual(calls, [
			`curl -fsSL -o /tmp/terraform.zip ${terraformUrl('1.9.8')}`,
			'unzip -q -o /tmp/terraform.zip -d /usr/local/bin',
			'chmod +x /usr/local/bin/terraform',
			'terraform version',
		])
		assert.deepEqual(logs, [])
	})

	it('with --tflint announces both versions and installs both before printing versions', () => {
		const { exec, calls } = fakeExec()
		const logs = []
		main({ argv: ['--tflint'], exec, log: (m) => logs.push(m), read })
		assert.deepEqual(logs, ['Terraform 1.9.8, tflint v0.55.0'])
		assert.deepEqual(calls, [
			`curl -fsSL -o /tmp/terraform.zip ${terraformUrl('1.9.8')}`,
			'unzip -q -o /tmp/terraform.zip -d /usr/local/bin',
			'chmod +x /usr/local/bin/terraform',
			`curl -fsSL -o /tmp/tflint.zip ${tflintUrl('v0.55.0')}`,
			'unzip -q -o /tmp/tflint.zip -d /usr/local/bin',
			'chmod +x /usr/local/bin/tflint',
			'terraform version',
			'tflint --version',
		])
	})

	it('fails when a download fails', () => {
		const { exec } = fakeExec({ [`curl -fsSL -o /tmp/terraform.zip ${terraformUrl('1.9.8')}`]: { status: 22 } })
		assert.throws(() => main({ argv: [], exec, log() {}, read }))
	})
})
