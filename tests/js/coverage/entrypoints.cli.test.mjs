import { describe, it, beforeEach, afterEach } from 'node:test'
import assert from 'node:assert/strict'
import { existsSync, mkdirSync, mkdtempSync, readFileSync, rmSync, writeFileSync } from 'node:fs'
import { tmpdir } from 'node:os'
import { join } from 'node:path'
import { spawnSync } from 'node:child_process'
import { fileURLToPath } from 'node:url'

import { main as writeComment } from '../../../tools/coverage/write-coverage-comment.mjs'

const script = (name) => fileURLToPath(new URL(`../../../tools/coverage/${name}.mjs`, import.meta.url))

// Each script run as the command, with its defaults, in an empty working
// directory: what the workflow steps do.
describe('the coverage scripts run as commands with their defaults', () => {
	let dir
	let output
	beforeEach(() => {
		dir = mkdtempSync(join(tmpdir(), 'coverage-entry-'))
		output = join(dir, 'github-output')
		writeFileSync(output, '')
	})
	afterEach(() => rmSync(dir, { recursive: true, force: true }))

	const run = (name, args = [], env = {}) =>
		spawnSync(process.execPath, [script(name), ...args], {
			cwd: dir,
			encoding: 'utf8',
			env: { PATH: process.env.PATH, GITHUB_OUTPUT: output, ...env },
		})

	it('write-coverage-comment writes the marker and the gate output, and returns the gate status', () => {
		mkdirSync(join(dir, 'tools/coverage'), { recursive: true })
		writeFileSync(join(dir, 'tools/coverage/check-coverage.mjs'), "console.log('gate ran'); process.exit(3)\n")
		const result = run('write-coverage-comment')
		assert.equal(result.status, 3)
		assert.match(result.stdout, /^<!-- coverage-gate -->\ngate ran/)
		assert.match(readFileSync(join(dir, 'comment.md'), 'utf8'), /gate ran/)
	})

	it('write-coverage-comment treats a gate with no exit status as a failure', () => {
		const status = writeComment({ comment: join(dir, 'c.md'), write: () => {}, spawn: () => ({ status: null }) })
		assert.equal(status, 1)
	})

	it('report-coverage-detail does nothing without a summary', () => {
		const result = run('report-coverage-detail', [], { GITHUB_STEP_SUMMARY: join(dir, 'summary') })
		assert.equal(result.status, 0)
		assert.equal(existsSync(join(dir, 'summary')), false)
	})

	it('read-local-baseline reports no baseline when nothing was fetched', () => {
		const result = run('read-local-baseline')
		assert.equal(result.status, 0)
		assert.match(result.stdout, /ci-local:github-token=empty/)
		assert.match(readFileSync(output, 'utf8'), /path=none\nrun-id=none/)
	})

	it('read-local-baseline reads a baseline whose run id was not written', () => {
		mkdirSync(join(dir, '.ci-local/baseline'), { recursive: true })
		writeFileSync(join(dir, '.ci-local/baseline/Cobertura.xml'), '<coverage />')
		const result = run('read-local-baseline', [], { JOB_TOKEN: 'x' })
		assert.equal(result.status, 0)
		assert.match(result.stdout, /github-token=present/)
		assert.match(readFileSync(output, 'utf8'), /path=\.\/\.ci-local\/baseline\/Cobertura\.xml\nrun-id=\n/)
	})

	it('append-test-counts appends the counts to comment.md', () => {
		mkdirSync(join(dir, 'tools/coverage'), { recursive: true })
		writeFileSync(join(dir, 'tools/coverage/report-test-counts.mjs'), "console.log('C# counts')\n")
		writeFileSync(join(dir, 'comment.md'), 'gate\n')
		const result = run('append-test-counts')
		assert.equal(result.status, 0)
		assert.match(readFileSync(join(dir, 'comment.md'), 'utf8'), /gate\n\n## Test counts\n\nC# counts\n\n_Playwright suite not run in this job\._/)
	})

	it('fetch-coverage-baseline fails without a repository', () => {
		const result = run('fetch-coverage-baseline')
		assert.notEqual(result.status, 0)
		assert.match(result.stderr, /GITHUB_REPOSITORY is not set/)
	})

	it('hand-over-raw-reports names its usage and reads the default share when given no direction', () => {
		const result = run('hand-over-raw-reports')
		assert.equal(result.status, 2)
		assert.match(result.stdout, /Usage: hand-over-raw-reports\.mjs push\|pull/)
	})

	it('run-js-tests skips when the working directory has no suite', () => {
		const result = run('run-js-tests')
		assert.equal(result.status, 0)
		assert.match(result.stdout, /No JavaScript test suite yet/)
	})

	it('print-ci-local-coverage prints the markers with every input missing', () => {
		const result = run('print-ci-local-coverage')
		assert.equal(result.status, 0)
		assert.match(result.stdout, /^ci-local:coverage:begin\n/)
		assert.match(result.stdout, /ci-local:reports=0\nci-local:coverage:end\n$/)
	})

	it('print-ci-local-coverage prints the files it finds', () => {
		mkdirSync(join(dir, 'artifacts/report'), { recursive: true })
		writeFileSync(join(dir, 'comment.md'), 'the comment\n')
		writeFileSync(join(dir, 'artifacts/report/SummaryGithub.md'), 'the summary\n')
		writeFileSync(join(dir, 'artifacts/report/Cobertura.xml'), '<coverage line-rate="1">\n')
		const result = run('print-ci-local-coverage')
		assert.match(result.stdout, /the comment\n\nthe summary\n\nCobertura totals:\n<coverage line-rate="1">\n/)
	})
})
