import { describe, it } from 'node:test'
import assert from 'node:assert/strict'
import { mkdtempSync, readFileSync } from 'node:fs'
import { spawnSync } from 'node:child_process'
import { tmpdir } from 'node:os'
import path from 'node:path'

import { main as openTranslationPr } from '../../../tools/i18n/open-translation-pr.mjs'
import { main as reportTranslationRun } from '../../../tools/i18n/report-translation-run.mjs'

const tools = path.resolve(import.meta.dirname, '../../../tools/i18n')

const spawn = (script, args = []) =>
	spawnSync(process.execPath, [path.join(tools, script), ...args], {
		env: { PATH: process.env.PATH, NODE_V8_COVERAGE: process.env.NODE_V8_COVERAGE },
		encoding: 'utf8',
	})

const ok = (stdout = '', stderr = '') => ({ status: 0, stdout, stderr })

describe('translation scripts run as commands', () => {
	it('open-translation-pr refuses without GH_TOKEN', () => {
		const result = spawn('open-translation-pr.mjs')
		assert.notEqual(result.status, 0)
		assert.match(result.stderr, /GH_TOKEN is not set/)
	})

	it('report-translation-run prints usage without a mode', () => {
		const result = spawn('report-translation-run.mjs')
		assert.equal(result.status, 1)
		assert.match(result.stdout, /Usage: node tools\/i18n\/report-translation-run.mjs/)
	})
})

describe('open-translation-pr defaults', () => {
	const env = { GH_TOKEN: 't', TRANSLATION_BRANCH: 'br', GITHUB_REPOSITORY: 'o/r' }

	it('creates the pull request with no summary, a temp directory, and printed gh output', () => {
		const out = []
		const exec = (command, args) => (command === 'gh' && args[1] === 'view' ? { status: 1, stdout: '', stderr: '' } : ok(command === 'gh' ? 'https://example/pr/1' : ''))
		assert.equal(openTranslationPr({ env, exec, log: (line) => out.push(line), exists: () => true }), 0)
		assert.deepEqual(out, ['https://example/pr/1'])
	})

	it('creates the pull request quietly when gh prints nothing', () => {
		const out = []
		const exec = (command, args) => (command === 'gh' && args[1] === 'view' ? { status: 1, stdout: '', stderr: '' } : ok())
		assert.equal(openTranslationPr({ env, exec, log: (line) => out.push(line), exists: () => false }), 0)
		assert.deepEqual(out, [])
	})

	it('updates an open pull request, printing gh output, and writes the body into RUNNER_TEMP', () => {
		const dir = mkdtempSync(path.join(tmpdir(), 'otp-'))
		const out = []
		const exec = (command, args) => ok(args[1] === 'edit' ? 'edited' : '')
		const code = openTranslationPr({ env: { ...env, SUMMARY: '3 keys', RUNNER_TEMP: dir }, exec, log: (line) => out.push(line), exists: () => false })
		assert.equal(code, 0)
		assert.deepEqual(out, ['edited', 'Updated the open translation pull request.'])
		assert.match(readFileSync(path.join(dir, 'pr-body.md'), 'utf8'), /^3 keys\./)
	})

	it('reports a failing command with its stderr, or bare', () => {
		const out = []
		const failing = (stderr) => () => ({ status: 1, stdout: '', stderr })
		assert.equal(openTranslationPr({ env, exec: failing('boom'), log: (line) => out.push(line), exists: () => false }), 1)
		assert.equal(openTranslationPr({ env, exec: failing(''), log: (line) => out.push(line), exists: () => false }), 1)
		assert.deepEqual(out, ['::error::git config failed: boom', '::error::git config failed'])
	})
})

describe('report-translation-run defaults', () => {
	const calls = []
	const exec = (command, args) => {
		calls.push(args)
		return ok(args[1] === 'list' ? '7' : '')
	}

	it('reports a success from an environment that sets nothing else', () => {
		calls.length = 0
		assert.equal(reportTranslationRun({ argv: ['success'], env: { GITHUB_REPOSITORY: 'o/r', PR_NUMBER: '5' }, exec, log: () => {} }), 0)
		assert.deepEqual(calls.map((args) => args[1]), ['list', 'comment'])
	})

	it('reports a failure from an environment that sets nothing else', () => {
		calls.length = 0
		assert.equal(reportTranslationRun({ argv: ['failure'], env: { GITHUB_REPOSITORY: 'o/r' }, exec, log: () => {}, exists: () => false }), 0)
		assert.deepEqual(calls.map((args) => args[1]), ['list', 'comment'])
	})

	it('reads the failure log from a temp directory when RUNNER_TEMP is set', () => {
		calls.length = 0
		const dir = mkdtempSync(path.join(tmpdir(), 'rtr-'))
		const env = { GITHUB_REPOSITORY: 'o/r', RUNNER_TEMP: dir, OUTCOMES: 'a=failure b=success', RUN_URL: 'u' }
		assert.equal(reportTranslationRun({ argv: ['failure'], env, exec, log: () => {}, exists: () => false }), 0)
		assert.match(readFileSync(path.join(dir, 'report.md'), 'utf8'), /Failed step\(s\): a\n/)
	})
})
