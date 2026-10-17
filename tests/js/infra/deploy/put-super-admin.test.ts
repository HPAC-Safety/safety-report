import { describe, it } from 'node:test'
import assert from 'node:assert/strict'
import { existsSync, mkdtempSync, readFileSync, statSync } from 'node:fs'
import { tmpdir } from 'node:os'
import path from 'node:path'

import { entry, main, redact } from '../../../../tools/infra/deploy/put-super-admin.ts'
import type { Exec, ExecResult } from '../../../../tools/lib/actions.ts'

const entries = JSON.stringify({ super_admin: 'arn:super' })
const email = 'boss@example.test'

type AwsReply = Partial<ExecResult> | (() => Partial<ExecResult>)

function run(env: Record<string, string | undefined>, awsReply: AwsReply = {}) {
	const runnerTemp = mkdtempSync(path.join(tmpdir(), 'runner-temp-'))
	const calls: string[] = []
	const files: { path: string; value: string; mode: number }[] = []
	const exec: Exec = (command, args = []) => {
		calls.push([command, ...args].join(' '))
		if (command === 'terraform') return { status: 0, stdout: entries, stderr: '' }
		const source = args[args.indexOf('--secret-string') + 1] ?? ''
		const file = source.replace(/^file:\/\//, '')
		files.push({ path: file, value: readFileSync(file, 'utf8'), mode: statSync(file).mode & 0o777 })
		return { status: 0, stdout: '', stderr: '', ...(typeof awsReply === 'function' ? awsReply() : awsReply) }
	}
	const lines: string[] = []
	return { go: () => main({ env: { RUNNER_TEMP: runnerTemp, ...env }, exec, log: (l) => lines.push(l) }), calls, files, lines, runnerTemp }
}

describe('entry', () => {
	it('reads a key, or null text when missing', () => {
		assert.equal(entry('{"a":"x"}', 'a'), 'x')
		assert.equal(entry('{}', 'a'), 'null')
	})
})

describe('redact', () => {
	it('cuts every occurrence of the value, any case, and treats regex characters literally', () => {
		assert.equal(redact('a+b@x.test and A+B@X.TEST', 'a+b@x.test'), '<redacted> and <redacted>')
		assert.equal(redact('aXb', 'a.b'), 'aXb')
	})

	it('leaves the text alone for a blank value', () => {
		assert.equal(redact('anything', ''), 'anything')
	})
})

describe('main', () => {
	it('puts a set value from a private file under RUNNER_TEMP, never in argv or output', () => {
		const r = run({ SUPER_ADMIN: `  ${email}\n` })
		assert.equal(r.go(), 0)
		assert.equal(r.files.length, 1)
		assert.equal(r.files[0]?.value, email)
		assert.equal(r.files[0]?.mode, 0o600)
		assert.ok(r.files[0]?.path.startsWith(r.runnerTemp))
		assert.ok(r.calls.some((c) => c.startsWith('aws secretsmanager put-secret-value --secret-id arn:super --secret-string file://')))
		assert.ok(!r.calls.join('\n').includes(email))
		assert.ok(!r.lines.join('\n').includes(email))
	})

	it('removes the temporary file afterwards', () => {
		const r = run({ SUPER_ADMIN: email })
		r.go()
		assert.ok(!existsSync(r.files[0]?.path ?? ''))
	})

	for (const absent of [undefined, '', '  \n']) {
		it(`puts a blank value when SUPER_ADMIN is ${JSON.stringify(absent)}`, () => {
			const r = run({ SUPER_ADMIN: absent })
			assert.equal(r.go(), 0)
			assert.equal(r.files[0]?.value, ' ')
		})
	}

	it('throws a generic message on a failed put, logs the CLI reason without the value, and still removes the file', () => {
		const r = run({ SUPER_ADMIN: email }, { status: 252, stderr: `Error parsing parameter for ${email.toUpperCase()}` })
		assert.throws(r.go, (e: Error) => !e.message.includes(email) && /failed/.test(e.message))
		assert.ok(!r.lines.join('\n').toLowerCase().includes(email))
		assert.ok(r.lines.join('\n').includes('Error parsing parameter for <redacted>'))
		assert.ok(!existsSync(r.files[0]?.path ?? ''))
	})

	it('throws a generic message when exec itself throws, and still removes the file', () => {
		let seen = ''
		const r = run({ SUPER_ADMIN: email }, () => {
			throw new Error(`aws ... ${email}`)
		})
		assert.throws(r.go, (e: Error) => {
			seen = e.message
			return !e.message.includes(email)
		})
		assert.ok(/failed/.test(seen))
		assert.ok(!existsSync(r.files[0]?.path ?? ''))
	})
})
