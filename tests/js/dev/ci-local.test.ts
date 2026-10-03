import { describe, it, before, after } from 'node:test'
import assert from 'node:assert/strict'
import { spawnSync } from 'node:child_process'
import { chmodSync, copyFileSync, mkdirSync, mkdtempSync, readdirSync, readFileSync, rmSync, symlinkSync, writeFileSync } from 'node:fs'
import { tmpdir } from 'node:os'
import { dirname, join } from 'node:path'
import { fileURLToPath } from 'node:url'

const REPO = join(dirname(fileURLToPath(import.meta.url)), '..', '..', '..')
const SCRIPT = join(REPO, 'tools/dev/ci-local.sh')
const text = readFileSync(SCRIPT, 'utf8')
const code = text
	.split('\n')
	.filter((line) => !/^\s*#/.test(line))
	.join('\n')

// act receives no token (ADR-0145, #554). act fills a missing GITHUB_TOKEN
// secret from `gh auth token`, so leaving the flag out is not enough: the
// secret has to be given, and given empty.
describe('tools/dev/ci-local.sh gives act no token', () => {
	const act = code.match(/\bif act pull_request[\s\S]*?; then/)?.[0] ?? ''

	it('invokes act in one place this test can read', () => {
		assert.ok(act.length > 0)
	})

	it('passes GITHUB_TOKEN explicitly empty', () => {
		assert.match(act, /-s GITHUB_TOKEN= /)
	})

	it('passes no other secret', () => {
		const secrets = act.match(/(?:^|\s)(?:-s|--secret)\s+\S+/g) ?? []
		assert.deepEqual(secrets.map((s) => s.trim()), ['-s GITHUB_TOKEN='])
		assert.match(act, /--secret-file \/dev\/null/)
	})

	it('unsets the token variables before starting act', () => {
		assert.match(code, /unset GITHUB_TOKEN GH_TOKEN/)
	})

	it('no longer reads HPAC_ACT_TOKEN or offers --allow-gh-token', () => {
		assert.doesNotMatch(text, /HPAC_ACT_TOKEN|allow-gh-token|ALLOW_GH_TOKEN/)
	})
})

describe('the coverage job under act', () => {
	const ci = readFileSync(join(REPO, '.github/workflows/ci.yml'), 'utf8')

	it('skips the token-using baseline download', () => {
		assert.match(ci, /- name: Fetch the main baseline\n\s+id: baseline\n\s+if: .*&& !env\.ACT\n/)
	})

	it('reads the baseline the wrapper fetched, and reports whether it holds a token', () => {
		assert.match(ci, /id: baseline-local\n\s+if: .*&& env\.ACT\n[\s\S]*?run: node tools\/coverage\/read-local-baseline\.ts\n/)
		assert.match(readFileSync(join(REPO, 'tools/coverage/read-local-baseline.ts'), 'utf8'), /ci-local:github-token=/)
		assert.match(ci, /steps\.baseline\.outputs\.path \|\| steps\.baseline-local\.outputs\.path \|\| 'none'/)
	})
})

// Stubbed gh and act on PATH: no real login, token, or act run is involved.
describe('tools/dev/ci-local.sh without a gh login', () => {
	let dir: string
	let body: string

	before(() => {
		dir = mkdtempSync(join(tmpdir(), 'ci-local-test-'))
		body = join(dir, 'body.md')
		writeFileSync(body, 'Closes #0\n')
		// gh is installed but logged out; act reports a version no pin names, so
		// a run that gets past the login check stops there, before Docker.
		writeFileSync(join(dir, 'gh'), '#!/bin/sh\n[ "$1 $2" = "auth status" ] && exit 1\nexit 1\n')
		writeFileSync(join(dir, 'act'), '#!/bin/sh\necho "act version 0.0.0-stub"\n')
		chmodSync(join(dir, 'gh'), 0o755)
		chmodSync(join(dir, 'act'), 0o755)
	})

	after(() => rmSync(dir, { recursive: true, force: true }))

	const run = (...args: string[]) =>
		spawnSync('sh', [SCRIPT, '--body', body, ...args], {
			cwd: REPO,
			encoding: 'utf8',
			env: { ...process.env, PATH: `${dir}:${process.env.PATH}`, GH_TOKEN: '', GITHUB_TOKEN: '' },
		})

	it('stops before act, naming gh auth login, when the run includes coverage (--full)', () => {
		const result = run('--full')
		assert.equal(result.status, 2)
		assert.match(result.stderr, /coverage ratchet needs main's baseline artifact.*run gh auth login/)
	})

	it('needs no login for the default fast run, which skips coverage', () => {
		const result = run()
		assert.equal(result.status, 2)
		assert.doesNotMatch(result.stderr, /gh auth login/)
		assert.match(result.stderr, /act 0\.0\.0-stub is installed/)
	})

	it('refuses --full together with --job', () => {
		const result = run('--full', '--job', 'docs')
		assert.equal(result.status, 2)
		assert.match(result.stderr, /--full and --job are exclusive/)
	})

	it('stops the same way for --job coverage', () => {
		const result = run('--job', 'coverage')
		assert.equal(result.status, 2)
		assert.match(result.stderr, /run gh auth login/)
	})

	it('needs no login for a job other than coverage', () => {
		const result = run('--job', 'linked-issue')
		assert.equal(result.status, 2)
		assert.doesNotMatch(result.stderr, /gh auth login/)
		assert.match(result.stderr, /act 0\.0\.0-stub is installed/)
	})

	it('refuses the removed --allow-gh-token flag', () => {
		const result = run('--allow-gh-token')
		assert.equal(result.status, 2)
		assert.match(result.stderr, /unknown option: --allow-gh-token/)
	})
})

// On GitHub two bots commit onto a same-repository pull request before its
// verdict settles: the regenerated traceability matrix and the French. Neither
// runs locally, so ci-local stands in for both without changing what GitHub
// runs (ADR-0145, #546).
describe('what the bots would commit', () => {
	const ci = readFileSync(join(REPO, '.github/workflows/ci.yml'), 'utf8')
	const step = ci.match(/- name: Locale translation is in step with English\n\s+run: (.*)\n/)?.[1] ?? ''

	it('the i18n check tolerates pending French only when ACT is set', () => {
		assert.equal(step, 'node tools/i18n/translate-locale.ts --check --locales locales ${ACT:+--allow-pending-translation}')
	})

	describe('running that step against French still pending as a # stub', () => {
		let dir: string

		before(() => {
			dir = mkdtempSync(join(tmpdir(), 'ci-local-i18n-'))
			mkdirSync(join(dir, 'locales'))
			for (const file of readdirSync(join(REPO, 'locales'))) {
				copyFileSync(join(REPO, 'locales', file), join(dir, 'locales', file))
			}
			symlinkSync(join(REPO, 'tools'), join(dir, 'tools'))
			// One French value turned back into the stub the pre-commit hook
			// writes for a new English key.
			const french = JSON.parse(readFileSync(join(dir, 'locales/fr-CA.json'), 'utf8')) as Record<string, unknown>
			const stub = (node: Record<string, unknown>): boolean => {
				for (const [key, value] of Object.entries(node)) {
					if (typeof value === 'string') {
						node[key] = `#${value}`
						return true
					}
					if (value && typeof value === 'object' && stub(value as Record<string, unknown>)) return true
				}
				return false
			}
			assert.ok(stub(french))
			writeFileSync(join(dir, 'locales/fr-CA.json'), `${JSON.stringify(french, null, 2)}\n`)
		})

		after(() => rmSync(dir, { recursive: true, force: true }))

		const runStep = (env: Record<string, string>) =>
			spawnSync('sh', ['-c', step], { cwd: dir, encoding: 'utf8', env: { PATH: process.env.PATH, ...env } })

		it('fails on GitHub, where ACT is unset', () => {
			const result = runStep({})
			assert.equal(result.status, 1)
			assert.match(result.stderr, /still a local # stub/)
		})

		it('passes under act, reporting the stub as a notice', () => {
			const result = runStep({ ACT: 'true' })
			assert.equal(result.status, 0, result.stderr)
			assert.match(result.stdout, /::notice::.*still a local # stub/)
		})
	})

	it('ci-local regenerates the matrix in the clone and commits it there', () => {
		assert.match(code, /diff --quiet "\$BASE_SHA" HEAD -- tools\/spec\/generate-traceability\.ts tools\/spec\/generate-spec-index\.ts tools\/spec\/spec-paths\.ts tools\/spec\/generate-bindings\.ts/)
		assert.match(code, /cd "\$WORK\/repo" && node tools\/spec\/generate-traceability\.ts >\/dev\/null 2>&1 && node tools\/spec\/generate-spec-index\.ts >\/dev\/null 2>&1 && node tools\/spec\/generate-bindings\.ts --no-fail/)
		assert.match(code, /commit -q --no-verify -m "Regenerate the traceability matrix[^"]*"\s*\\\s*-- \.spec\/traceability\.md \.spec\/README\.md \.spec\/bindings\.md/)
		assert.match(code, /update-ref "refs\/remotes\/origin\/\$BRANCH" "\$HEAD_SHA"/)
	})

	it('regenerates before the event is written, so head.sha is the regenerated commit', () => {
		assert.ok(code.indexOf('node tools/spec/generate-traceability.ts') < code.indexOf('HEAD_SHA="$HEAD_SHA"'))
	})
})

// vstest copies each project's report into a per-run directory named to the
// second as well as its GUID attachment directory; two projects finishing in
// the same second made the merge read 11 files instead of 12 (#546).
describe('the coverage merge', () => {
	const ci = readFileSync(join(REPO, '.github/workflows/ci.yml'), 'utf8')

	it('reads only the per-project attachment copies', () => {
		assert.match(ci, /"-reports:\.\/artifacts\/coverage\/\*\/coverage\.cobertura\.xml;/)
		assert.doesNotMatch(ci, /-reports:\.\/artifacts\/coverage\/\*\*\/coverage\.cobertura\.xml/)
	})
})

// The default run is the fast checks; --full is the whole GitHub suite (#687,
// ADR-0145).
describe('the modes of tools/dev/ci-local.sh', () => {
	const fast = code.match(/^FAST_CI_JOBS='([^']*)'/m)?.[1].split(/\s+/) ?? []
	const ci = readFileSync(join(REPO, '.github/workflows/ci.yml'), 'utf8')
	const ciJobs = [...ci.matchAll(/^ {2}([a-z][a-z0-9-]*):$/gm)].map((m) => m[1])
	const branches = code.match(/^if \[ -n "\$JOBS" \][\s\S]*?\nfi\n/m)?.[0] ?? ''
	const [, fullBranch = '', fastBranch = ''] = branches.split(/\nelif |\nelse\n/)

	it('runs build, lint, web, i18n, docs, cucumber, and agent-config by default', () => {
		assert.deepEqual(fast, ['build', 'lint', 'web', 'i18n', 'docs', 'cucumber', 'agent-config'])
	})

	it('runs only ci.yml jobs that exist', () => {
		for (const job of fast) assert.ok(ciJobs.includes(job), `${job} is not a job in ci.yml`)
	})

	it('skips test, coverage, e2e, and terraform by default', () => {
		for (const job of ['test', 'coverage', 'e2e', 'infra']) assert.ok(!fast.includes(job))
		assert.ok(fastBranch.length > 0)
		assert.match(fastBranch, /run_act linked-issue\.yml && run_act feature-coverage\.yml/)
		assert.match(fastBranch, /FAST_CI_JOBS/)
		assert.doesNotMatch(fastBranch, /terraform|infra/)
		assert.doesNotMatch(fastBranch, /run_act ci\.yml\s*\|\|/)
	})

	it('--full runs every workflow, terraform and all of ci.yml included', () => {
		assert.match(fullBranch, /"\$FULL" -eq 1/)
		assert.match(fullBranch, /run_act terraform\.yml infra/)
		assert.match(fullBranch, /run_act ci\.yml \|\| FAILED=1/)
	})

	it('--job runs exactly the jobs named, whatever the default is', () => {
		assert.match(branches, /^if \[ -n "\$JOBS" \]; then\n\tfor job in \$JOBS; do\n\t\trun_act "\$\(workflow_of "\$job"\)" "\$job"/)
	})

	it('needs main\'s baseline only for --full or --job coverage', () => {
		assert.match(code, /\[ "\$FULL" -eq 0 \] \|\| NEED_BASELINE=1/)
		assert.match(code, /case " \$JOBS " in \*' coverage '\*\) NEED_BASELINE=1/)
	})
})

// `test` runs the suites once, collecting coverage; `coverage` only merges and
// gates what it handed over (#694). Under act the hand-over is the per-run
// directory tools/dev/ci-local.sh mounts, since act rejects the artifact actions.
describe('the suites run once', () => {
	const ci = readFileSync(join(REPO, '.github/workflows/ci.yml'), 'utf8')
	const job = (id: string) => ci.match(new RegExp(`^ {2}${id}:\\n[\\s\\S]*?(?=^ {2}[a-z][a-z0-9-]*:\\n|(?![\\s\\S]))`, 'm'))?.[0] ?? ''
	const steps = (id: string) =>
		job(id)
			.split('\n')
			.filter((line) => !/^\s*#/.test(line))
			.join('\n')

	it('runs dotnet test in exactly one place, the test job, collecting coverage', () => {
		assert.equal(steps('test').match(/\bdotnet test HpacSafety\.slnx/g)?.length, 1)
		assert.match(steps('test'), /--collect:"XPlat Code Coverage"/)
		assert.match(steps('test'), /--settings coverlet\.runsettings/)
		assert.match(steps('test'), /--logger trx/)
		assert.equal(ci.match(/^\s*dotnet test HpacSafety\.slnx/gm)?.length, 1)
	})

	it('runs node --test with coverage in the test job', () => {
		assert.match(steps('test'), /run: node tools\/coverage\/run-js-tests\.ts\n/)
		const script = readFileSync(join(REPO, 'tools/coverage/run-js-tests.ts'), 'utf8')
		assert.match(script, /'--experimental-test-coverage'/)
		assert.match(script, /'--test-reporter=lcov'/)
		assert.match(script, /LCOV = '\.\/artifacts\/coverage\/js\/lcov\.info'/)
	})

	it('runs no test in the coverage job', () => {
		const coverage = steps('coverage')
		assert.ok(coverage.length > 0)
		assert.doesNotMatch(coverage, /dotnet test|node --test/)
	})

	it('runs the test job on every change the coverage job gates', () => {
		assert.match(job('test'), /needs\.changes\.outputs\.dotnet == 'true' \|\| needs\.changes\.outputs\.e2e == 'true'/)
		assert.match(job('coverage'), /needs: \[changes, test, e2e\]/)
		assert.match(job('coverage'), /needs\.test\.result == 'success'/)
	})

	it('hands the raw reports over as coverage-raw on GitHub', () => {
		assert.match(steps('test'), /upload-artifact@\S+\n\s+if: "!env\.ACT"\n\s+with:\n\s+name: coverage-raw\n\s+path: \.\/artifacts\/coverage\n/)
		assert.match(steps('coverage'), /download-artifact@\S+\n\s+with:\n\s+name: coverage-raw\n\s+path: \.\/artifacts\/coverage\n/)
	})

	it('hands them over through /ci-local-share under act', () => {
		assert.match(steps('test'), /if: env\.ACT\n\s+run: node tools\/coverage\/hand-over-raw-reports\.ts push\n/)
		assert.match(steps('coverage'), /if: env\.ACT\n\s+run: node tools\/coverage\/hand-over-raw-reports\.ts pull\n/)
		const script = readFileSync(join(REPO, 'tools/coverage/hand-over-raw-reports.ts'), 'utf8')
		assert.match(script, /'\/ci-local-share'/)
		assert.match(script, /exec\('cp', \['-R', `\$\{LOCAL\}\/\.`, `\$\{shared\}\/`\]/)
		assert.match(script, /exec\('cp', \['-R', `\$\{shared\}\/\.`, `\$\{LOCAL\}\/`\]/)
	})

	it('mounts a per-run directory, inside the run\'s work directory, at /ci-local-share', () => {
		assert.match(code, /^SHARE="\$WORK\/share"$/m)
		assert.match(code, /--container-options "\$LABELS -v \$SHARE:\/ci-local-share"/)
	})
})
