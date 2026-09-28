import { describe, it, before, after } from 'node:test'
import assert from 'node:assert/strict'
import { spawnSync } from 'node:child_process'
import { chmodSync, mkdtempSync, rmSync, writeFileSync } from 'node:fs'
import { tmpdir } from 'node:os'
import { join } from 'node:path'

const SCRIPT = new URL('../../tools/find-coverage-baseline.mjs', import.meta.url).pathname

/**
 * A stub `gh` on PATH, standing in for `gh run list` (one call) and `gh api
 * .../artifacts` (one call per candidate, in order). No real network or login
 * is involved - the same style as tools/ci-local.sh's own tests stub `gh` and
 * `act`.
 */
function stubGh(dir, { runs, artifactCounts }) {
	const script = `#!/bin/sh
if [ "$1" = "run" ] && [ "$2" = "list" ]; then
  cat <<'RUNS'
${JSON.stringify(runs)}
RUNS
  exit 0
fi
if [ "$1" = "api" ]; then
  case "$2" in
${Object.entries(artifactCounts)
	.map(([id, count]) => `    *"/runs/${id}/artifacts"*) echo ${count}; exit 0 ;;`)
	.join('\n')}
    *) echo 0; exit 0 ;;
  esac
fi
echo "unexpected gh invocation: $*" >&2
exit 1
`
	const path = join(dir, 'gh')
	writeFileSync(path, script)
	chmodSync(path, 0o755)
}

/** Runs the script as CI and tools/ci-local.sh do, and reports what each sees. */
function run(dir) {
	const result = spawnSync(process.execPath, [SCRIPT, '--repo', 'HPAC-Safety/safety-report'], {
		encoding: 'utf8',
		env: { PATH: `${dir}:${process.env.PATH}` },
	})
	return { code: result.status, stdout: result.stdout, stderr: result.stderr }
}

describe('the coverage baseline command', () => {
	let dir

	before(() => {
		dir = mkdtempSync(join(tmpdir(), 'find-coverage-baseline-'))
	})

	after(() => rmSync(dir, { recursive: true, force: true }))

	describe('given the newest push run on main has no coverage-report artifact', () => {
		it('when it runs then it prints the older run that has one', () => {
			// Given - run 3 is the newest, but only run 2 ran coverage
			stubGh(dir, {
				runs: [
					{ databaseId: 3, event: 'push', headBranch: 'main', conclusion: 'success' },
					{ databaseId: 2, event: 'push', headBranch: 'main', conclusion: 'success' },
				],
				artifactCounts: { 3: 0, 2: 1 },
			})

			// When
			const { code, stdout, stderr } = run(dir)

			// Then
			assert.equal(code, 0)
			assert.equal(stdout.trim(), '2')
			assert.match(stderr, /::notice::Baseline from main run 2\./)
		})
	})

	describe('given no candidate carries a non-expired coverage-report artifact', () => {
		it('when it runs then it prints none and explains the walk-back', () => {
			// Given
			stubGh(dir, {
				runs: [
					{ databaseId: 3, event: 'push', headBranch: 'main', conclusion: 'success' },
					{ databaseId: 2, event: 'push', headBranch: 'main', conclusion: 'success' },
				],
				artifactCounts: { 3: 0, 2: 0 },
			})

			// When
			const { code, stdout, stderr } = run(dir)

			// Then
			assert.equal(code, 0)
			assert.equal(stdout.trim(), 'none')
			assert.match(stderr, /::notice::Walked back 2 successful push run\(s\) of CI on main/)
		})
	})

	describe('given no repo is given', () => {
		it('when it runs then it refuses rather than guessing one', () => {
			// Given / When
			const result = spawnSync(process.execPath, [SCRIPT], { encoding: 'utf8' })

			// Then
			assert.equal(result.status, 2)
			assert.match(result.stderr, /--repo is required/)
		})
	})
})
