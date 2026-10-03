import { describe, it, beforeEach, afterEach } from 'node:test'
import assert from 'node:assert/strict'
import { existsSync, mkdirSync, mkdtempSync, readFileSync, rmSync, writeFileSync } from 'node:fs'
import { tmpdir } from 'node:os'
import { join } from 'node:path'

import { main } from '../../../tools/coverage/hand-over-raw-reports.ts'

describe('hand-over-raw-reports', () => {
	let dir: string
	let cwd: string
	let logs: string[]

	beforeEach(() => {
		dir = mkdtempSync(join(tmpdir(), 'hand-over-'))
		cwd = process.cwd()
		process.chdir(dir)
		logs = []
	})

	afterEach(() => {
		process.chdir(cwd)
		rmSync(dir, { recursive: true, force: true })
	})

	const run = (direction: string, share: string, ...name: string[]) => main({ argv: [direction, ...name], env: { CI_LOCAL_SHARE: share }, log: (m) => logs.push(m) })

	it('push refuses when the share is not mounted', () => {
		assert.equal(run('push', join(dir, 'missing')), 1)
		assert.match(logs[0], /^::error::.*missing is not mounted; run this workflow through tools\/dev\/ci-local\.sh$/)
	})

	it('push copies the reports, nested folders included, into the share', () => {
		const share = join(dir, 'share')
		mkdirSync(share)
		mkdirSync('artifacts/coverage/guid', { recursive: true })
		writeFileSync('artifacts/coverage/guid/coverage.cobertura.xml', '<x/>')
		assert.equal(run('push', share), 0)
		assert.equal(readFileSync(join(share, 'coverage/guid/coverage.cobertura.xml'), 'utf8'), '<x/>')
	})

	it('pull refuses when the share holds no reports', () => {
		const share = join(dir, 'share')
		mkdirSync(share)
		assert.equal(run('pull', share), 1)
		assert.match(logs[0], /no raw reports in .*share\/coverage; run this workflow through tools\/dev\/ci-local\.sh/)
		assert.equal(existsSync('artifacts'), false)
	})

	it('pull copies the shared reports into ./artifacts/coverage', () => {
		const share = join(dir, 'share')
		mkdirSync(join(share, 'coverage/js'), { recursive: true })
		writeFileSync(join(share, 'coverage/js/lcov.info'), 'TN:')
		assert.equal(run('pull', share), 0)
		assert.equal(readFileSync('artifacts/coverage/js/lcov.info', 'utf8'), 'TN:')
	})

	it('hands over the claim results by name, merging two jobs\' files', () => {
		const share = join(dir, 'share')
		mkdirSync(share)
		mkdirSync('artifacts/claims', { recursive: true })
		writeFileSync('artifacts/claims/reqnroll.ndjson', 'r')
		assert.equal(run('push', share, 'claims'), 0)
		rmSync('artifacts/claims/reqnroll.ndjson')
		writeFileSync('artifacts/claims/playwright-bdd.ndjson', 'p')
		assert.equal(run('push', share, 'claims'), 0)
		rmSync('artifacts', { recursive: true })
		assert.equal(run('pull', share, 'claims'), 0)
		assert.equal(readFileSync('artifacts/claims/reqnroll.ndjson', 'utf8'), 'r')
		assert.equal(readFileSync('artifacts/claims/playwright-bdd.ndjson', 'utf8'), 'p')
	})

	it('rejects a name it does not hand over', () => {
		assert.equal(run('push', dir, 'secrets'), 2)
		assert.match(logs[0], /push\|pull \[coverage\|claims\]/)
	})

	it('rejects an unknown direction', () => {
		assert.equal(run('sideways', dir), 2)
	})
})
