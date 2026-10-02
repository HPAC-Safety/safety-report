#!/usr/bin/env node
// Installs the pinned Terraform (and, with --tflint, the pinned tflint) into
// /usr/local/bin on a runner. Shared by terraform.yml, terraform-relock.yml, and
// deploy-environment.yml, which each used to carry the same inline copy.
//
// Versions come out of the files that pin them — infra/.terraform-version and
// infra/.tflint-version — never out of a workflow. AGENTS.md: a tool version is
// pinned in exactly one file, and a second copy is a copy that will drift
// (ADR-0015).
//
//   node tools/infra/install-terraform.mjs [--tflint]
//
// Order matches the old inline step: read both versions, announce them, fetch
// and install Terraform, then tflint, then print each tool's version.
import { readFileSync } from 'node:fs'
import { exec as realExec, isMain } from '../lib/actions.mjs'

export const INSTALL_DIR = '/usr/local/bin'

/** A version file's content with every whitespace character removed (`tr -d '[:space:]'`). */
export function readVersion(file, read = (f) => readFileSync(f, 'utf8')) {
	return read(file).replace(/\s/g, '')
}

export function terraformUrl(version) {
	return `https://releases.hashicorp.com/terraform/${version}/terraform_${version}_linux_amd64.zip`
}

export function tflintUrl(version) {
	return `https://github.com/terraform-linters/tflint/releases/download/${version}/tflint_linux_amd64.zip`
}

/** Downloads a zip and unpacks the one binary into the install directory. */
function install(exec, { name, url }) {
	const zip = `/tmp/${name}.zip`
	exec('curl', ['-fsSL', '-o', zip, url], { check: true, inherit: true })
	exec('unzip', ['-q', '-o', zip, '-d', INSTALL_DIR], { check: true, inherit: true })
	exec('chmod', ['+x', `${INSTALL_DIR}/${name}`], { check: true, inherit: true })
}

export function main({ argv = process.argv.slice(2), exec = realExec, log = console.log, read } = {}) {
	const withTflint = argv.includes('--tflint')
	const tfVersion = readVersion('infra/.terraform-version', read)
	const tflintVersion = withTflint ? readVersion('infra/.tflint-version', read) : null

	if (withTflint) log(`Terraform ${tfVersion}, tflint ${tflintVersion}`)

	install(exec, { name: 'terraform', url: terraformUrl(tfVersion) })
	if (withTflint) install(exec, { name: 'tflint', url: tflintUrl(tflintVersion) })

	exec('terraform', ['version'], { check: true, inherit: true })
	if (withTflint) exec('tflint', ['--version'], { check: true, inherit: true })
	return 0
}

if (isMain(import.meta.url)) process.exit(main())
