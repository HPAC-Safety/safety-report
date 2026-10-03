// Runs each infra script as a child process, the way a workflow step does,
// with a PATH holding only stub executables: this covers the isMain guard and
// the default parameters (real env, real exec) that the in-process tests inject.
import { describe, it } from 'node:test'
import assert from 'node:assert/strict'
import { chmodSync, mkdirSync, mkdtempSync, writeFileSync } from 'node:fs'
import { tmpdir } from 'node:os'
import path from 'node:path'
import { spawnSync } from 'node:child_process'

import { main as requireConfig } from '../../../tools/infra/require-config.ts'
import { main as copyVendorKeys } from '../../../tools/infra/deploy/copy-vendor-keys.ts'
import { main as readImageRegistries } from '../../../tools/infra/deploy/read-image-registries.ts'
import { main as smokeTest } from '../../../tools/infra/deploy/smoke-test-health.ts'
import { parseOutputs } from '../../../tools/infra/check-terraform-outputs.ts'
import { fakeExec, tempFile } from './fake-exec.ts'

const REPO = path.resolve(import.meta.dirname, '../../..')
const TOOLS = path.join(REPO, 'tools/infra')

/** A temp working directory with the given relative files written into it. */
function workdir(files: Record<string, string> = {}): string {
	const dir = mkdtempSync(path.join(tmpdir(), 'infra-run-'))
	for (const [name, content] of Object.entries(files)) {
		mkdirSync(path.dirname(path.join(dir, name)), { recursive: true })
		writeFileSync(path.join(dir, name), content)
	}
	return dir
}

/** Runs a script with `stubs` ({ command: shell body }) as the only executables on PATH. */
function run(
	script: string,
	{ env = {}, stubs = {}, cwd = workdir(), args = [] }: { env?: Record<string, string>; stubs?: Record<string, string>; cwd?: string; args?: string[] } = {},
) {
	const bin = mkdtempSync(path.join(tmpdir(), 'infra-bin-'))
	for (const [name, body] of Object.entries(stubs)) {
		writeFileSync(path.join(bin, name), `#!/bin/sh\n${body}\n`)
		chmodSync(path.join(bin, name), 0o755)
	}
	return spawnSync(process.execPath, [path.join(TOOLS, script), ...args], {
		cwd,
		env: { PATH: bin, ...env },
		encoding: 'utf8',
	})
}

describe('infra scripts run as commands', () => {
	it('check-aws-configured writes its output', () => {
		const out = tempFile('output')
		const result = run('check-aws-configured.ts', { env: { SAME_REPO: 'false', GITHUB_OUTPUT: out.file } })
		assert.equal(result.status, 0)
		assert.match(out.read(), /ready=false/)
	})

	it('comment-terraform-plan reads plan.txt, writes comment.md, and comments', () => {
		const result = run('comment-terraform-plan.ts', {
			cwd: workdir({ 'plan.txt': 'No changes.' }),
			env: { ACCOUNT: 'staging', PLAN_OUTCOME: 'success', PR_NUMBER: '7' },
			stubs: { gh: 'exit 0' },
		})
		assert.equal(result.status, 0)
	})

	it('install-terraform installs terraform alone by default', () => {
		const result = run('install-terraform.ts', {
			cwd: workdir({ 'infra/.terraform-version': '1.9.0\n' }),
			stubs: { curl: 'exit 0', unzip: 'exit 0', chmod: 'exit 0', terraform: 'exit 0' },
		})
		assert.equal(result.status, 0)
	})

	it('open-relock-pr updates the open pull request', () => {
		const result = run('open-relock-pr.ts', {
			env: { GH_TOKEN: 't', RELOCK_BRANCH: 'relock', GITHUB_REPOSITORY: 'o/r' },
			stubs: { git: 'exit 0', gh: 'exit 0' },
		})
		assert.equal(result.status, 0)
	})

	it('read-lockfile-change reports a change when git diff fails', () => {
		const out = tempFile('output')
		const result = run('read-lockfile-change.ts', { env: { GITHUB_OUTPUT: out.file }, stubs: { git: 'exit 1' } })
		assert.equal(result.status, 0)
		assert.equal(out.read(), 'changed=true\n')
	})

	it('require-config passes with nothing required', () => {
		const result = run('require-config.ts')
		assert.equal(result.status, 0)
	})

	it('check-terraform-outputs passes against the repository', () => {
		const result = run('check-terraform-outputs.ts', { cwd: REPO })
		assert.equal(result.status, 0, result.stderr)
	})

	it('copy-vendor-keys puts both keys', () => {
		const result = run('deploy/copy-vendor-keys.ts', {
			env: { GEMINI_API_KEY: 'g', DEEPL_API_KEY: 'd' },
			stubs: { terraform: `echo '{"gemini_api_key":"a","deepl_api_key":"b"}'`, aws: 'exit 0' },
		})
		assert.equal(result.status, 0)
	})

	it('fail-on-drift exits with the plan status', () => {
		const result = run('deploy/fail-on-drift.ts', {
			env: { GITHUB_WORKSPACE: '/w', TFVARS_FILE: 'a.tfvars' },
			stubs: { terraform: 'exit 0' },
		})
		assert.equal(result.status, 0)
	})

	it('generate-interim-issuer-key stops when the issuer is disabled', () => {
		const result = run('deploy/generate-interim-issuer-key.ts', { stubs: { terraform: 'exit 0' } })
		assert.equal(result.status, 0)
	})

	it('push-images pushes and reports digests', () => {
		const out = tempFile('output')
		const result = run('deploy/push-images.ts', {
			env: { SHA: 'abc', API_REPO: 'r/api', WORKER_REPO: 'r/worker', GITHUB_OUTPUT: out.file },
			stubs: { docker: 'echo r/x@sha256:1' },
		})
		assert.equal(result.status, 0)
		assert.match(out.read(), /api_digest=sha256:1/)
	})

	it('read-deploy-variables writes each variable', () => {
		const out = tempFile('output')
		const result = run('deploy/read-deploy-variables.ts', {
			env: { GITHUB_OUTPUT: out.file },
			stubs: { terraform: `echo '{"A":"b"}'` },
		})
		assert.equal(result.status, 0)
		assert.equal(out.read(), 'A=b\n')
	})

	it('read-image-registries writes the repositories', () => {
		const out = tempFile('output')
		const state = '{"values":{"root_module":{"resources":[]}}}'
		const result = run('deploy/read-image-registries.ts', {
			env: { GITHUB_OUTPUT: out.file },
			stubs: { terraform: `echo '${state}'` },
		})
		assert.equal(result.status, 0)
	})

	it('replace-nat-instance waits for a successful refresh', () => {
		const result = run('deploy/replace-nat-instance.ts', {
			stubs: { terraform: 'echo arn:aws:autoscaling:x:group/nat', aws: 'echo Successful' },
		})
		assert.equal(result.status, 0)
	})

	it('report-dns-records appends to the step summary', () => {
		const summary = tempFile('summary')
		const result = run('deploy/report-dns-records.ts', {
			env: { GITHUB_STEP_SUMMARY: summary.file },
			stubs: { terraform: 'echo {}' },
		})
		assert.equal(result.status, 0)
		assert.match(summary.read(), /DNS records/)
	})

	it('report-image-digests appends to the step summary', () => {
		const summary = tempFile('summary')
		const result = run('deploy/report-image-digests.ts', {
			env: { GITHUB_STEP_SUMMARY: summary.file, ENVIRONMENT_NAME: 'staging', API_DIGEST: 'a', WORKER_DIGEST: 'w' },
		})
		assert.equal(result.status, 0)
		assert.match(summary.read(), /staging/)
	})

	it('smoke-test-health answers healthy', () => {
		const result = run('deploy/smoke-test-health.ts', {
			env: { PUBLIC_URL: 'https://example.test/' },
			stubs: { curl: 'printf 200' },
		})
		assert.equal(result.status, 0)
	})

	it('untaint-protected-resources reads infra/*.tf and untaints nothing', () => {
		const result = run('deploy/untaint-protected-resources.ts', {
			cwd: workdir({ 'infra/main.tf': 'resource "a" "b" {}\n', 'infra/notes.txt': 'x' }),
			stubs: { terraform: 'echo {}' },
		})
		assert.equal(result.status, 0)
	})

	it('update-lambda-function updates and waits', () => {
		const result = run('deploy/update-lambda-function.ts', {
			env: { FUNCTION_NAME: 'f', IMAGE_URI: 'r/i:1' },
			stubs: { aws: 'exit 0' },
		})
		assert.equal(result.status, 0)
	})
})

describe('defaults the injected tests skip', () => {
	it('require-config reads an empty environment', () => {
		const logs: string[] = []
		assert.equal(requireConfig({ env: {}, log: (m) => logs.push(m) }), 0)
		assert.deepEqual(logs, ['All required configuration is present.'])
	})

	it('copy-vendor-keys puts an empty value for a missing key', () => {
		const { exec, calls } = fakeExec({ 'terraform -chdir=infra output -json secret_entries': { stdout: '{"gemini_api_key":"g","deepl_api_key":"d"}' } })
		assert.equal(copyVendorKeys({ env: {}, exec, log() {} }), 0)
		assert.deepEqual(calls.slice(2), [
			'aws secretsmanager put-secret-value --secret-id g --secret-string ',
			'aws secretsmanager put-secret-value --secret-id d --secret-string ',
		])
	})

	it('read-image-registries says null for a repository with no URL', () => {
		const state = JSON.stringify({
			values: { root_module: { resources: [{ address: 'aws_ecr_repository.this["api"]', values: { repository_url: null } }] } },
		})
		const out = tempFile('output')
		const { exec } = fakeExec({ 'terraform -chdir=infra show -json': { stdout: state } })
		assert.equal(readImageRegistries({ env: { GITHUB_OUTPUT: out.file }, exec }), 0)
		assert.match(out.read(), /api_repo=null\n/)
	})

	it('smoke-test-health treats an unset PUBLIC_URL as empty', () => {
		const { exec, calls } = fakeExec({ 'curl -fsS -o /dev/null -w %{http_code} /api/health': { stdout: '200' } })
		assert.equal(smokeTest({ env: {}, exec, log() {}, sleep() {} }), 0)
		assert.equal(calls.length, 1)
	})

	it('check-terraform-outputs refuses unbalanced braces', () => {
		assert.throws(() => parseOutputs('output "x" {\n  value = {\n'), /unbalanced braces/)
	})
})
