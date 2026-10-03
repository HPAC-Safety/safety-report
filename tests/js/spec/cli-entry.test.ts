import { describe, it } from 'node:test'
import assert from 'node:assert/strict'
import { spawnSync } from 'node:child_process'
import path from 'node:path'

import type { Env, Exec } from '../../../tools/lib/actions.ts'
import { main as checkFeatureCoverageDiff } from '../../../tools/spec/check-feature-coverage-diff.ts'
import { main as regenerateSpecDocs } from '../../../tools/spec/regenerate-spec-docs.ts'

const tools = path.resolve(import.meta.dirname, '../../../tools/spec')

const spawn = (script: string, args: string[] = [], env: Env = {}) =>
	spawnSync(process.execPath, [path.join(tools, script), ...args], {
		env: { PATH: process.env.PATH, NODE_V8_COVERAGE: process.env.NODE_V8_COVERAGE, ...env },
		encoding: 'utf8',
	})

describe('spec scripts run as commands', () => {
	it('check-feature-coverage-diff refuses without BASE_SHA', () => {
		const result = spawn('check-feature-coverage-diff.ts')
		assert.notEqual(result.status, 0)
		assert.match(result.stderr, /BASE_SHA is not set/)
	})

	it('check-generated-file demands its three options', () => {
		const result = spawn('check-generated-file.ts')
		assert.equal(result.status, 2)
		assert.match(result.stdout, /--generator, --file, and --message are required/)
	})

	it('regenerate-spec-docs prints usage without both directories', () => {
		const result = spawn('regenerate-spec-docs.ts')
		assert.equal(result.status, 1)
		assert.match(result.stdout, /Usage: node tools\/spec\/regenerate-spec-docs.ts/)
		const out: string[] = []
		assert.equal(regenerateSpecDocs({ argv: [], log: (line) => out.push(line) }), 1)
		assert.equal(regenerateSpecDocs({ argv: ['base'], log: (line) => out.push(line) }), 1)
		assert.equal(out.length, 2)
	})
})

describe('check-feature-coverage-diff defaults', () => {
	it('reads the committed matrix when none is given', () => {
		const exec: Exec = () => ({ status: 0, stdout: '', stderr: '' })
		assert.equal(typeof checkFeatureCoverageDiff({ env: { BASE_SHA: 'abc' }, exec }), 'number')
	})

	it('names an unknown pull request when a queued subject carries no number', () => {
		const errors: string[] = []
		const original = console.error
		console.error = (line: string) => errors.push(line)
		try {
			const exec: Exec = (_command, args = []) => {
				if (args[0] === 'rev-list') return { status: 0, stdout: 'c1', stderr: '' }
				if (args[0] === 'log' && args[2] === '--format=%s') return { status: 0, stdout: 'No number here', stderr: '' }
				if (args[0] === 'log') return { status: 0, stdout: 'message', stderr: '' }
				return { status: 0, stdout: args.includes(':(glob)src/**') ? 'src/a.ts' : '', stderr: '' }
			}
			const code = checkFeatureCoverageDiff({ env: { BASE_SHA: 'b', EVENT_NAME: 'merge_group' }, exec, log: () => {}, matrix: '' })
			assert.equal(code, 1)
			assert.ok(errors.some((line) => line.includes('pull request #unknown')))
		} finally {
			console.error = original
		}
	})
})
