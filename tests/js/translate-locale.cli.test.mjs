import { describe, it } from 'node:test'
import assert from 'node:assert/strict'
import { execFileSync } from 'node:child_process'
import { mkdtempSync, mkdirSync, readFileSync, writeFileSync, existsSync } from 'node:fs'
import { tmpdir } from 'node:os'
import { join } from 'node:path'

const TOOL = new URL('../../tools/translate-locale.mjs', import.meta.url).pathname

let counter = 0

/** A throwaway `locales/` directory holding whichever files a case needs. */
function locales(files) {
	const dir = join(mkdtempSync(join(tmpdir(), 'translate-locale-')), `case-${counter++}`)
	mkdirSync(dir, { recursive: true })
	for (const [name, content] of Object.entries(files)) {
		writeFileSync(join(dir, name), `${JSON.stringify(content, null, 2)}\n`)
	}
	return dir
}

const read = (dir, name) => JSON.parse(readFileSync(join(dir, name), 'utf8'))

/** Runs the tool the way a workflow step runs it, capturing what CI would see. */
function run(args, env = {}) {
	const outputs = join(mkdtempSync(join(tmpdir(), 'gh-output-')), 'output.txt')
	writeFileSync(outputs, '')
	try {
		const stdout = execFileSync(process.execPath, [TOOL, ...args], {
			encoding: 'utf8',
			env: {
				PATH: process.env.PATH,
				HOME: process.env.HOME,
				GITHUB_OUTPUT: outputs,
				...env,
			},
		})
		return { code: 0, output: stdout, outputs: readFileSync(outputs, 'utf8') }
	} catch (error) {
		return {
			code: error.status,
			output: `${error.stdout ?? ''}${error.stderr ?? ''}`,
			outputs: readFileSync(outputs, 'utf8'),
		}
	}
}

const stub = { TRANSLATION_PROVIDER: 'stub' }

const english = { form: { submit: 'Submit', cancel: 'Cancel' } }

describe('the locale translation command', () => {
	describe('given a repository with no locale files yet', () => {
		it('when it verifies then it skips with a notice rather than failing the build', () => {
			// Given
			const dir = locales({})

			// When
			const { code, output } = run(['--locales', dir, '--check'])

			// Then
			assert.equal(code, 0)
			assert.match(output, /::notice::/)
		})
	})

	describe('given English exists while the initial French generation is pending', () => {
		it('when it verifies then the tracked bootstrap marker defers parity without hiding a later deletion', () => {
			// Given
			const dir = locales({ 'en-CA.json': english, '.fr-CA.pending': 'initial generation pending' })

			// When
			const { code, output } = run(['--locales', dir, '--check'])

			// Then
			assert.equal(code, 0)
			assert.match(output, /::notice::/)
			assert.match(output, /initial French generation/i)
		})
	})

	describe('given English exists and the generated French pair was removed after initialization', () => {
		it('when it verifies then it fails instead of treating the deletion as bootstrap', () => {
			// Given
			const dir = locales({ 'en-CA.json': english })

			// When
			const { code, output } = run(['--locales', dir, '--check'])

			// Then
			assert.equal(code, 1)
			assert.match(output, /form\.submit/)
		})
	})

	describe('given a new English key and a configured provider', () => {
		it('when it generates then only that key is translated and provenance is stamped', () => {
			// Given — a settled set, then one key added
			const dir = locales({ 'en-CA.json': english, '.fr-CA.pending': 'initial generation pending' })
			assert.equal(run(['--locales', dir, '--generate'], stub).code, 0)
			assert.equal(existsSync(join(dir, '.fr-CA.pending')), false)
			const frenchBefore = read(dir, 'fr-CA.json')
			writeFileSync(
				join(dir, 'en-CA.json'),
				`${JSON.stringify({ form: { ...english.form, reset: 'Start over' } }, null, 2)}\n`,
			)

			// When
			const { code, output, outputs } = run(['--locales', dir, '--generate'], stub)

			// Then
			assert.equal(code, 0)
			assert.match(outputs, /changed=true/)
			assert.match(output, /form\.reset/)
			assert.doesNotMatch(output.split('Translating')[1] ?? output, /form\.cancel/)

			const frenchAfter = read(dir, 'fr-CA.json')
			assert.equal(frenchAfter.form.submit, frenchBefore.form.submit)
			assert.equal(frenchAfter.form.cancel, frenchBefore.form.cancel)
			assert.ok(frenchAfter.form.reset)

			const meta = read(dir, 'fr-CA.meta.json')
			assert.equal(meta['form.reset'].reviewed, false)
			assert.equal(meta['form.reset'].provider, 'stub')
			assert.ok(meta['form.reset'].source_hash)
		})
	})

	describe('given nothing has changed in English', () => {
		it('when it generates again then it exits 0, writes nothing, and reports no change', () => {
			// Given
			const dir = locales({ 'en-CA.json': english })
			assert.equal(run(['--locales', dir, '--generate'], stub).code, 0)
			const frenchBefore = readFileSync(join(dir, 'fr-CA.json'), 'utf8')
			const metaBefore = readFileSync(join(dir, 'fr-CA.meta.json'), 'utf8')

			// When
			const { code, output, outputs } = run(['--locales', dir, '--generate'], stub)

			// Then
			assert.equal(code, 0)
			assert.match(outputs, /changed=false/)
			assert.match(output, /Nothing to translate/)
			assert.equal(readFileSync(join(dir, 'fr-CA.json'), 'utf8'), frenchBefore)
			assert.equal(readFileSync(join(dir, 'fr-CA.meta.json'), 'utf8'), metaBefore)
		})
	})

	describe('given a glossary-pinned key', () => {
		it('when it generates then HPAC official French lands and the machine never sees the key', () => {
			// Given
			const dir = locales({
				'en-CA.json': { form: { severity: 'Serious injury (secondary medical aid)' } },
				'glossary.json': { 'form.severity': 'Blessure grave (aide médicale secondaire)' },
			})

			// When
			const { code, output } = run(['--locales', dir, '--generate'], stub)

			// Then
			assert.equal(code, 0)
			assert.equal(read(dir, 'fr-CA.json').form.severity, 'Blessure grave (aide médicale secondaire)')
			assert.equal(read(dir, 'fr-CA.meta.json')['form.severity'].provider, 'glossary')
			assert.match(output, /pinned/i)
		})
	})

	describe('given a glossary-pinned key and an edit to its English wording', () => {
		it('when it generates then the pinned French is left exactly as HPAC wrote it', () => {
			// Given
			const dir = locales({
				'en-CA.json': { form: { severity: 'Serious injury' } },
				'glossary.json': { 'form.severity': 'Blessure grave (aide médicale secondaire)' },
			})
			assert.equal(run(['--locales', dir, '--generate'], stub).code, 0)
			writeFileSync(
				join(dir, 'en-CA.json'),
				`${JSON.stringify({ form: { severity: 'Serious injury (secondary medical aid)' } }, null, 2)}\n`,
			)

			// When
			const { code } = run(['--locales', dir, '--generate'], stub)

			// Then
			assert.equal(code, 0)
			assert.equal(read(dir, 'fr-CA.json').form.severity, 'Blessure grave (aide médicale secondaire)')
		})
	})

	describe('given work to do but no provider configured', () => {
		it('when it generates then it warns and writes nothing, rather than guessing a provider', () => {
			// Given
			const dir = locales({ 'en-CA.json': english })

			// When
			const { code, output, outputs } = run(['--locales', dir, '--generate'])

			// Then
			assert.equal(code, 0)
			assert.match(output, /::warning::/)
			assert.match(outputs, /changed=false/)
			assert.equal(existsSync(join(dir, 'fr-CA.json')), false)
		})
	})

	describe('given English edited without regenerating French', () => {
		it('when it verifies then it fails and names the drifted key', () => {
			// Given
			const dir = locales({ 'en-CA.json': english })
			assert.equal(run(['--locales', dir, '--generate'], stub).code, 0)
			writeFileSync(
				join(dir, 'en-CA.json'),
				`${JSON.stringify({ form: { submit: 'Submit report', cancel: 'Cancel' } }, null, 2)}\n`,
			)

			// When — the endpoint is deliberately unroutable: --check must never reach it
			const { code, output } = run(['--locales', dir, '--check'], {
				TRANSLATION_PROVIDER: 'chat-completions',
				TRANSLATION_ENDPOINT: 'https://translation.invalid/chat/completions',
				TRANSLATION_MODEL: 'vendor/a-model',
				TRANSLATION_API_KEY: 'not-a-real-key',
			})

			// Then
			assert.equal(code, 1)
			assert.match(output, /form\.submit/)
			assert.doesNotMatch(output, /ENOTFOUND|fetch failed/)
		})
	})

	describe('given a generated set that is fully in step', () => {
		it('when it verifies then it passes', () => {
			// Given — a set as a real provider would have left it. Stub provenance is
			// rejected on purpose, so this fixture names a provider instead.
			const dir = locales({ 'en-CA.json': english })
			assert.equal(run(['--locales', dir, '--generate'], stub).code, 0)
			const meta = read(dir, 'fr-CA.meta.json')
			for (const entry of Object.values(meta)) entry.provider = 'test-provider'
			writeFileSync(join(dir, 'fr-CA.meta.json'), `${JSON.stringify(meta, null, 2)}\n`)

			// When
			const { code, output } = run(['--locales', dir, '--check'])

			// Then
			assert.equal(code, 0)
			assert.match(output, /in step/i)
		})
	})

	describe('given French carrying offline stub provenance', () => {
		it('when it verifies then it fails, so stub output can never reach main', () => {
			// Given
			const dir = locales({ 'en-CA.json': english })
			assert.equal(run(['--locales', dir, '--generate'], stub).code, 0)

			// When
			const { code, output } = run(['--locales', dir, '--check'])

			// Then
			assert.equal(code, 1)
			assert.match(output, /stub/)
		})
	})

	describe('given neither --check nor --generate', () => {
		it('when it runs then it refuses rather than defaulting to the side-effecting mode', () => {
			// Given / When
			const { code, output } = run(['--locales', locales({ 'en-CA.json': english })])

			// Then
			assert.equal(code, 2)
			assert.match(output, /--check|--generate/)
		})
	})
})
