import { describe, it } from 'node:test'
import assert from 'node:assert/strict'
import { mkdirSync, mkdtempSync, readFileSync, rmSync, writeFileSync } from 'node:fs'
import { tmpdir } from 'node:os'
import { join } from 'node:path'
import { spawnSync } from 'node:child_process'
import { fileURLToPath } from 'node:url'

const SCRIPT = fileURLToPath(new URL('../../../tools/coverage/report-test-counts.ts', import.meta.url))

describe('report-test-counts --section e2e', () => {
	const fixture = () => {
		const dir = mkdtempSync(join(tmpdir(), 'test-counts-cli-'))
		mkdirSync(join(dir, 'gen/home'), { recursive: true })
		writeFileSync(join(dir, 'gen/home/home.spec.js'), "test('a', () => {})\ntest('b', () => {})\n")
		return dir
	}
	const run = (dir: string, ...extra: string[]) =>
		spawnSync('node', [SCRIPT, '--section', 'e2e', '--e2e-gen', join(dir, 'gen'), '--smoke-spec', join(dir, 'none.ts'), ...extra], { encoding: 'utf8' })

	it('prints the table by default', () => {
		const dir = fixture()
		const result = run(dir)
		rmSync(dir, { recursive: true })
		assert.equal(result.status, 0)
		assert.match(result.stdout, /Playwright \(E2E\) \(2 tests\)/)
		assert.match(result.stdout, /\| home\.feature \| 2 \|/)
	})

	it('writes the same text to --out, creating its directory', () => {
		const dir = fixture()
		const printed = run(dir).stdout
		const out = join(dir, 'nested/dir/e2e.md')
		const result = run(dir, '--out', out)
		const written = readFileSync(out, 'utf8')
		rmSync(dir, { recursive: true })
		assert.equal(result.stdout, '')
		assert.equal(written, printed)
	})
})

describe('report-test-counts --section csharp', () => {
	const unitTest = (storage: string, className: string): string => `<UnitTest name="t"><TestMethod className="${className}" /></UnitTest>`.replace('<UnitTest ', `<UnitTest storage="${storage}" `)
	const trx = (...tests: string[]): string => `<TestRun><TestDefinitions>${tests.join('')}</TestDefinitions></TestRun>`

	const fixture = () => {
		const dir = mkdtempSync(join(tmpdir(), 'test-counts-csharp-'))
		mkdirSync(join(dir, 'tests/HpacSafety.Api.Tests'), { recursive: true })
		writeFileSync(join(dir, 'tests/not-a-directory.txt'), '')
		mkdirSync(join(dir, 'trx/nested'), { recursive: true })
		writeFileSync(join(dir, 'trx/a.trx'), trx(
			unitTest('/x/hpacsafety.api.tests.dll', 'HpacSafety.Api.Tests.FooTests'),
			unitTest('/x/Other.Tests.dll', 'Other.Tests.BarTests'),
			unitTest('/x/Other.Tests.dll', 'Other.Tests.Features.Some_Feature.SomeFeatureFeature'),
			'<UnitTest name="no-attributes"></UnitTest>',
		))
		writeFileSync(join(dir, 'trx/nested/b.trx'), '<TestRun></TestRun>')
		writeFileSync(join(dir, 'trx/readme.txt'), 'not a trx')
		return dir
	}

	const run = (dir: string, ...extra: string[]) => spawnSync('node', [SCRIPT, ...extra], { encoding: 'utf8', cwd: dir })

	it('counts xUnit tests per assembly and Reqnroll scenarios per feature, under both sections', () => {
		const dir = fixture()
		const result = run(dir, '--section', 'both', '--trx-dir', 'trx', '--tests-dir', 'tests', '--e2e-gen', 'missing', '--smoke-spec', 'missing.ts')
		rmSync(dir, { recursive: true })
		assert.equal(result.status, 0)
		assert.match(result.stdout, /^## Test counts/)
		assert.match(result.stdout, /xUnit \(2 tests\)/)
		assert.match(result.stdout, /\| HpacSafety\.Api\.Tests \| 1 \|/)
		assert.match(result.stdout, /\| Other\.Tests \| 1 \|/)
		assert.match(result.stdout, /Reqnroll \(1 tests\)/)
		assert.match(result.stdout, /\| Some Feature \| 1 \|/)
		assert.match(result.stdout, /Playwright suite not run/)
	})

	it('says so when the trx directory is missing', () => {
		const dir = mkdtempSync(join(tmpdir(), 'test-counts-csharp-'))
		const result = run(dir, '--section', 'csharp', '--trx-dir', 'nowhere', '--tests-dir', 'nowhere')
		rmSync(dir, { recursive: true })
		assert.match(result.stdout, /No \.trx results found in this run/)
		assert.match(result.stdout, /No Reqnroll scenario tests found in this run/)
	})

	it('uses the default directories when none are given', () => {
		const dir = mkdtempSync(join(tmpdir(), 'test-counts-csharp-'))
		const result = run(dir, '--section', 'csharp')
		rmSync(dir, { recursive: true })
		assert.equal(result.status, 0)
		assert.match(result.stdout, /No \.trx results found in this run/)
	})
})

describe('report-test-counts --section e2e, the shapes of the generated directory', () => {
	it('skips stray files, counts only spec files, and lists the smoke spec', () => {
		const dir = mkdtempSync(join(tmpdir(), 'test-counts-e2e-'))
		mkdirSync(join(dir, 'gen/home'), { recursive: true })
		writeFileSync(join(dir, 'gen/stray.txt'), '')
		writeFileSync(join(dir, 'gen/home/notes.txt'), 'test(')
		writeFileSync(join(dir, 'gen/home/empty.spec.js'), '// no tests here\n')
		writeFileSync(join(dir, 'smoke.spec.ts'), "test('smoke', () => {})\n")
		const result = spawnSync('node', [SCRIPT, '--section', 'e2e', '--e2e-gen', 'gen', '--smoke-spec', 'smoke.spec.ts'], { encoding: 'utf8', cwd: dir })
		rmSync(dir, { recursive: true })
		assert.match(result.stdout, /Playwright \(E2E\) \(1 tests\)/)
		assert.match(result.stdout, /\| home\.feature \| 0 \|/)
		assert.match(result.stdout, /\| smoke\.spec\.ts \| 1 \|/)
	})

	it('says the suite was not run when nothing is there', () => {
		const dir = mkdtempSync(join(tmpdir(), 'test-counts-e2e-'))
		const result = spawnSync('node', [SCRIPT, '--section', 'e2e'], { encoding: 'utf8', cwd: dir })
		rmSync(dir, { recursive: true })
		assert.match(result.stdout, /Playwright suite not run/)
	})
})

describe('report-test-counts with no options', () => {
	it('prints both sections, tolerating a trx with no tests and no tests directory', () => {
		const dir = mkdtempSync(join(tmpdir(), 'test-counts-defaults-'))
		mkdirSync(join(dir, 'artifacts/coverage'), { recursive: true })
		writeFileSync(join(dir, 'artifacts/coverage/empty.trx'), '<TestRun><TestDefinitions></TestDefinitions></TestRun>')
		const result = spawnSync('node', [SCRIPT], { encoding: 'utf8', cwd: dir })
		rmSync(dir, { recursive: true })
		assert.equal(result.status, 0)
		assert.match(result.stdout, /^## Test counts/)
		assert.match(result.stdout, /No \.trx results found in this run/)
		assert.match(result.stdout, /Playwright suite not run/)
	})
})
