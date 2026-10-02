import { test } from 'node:test'
import assert from 'node:assert/strict'
import { mkdtempSync, mkdirSync, writeFileSync, rmSync, readFileSync } from 'node:fs'
import { tmpdir } from 'node:os'
import { join } from 'node:path'
import {
	CI_WORKFLOW,
	DEFAULT_LCOV,
	REPO_ROOT,
	globMatches,
	main,
	problems,
	reportGlobs,
	sourceFiles,
} from '../../../tools/web/check-web-lcov.mjs'

const WORKFLOW = 'run: >\n  dotnet tool run reportgenerator\n  "-reports:./artifacts/coverage/*/coverage.cobertura.xml;./artifacts/coverage/**/lcov.info"\n'

function repo(files) {
	const root = mkdtempSync(join(tmpdir(), 'web-lcov-'))
	for (const [name, content] of Object.entries(files)) {
		const path = join(root, name)
		mkdirSync(join(path, '..'), { recursive: true })
		writeFileSync(path, content)
	}
	return root
}

function quietly(run) {
	const log = console.log
	const error = console.error
	const lines = []
	console.log = (...args) => lines.push(args.join(' '))
	console.error = (...args) => lines.push(args.join(' '))
	try {
		return { status: run(), lines }
	} finally {
		console.log = log
		console.error = error
	}
}

test('the merge globs are read from the workflow, without their leading ./', () => {
	assert.deepEqual(reportGlobs(WORKFLOW), ['artifacts/coverage/*/coverage.cobertura.xml', 'artifacts/coverage/**/lcov.info'])
	assert.deepEqual(reportGlobs('no merge here'), [])
})

test('the real ci.yml merges the lcov where vite.config.ts writes it', () => {
	const globs = reportGlobs(readFileSync(join(REPO_ROOT, CI_WORKFLOW), 'utf8'))
	assert.ok(globs.some((glob) => globMatches(glob, DEFAULT_LCOV)), globs.join(';'))
})

test('a double star spans any number of directories and a single star one', () => {
	assert.ok(globMatches('artifacts/coverage/**/lcov.info', 'artifacts/coverage/web/lcov.info'))
	assert.ok(globMatches('artifacts/coverage/**/lcov.info', 'artifacts/coverage/lcov.info'))
	assert.ok(globMatches('artifacts/coverage/**/lcov.info', 'artifacts/coverage/a/b/lcov.info'))
	assert.ok(globMatches('artifacts/coverage/*/x.xml', 'artifacts/coverage/api/x.xml'))
	assert.ok(!globMatches('artifacts/coverage/*/x.xml', 'artifacts/coverage/a/b/x.xml'))
	assert.ok(!globMatches('artifacts/coverage/**/lcov.info', 'artifacts/other/web/lcov.info'))
	assert.ok(!globMatches('artifacts/coverage/**/lcov.info', 'artifacts/coverage/web/lcovXinfo'))
})

test('the source files are the SF: records', () => {
	assert.deepEqual(sourceFiles('TN:\nSF:src/web/src/a.ts\nDA:1,1\nend_of_record\nSF:src/web/src/b.ts\n'), ['src/web/src/a.ts', 'src/web/src/b.ts'])
})

test('an lcov named from the repository root, where the merge reads it, has no problem', () => {
	const root = repo({ 'src/web/src/a.ts': '' })
	try {
		assert.deepEqual(problems({ lcovPath: DEFAULT_LCOV, lcov: 'SF:src/web/src/a.ts\n', workflow: WORKFLOW, root }), [])
	} finally {
		rmSync(root, { recursive: true })
	}
})

test('a path relative to src/web, an absolute path, and an lcov outside the merge are each refused', () => {
	const root = repo({ 'src/web/src/a.ts': '' })
	try {
		const found = problems({
			lcovPath: 'artifacts/elsewhere/lcov.info',
			lcov: 'SF:src/a.ts\nSF:/abs/src/web/src/a.ts\n',
			workflow: WORKFLOW,
			root,
		})
		assert.equal(found.length, 3)
		assert.match(found[0], /is not read by the coverage merge/)
		assert.match(found[1], /src\/a\.ts does not exist from the repository root/)
		assert.match(found[2], /is absolute/)
	} finally {
		rmSync(root, { recursive: true })
	}
})

test('an lcov naming no file, or a workflow with no merge, is refused', () => {
	const found = problems({ lcovPath: DEFAULT_LCOV, lcov: 'TN:\n', workflow: 'nothing', root: '/' })
	assert.equal(found.length, 2)
	assert.match(found[0], /no ReportGenerator -reports: globs/)
	assert.match(found[1], /names no source file/)
})

test('main passes a good lcov and says how many files it names', () => {
	const root = repo({ 'src/web/src/a.ts': '', [DEFAULT_LCOV]: 'SF:src/web/src/a.ts\n', [CI_WORKFLOW]: WORKFLOW })
	try {
		const { status, lines } = quietly(() => main([], root))
		assert.equal(status, 0)
		assert.match(lines[0], /names 1 file\(s\) from the repository root/)
	} finally {
		rmSync(root, { recursive: true })
	}
})

test('main fails on a missing lcov and on a bad one', () => {
	const root = repo({ [DEFAULT_LCOV]: 'SF:src/a.ts\n', [CI_WORKFLOW]: WORKFLOW })
	try {
		const missing = quietly(() => main(['artifacts/coverage/none/lcov.info'], root))
		assert.equal(missing.status, 1)
		assert.match(missing.lines[0], /run npm --prefix src\/web run test:coverage first/)
		const bad = quietly(() => main([], root))
		assert.equal(bad.status, 1)
		assert.match(bad.lines[0], /does not exist from the repository root/)
	} finally {
		rmSync(root, { recursive: true })
	}
})
