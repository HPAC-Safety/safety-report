import { describe, it } from 'node:test'
import assert from 'node:assert/strict'

import { currentVersions, main } from '../../../../tools/infra/deploy/generate-interim-issuer-key.mjs'
import { fakeExec } from '../fake-exec.mjs'

const secretOutput = 'terraform -chdir=infra output -raw interim_issuer_signing_key_secret'
const describeSecret = 'aws secretsmanager describe-secret --secret-id arn:k --query VersionIdsToStages --output text'
const genkey = 'openssl genpkey -algorithm RSA -pkeyopt rsa_keygen_bits:2048'
const put = 'aws secretsmanager put-secret-value --secret-id arn:k --secret-string PEM'
const env = { ENVIRONMENT_NAME: 'hpac-safety-staging' }

describe('currentVersions', () => {
	it('counts lines naming AWSCURRENT', () => {
		assert.equal(currentVersions('v1\tAWSPREVIOUS\nv2\tAWSCURRENT'), 1)
		assert.equal(currentVersions(''), 0)
	})
})

describe('generate-interim-issuer-key', () => {
	it('does nothing where the interim issuer is disabled', () => {
		const { exec, calls } = fakeExec({ [secretOutput]: { stdout: '' } })
		const logs = []
		assert.equal(main({ env, exec, log: (m) => logs.push(m) }), 0)
		assert.deepEqual(calls, [secretOutput])
		assert.deepEqual(logs, ['Interim issuer disabled for hpac-safety-staging; nothing to generate.'])
	})

	it('leaves an existing key alone', () => {
		const { exec, calls } = fakeExec({ [secretOutput]: { stdout: 'arn:k' }, [describeSecret]: { stdout: 'v1\tAWSCURRENT' } })
		const logs = []
		main({ env, exec, log: (m) => logs.push(m) })
		assert.deepEqual(calls, [secretOutput, describeSecret])
		assert.deepEqual(logs, ['Interim issuer signing key already exists; leaving it as it is.'])
	})

	it('generates and stores a key when none is current', () => {
		const { exec, calls } = fakeExec({ [secretOutput]: { stdout: 'arn:k' }, [describeSecret]: { stdout: 'v1\tAWSPREVIOUS' }, [genkey]: { stdout: 'PEM' } })
		const logs = []
		assert.equal(main({ env, exec, log: (m) => logs.push(m) }), 0)
		assert.deepEqual(calls, [secretOutput, describeSecret, genkey, put])
		assert.deepEqual(logs, ['Interim issuer signing key generated. The value is never echoed or read back.'])
	})

	it('treats a failed describe as no key yet', () => {
		const { exec, calls } = fakeExec({ [secretOutput]: { stdout: 'arn:k' }, [describeSecret]: { status: 254 }, [genkey]: { stdout: 'PEM' } })
		main({ env, exec, log() {} })
		assert.equal(calls.at(-1), put)
	})

	it('fails when the put fails', () => {
		const { exec } = fakeExec({ [secretOutput]: { stdout: 'arn:k' }, [genkey]: { stdout: 'PEM' }, [put]: { status: 1 } })
		assert.throws(() => main({ env, exec, log() {} }))
	})
})
