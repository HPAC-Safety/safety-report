import { describe, it } from 'node:test'
import assert from 'node:assert/strict'
import { mkdirSync, mkdtempSync, writeFileSync } from 'node:fs'
import { tmpdir } from 'node:os'
import path from 'node:path'

import { commandLines, main, problem, runSteps } from '../../../tools/github/check-workflow-steps.ts'

function repo(files: Record<string, string>): string {
	const root = mkdtempSync(path.join(tmpdir(), 'workflow-steps-'))
	for (const [file, text] of Object.entries(files)) {
		mkdirSync(path.dirname(path.join(root, file)), { recursive: true })
		writeFileSync(path.join(root, file), text)
	}
	return root
}

/** The reason a script is refused; fails the test when it is not. */
function reason(script: string): string {
	const refused = problem(script)
	assert.ok(refused, 'expected the script to be refused')
	return refused
}

describe('runSteps', () => {
	it('reads plain, quoted, and block run values with their lines', () => {
		const text = [
			'jobs:',
			'  a:',
			'    steps:',
			'      - run: node tools/x.ts',
			"      - run: 'echo hi'",
			'      - name: block',
			'        run: |',
			'          one',
			'',
			'          two',
			'      - uses: actions/checkout@v5',
		].join('\n')
		assert.deepEqual(runSteps(text), [
			{ line: 4, script: 'node tools/x.ts' },
			{ line: 5, script: 'echo hi' },
			{ line: 7, script: 'one\n\ntwo' },
		])
	})
})

describe('commandLines', () => {
	it('drops comments and blanks and joins continuations', () => {
		assert.deepEqual(commandLines('# why\n\ndotnet test \\\n  --no-build \\\n  -c Release\n'), ['dotnet test --no-build -c Release'])
	})
})

describe('problem', () => {
	it('allows one command, with a pipe, a redirect, or a substitution', () => {
		assert.equal(problem('node tools/a.ts --flag'), null)
		assert.equal(problem('echo "$TOKEN" | docker login ghcr.io --password-stdin'), null)
		assert.equal(problem('echo "sha=$(git rev-parse HEAD)" >> "$GITHUB_OUTPUT"'), null)
	})

	it('refuses a second command line', () => {
		assert.match(reason('npm ci\nnpm test'), /holds 2 command lines/)
	})

	it('refuses shell control flow', () => {
		assert.match(reason('if [ -n "$X" ]; then echo y; fi'), /control flow/)
		assert.match(reason('for f in a b; do echo $f; done'), /control flow/)
	})

	it('refuses a command list', () => {
		assert.match(reason('sudo apt-get update && sudo apt-get install -y ffmpeg'), /chains commands/)
		assert.match(reason('make || true'), /chains commands/)
		assert.match(reason('cd a; ls'), /chains commands/)
	})

	it('ignores keywords and separators inside quotes', () => {
		assert.equal(problem('echo "::notice::if this; then that && more"'), null)
		assert.equal(problem("echo 'for; while'"), null)
	})
})

describe('main', () => {
	it('passes a repository whose steps are each one command', () => {
		const root = repo({
			'.github/workflows/a.yml': 'jobs:\n  a:\n    steps:\n      - run: node tools/a.ts\n',
			'.github/actions/x/action.yml': 'runs:\n  steps:\n    - run: node tools/b.ts\n      shell: bash\n',
		})
		const lines: string[] = []
		assert.equal(main(root, (line) => lines.push(line)), 0)
		assert.match(lines.at(-1) ?? '', /2 run step\(s\)/)
	})

	it('fails and annotates the file and line of an inline block', () => {
		const root = repo({ '.github/workflows/a.yml': 'jobs:\n  a:\n    steps:\n      - run: |\n          if true; then echo; fi\n' })
		const lines: string[] = []
		assert.equal(main(root, (line) => lines.push(line)), 1)
		assert.match(lines[0], /^::error file=\.github\/workflows\/a\.yml,line=4::/)
	})
})
