import { describe, it } from 'node:test'
import assert from 'node:assert/strict'
import { mkdtempSync, readFileSync } from 'node:fs'
import { tmpdir } from 'node:os'
import path from 'node:path'

import { annotation, appendSummary, exec, isMain, required, run, setEnv, setOutput } from '../../../tools/lib/actions.ts'

const tempFile = () => path.join(mkdtempSync(path.join(tmpdir(), 'actions-')), 'file')

describe('exec', () => {
	it('returns the status and trimmed output without throwing on failure', () => {
		const result = exec('node', ['-e', 'process.stdout.write(" out \\n"); process.stderr.write("err"); process.exit(3)'])
		assert.deepEqual(result, { status: 3, stdout: 'out', stderr: 'err' })
	})

	it('throws with the stderr when check is set', () => {
		assert.throws(() => exec('node', ['-e', 'process.stderr.write("boom"); process.exit(1)'], { check: true }), /exited 1: boom/)
	})

	it('writes input to stdin and merges env over the process environment', () => {
		const stdout = run('node', ['-e', 'process.stdout.write(require("node:fs").readFileSync(0, "utf8") + process.env.EXTRA)'], { input: 'in-', env: { EXTRA: 'x' } })
		assert.equal(stdout, 'in-x')
	})
})

describe('setOutput', () => {
	it('appends a single-line value as name=value', () => {
		const file = tempFile()
		setOutput('sha', 'abc', { GITHUB_OUTPUT: file })
		assert.equal(readFileSync(file, 'utf8'), 'sha=abc\n')
	})

	it('appends a multi-line value with a delimiter', () => {
		const file = tempFile()
		setOutput('body', 'a\nb', { GITHUB_OUTPUT: file })
		assert.equal(readFileSync(file, 'utf8'), 'body<<__OUTPUT__\na\nb\n__OUTPUT__\n')
	})
})

describe('setEnv and appendSummary', () => {
	it('append to their files', () => {
		const envFile = tempFile()
		const summary = tempFile()
		setEnv('A', '1', { GITHUB_ENV: envFile })
		appendSummary('**done**', { GITHUB_STEP_SUMMARY: summary })
		assert.equal(readFileSync(envFile, 'utf8'), 'A=1\n')
		assert.equal(readFileSync(summary, 'utf8'), '**done**\n')
	})
})

describe('without the Actions files', () => {
	it('prints the output and the summary, and skips the environment line', (t) => {
		const written: unknown[] = []
		t.mock.method(process.stdout, 'write', (text: unknown) => { written.push(text); return true })
		setOutput('sha', 'abc', {})
		appendSummary('done', {})
		setEnv('A', '1', {})
		t.mock.restoreAll()
		assert.deepEqual(written, ['sha=abc\n', 'done\n'])
	})
})

describe('exec errors', () => {
	it('throws when the command cannot start', () => {
		assert.throws(() => exec('definitely-not-a-command-778'), /ENOENT/)
	})

	it('reports a signal-killed command as status 1, and a failure with no stderr', () => {
		assert.equal(exec('node', ['-e', 'process.kill(process.pid, "SIGKILL")']).status, 1)
		assert.throws(() => run('node', ['-e', 'process.exit(2)']), /exited 2$/)
	})

	it('streams output instead of capturing it when inherit is set', () => {
		assert.equal(exec('node', ['-e', ''], { inherit: true }).stdout, '')
	})
})

describe('annotation', () => {
	it('formats a bare annotation', () => {
		assert.equal(annotation('error', 'bad'), '::error::bad')
	})

	it('formats file, line, and title properties', () => {
		assert.equal(annotation('warning', 'x', { file: 'a.yml', line: 3, title: 'T' }), '::warning file=a.yml,line=3,title=T::x')
	})
})

describe('required', () => {
	it('returns a set variable and throws naming an unset one', () => {
		assert.equal(required({ A: 'v' }, 'A'), 'v')
		assert.throws(() => required({ A: '' }, 'A'), /A is not set/)
	})
})

describe('isMain', () => {
	it('is false when there is no script argument', () => {
		const saved = process.argv[1]
		process.argv[1] = ''
		try { assert.equal(isMain(import.meta.url), false) } finally { process.argv[1] = saved }
	})

	it('is false for a module that is imported', () => {
		assert.equal(isMain(new URL('../../../tools/lib/actions.ts', import.meta.url).href), false)
	})
})
