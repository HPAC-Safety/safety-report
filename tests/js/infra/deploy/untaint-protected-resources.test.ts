import { describe, it } from 'node:test'
import assert from 'node:assert/strict'

import { baseAddress, main, protectedAddresses, taintedAddresses } from '../../../../tools/infra/deploy/untaint-protected-resources.ts'
import { fakeExec } from '../fake-exec.ts'

const hcl = [
	'resource "aws_db_instance" "main" {',
	'  lifecycle {',
	'    prevent_destroy = true',
	'  }',
	'}',
	'resource "aws_instance" "nat" {',
	'  lifecycle { prevent_destroy = false }',
	'}',
	'resource "aws_cloudwatch_log_group" "this" {',
	'  lifecycle {',
	'    prevent_destroy=true',
	'  }',
	'}',
].join('\n')

const state = JSON.stringify({
	values: {
		root_module: {
			resources: [
				{ address: 'aws_db_instance.main', tainted: true },
				{ address: 'aws_instance.nat', tainted: true },
				{ address: 'aws_cloudwatch_log_group.this["api"]', tainted: true },
				{ address: 'aws_s3_bucket.site', tainted: false },
			],
		},
	},
})

describe('protectedAddresses', () => {
	it('lists the resources with prevent_destroy = true, whatever the spacing', () => {
		assert.deepEqual([...protectedAddresses([hcl])].sort(), ['aws_cloudwatch_log_group.this', 'aws_db_instance.main'])
	})

	it('ignores a prevent_destroy before any resource block', () => {
		assert.deepEqual([...protectedAddresses(['prevent_destroy = true'])], [])
	})
})

describe('taintedAddresses', () => {
	it('returns only tainted root-module resources', () => {
		assert.deepEqual(taintedAddresses(state), ['aws_db_instance.main', 'aws_instance.nat', 'aws_cloudwatch_log_group.this["api"]'])
	})

	it('returns nothing for an empty state', () => {
		assert.deepEqual(taintedAddresses('{}'), [])
	})
})

describe('baseAddress', () => {
	it('drops an instance index', () => {
		assert.equal(baseAddress('a.b["x"]'), 'a.b')
		assert.equal(baseAddress('a.b'), 'a.b')
	})
})

describe('untaint-protected-resources', () => {
	it('untaints only protected tainted resources, indexed ones included', () => {
		const { exec, calls } = fakeExec({ 'terraform -chdir=infra show -json': { stdout: state } })
		const logs: string[] = []
		assert.equal(main({ exec, log: (m) => logs.push(m), readSources: () => [hcl] }), 0)
		assert.deepEqual(calls, [
			'terraform -chdir=infra show -json',
			'terraform -chdir=infra untaint aws_db_instance.main',
			'terraform -chdir=infra untaint aws_cloudwatch_log_group.this["api"]',
		])
		assert.deepEqual(logs, ['Untainting aws_db_instance.main (prevent_destroy).', 'Untainting aws_cloudwatch_log_group.this["api"] (prevent_destroy).'])
	})

	it('does nothing when nothing is tainted', () => {
		const { exec, calls } = fakeExec({ 'terraform -chdir=infra show -json': { stdout: '{"values":{"root_module":{}}}' } })
		main({ exec, log() {}, readSources: () => [hcl] })
		assert.deepEqual(calls, ['terraform -chdir=infra show -json'])
	})

	it('fails when terraform show fails', () => {
		const { exec } = fakeExec({ 'terraform -chdir=infra show -json': { status: 1 } })
		assert.throws(() => main({ exec, log() {}, readSources: () => [hcl] }))
	})
})
