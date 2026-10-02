import { describe, it } from 'node:test'
import assert from 'node:assert/strict'

import { checkBlock, main } from '../../../tools/infra/require-config.mjs'

function run(env) {
	const logs = []
	const code = main({ env, log: (m) => logs.push(m) })
	return { code, logs }
}

describe('checkBlock', () => {
	it('accepts present values and skips blank lines', () => {
		assert.deepEqual(checkBlock('secret', 'A=1\n\n   \nB=two words\n', 'ctx'), [])
	})

	it('names a missing value with the context', () => {
		assert.deepEqual(checkBlock('variable', 'A=\n', 'See docs.'), ["::error::Missing variable 'A'. See docs."])
	})

	it('flags a line with no equals sign as malformed', () => {
		assert.deepEqual(checkBlock('secret', 'JUSTANAME', 'ctx'), ["::error::require-config: malformed secret entry 'JUSTANAME' (expected NAME=value)."])
	})

	it('keeps an equals sign inside the value', () => {
		assert.deepEqual(checkBlock('secret', 'A=b=c', 'ctx'), [])
	})
})

describe('require-config', () => {
	it('passes when everything is present', () => {
		const { code, logs } = run({ REQUIRED_SECRETS: 'A=1', REQUIRED_VARIABLES: 'B=2', FAILURE_CONTEXT: 'ctx' })
		assert.equal(code, 0)
		assert.deepEqual(logs, ['All required configuration is present.'])
	})

	it('passes with empty inputs', () => {
		assert.equal(run({ REQUIRED_SECRETS: '', REQUIRED_VARIABLES: '', FAILURE_CONTEXT: '' }).code, 0)
	})

	it('reports every missing value, then the bootstrap note, and fails', () => {
		const { code, logs } = run({ REQUIRED_SECRETS: 'A=\nB=1', REQUIRED_VARIABLES: 'C=', FAILURE_CONTEXT: 'Bootstrap it.' })
		assert.equal(code, 1)
		assert.deepEqual(logs, [
			"::error::Missing secret 'A'. Bootstrap it.",
			"::error::Missing variable 'C'. Bootstrap it.",
			'',
			'The AWS environment has not been created yet. It is built by',
			'#32 (bootstrap) and wired up by #30 (live deployment).',
		])
	})
})
