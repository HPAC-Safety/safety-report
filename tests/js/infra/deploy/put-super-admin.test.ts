import { describe, it } from 'node:test'
import assert from 'node:assert/strict'

import { entry, main } from '../../../../tools/infra/deploy/put-super-admin.ts'
import { fakeExec, type Reply } from '../fake-exec.ts'

const tf = 'terraform -chdir=infra output -json secret_entries'
const put = 'aws secretsmanager put-secret-value --secret-id arn:super --secret-string file:///dev/stdin'
const entries = { stdout: JSON.stringify({ super_admin: 'arn:super' }) }
const email = 'boss@example.test'

function run(env: Record<string, string | undefined>, awsReply: Reply = {}) {
	const inputs: (string | undefined)[] = []
	const { exec, calls } = fakeExec({
		[tf]: entries,
		[put]: (options) => {
			inputs.push(options.input)
			return typeof awsReply === 'function' ? awsReply(options) : awsReply
		},
	})
	const lines: string[] = []
	return { go: () => main({ env, exec, log: (l) => lines.push(l) }), calls, inputs, lines }
}

describe('entry', () => {
	it('reads a key, or null text when missing', () => {
		assert.equal(entry('{"a":"x"}', 'a'), 'x')
		assert.equal(entry('{}', 'a'), 'null')
	})
})

describe('main', () => {
	it('puts a set value on stdin, never in argv or output', () => {
		const r = run({ SUPER_ADMIN: `  ${email}\n` })
		assert.equal(r.go(), 0)
		assert.deepEqual(r.inputs, [email])
		assert.ok(!r.calls.join('\n').includes(email))
		assert.ok(!r.lines.join('\n').includes(email))
	})

	for (const absent of [undefined, '', '  \n']) {
		it(`puts a blank value when SUPER_ADMIN is ${JSON.stringify(absent)}`, () => {
			const r = run({ SUPER_ADMIN: absent })
			assert.equal(r.go(), 0)
			assert.deepEqual(r.inputs, [' '])
		})
	}

	it('throws a generic message on a failed put, without the value', () => {
		const r = run({ SUPER_ADMIN: email }, { status: 254, stderr: email })
		assert.throws(r.go, (e: Error) => !e.message.includes(email) && /failed/.test(e.message))
		assert.ok(!r.lines.join('\n').includes(email))
	})

	it('throws a generic message when exec itself throws', () => {
		const r = run({ SUPER_ADMIN: email }, () => {
			throw new Error(`aws ... ${email}`)
		})
		assert.throws(r.go, (e: Error) => !e.message.includes(email))
	})
})
