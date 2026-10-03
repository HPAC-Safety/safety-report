#!/usr/bin/env node
// Untaints the Terraform resources that must never be replaced
// (deploy-environment.yml, #640).
//
// An apply that fails after AWS has created a resource leaves it tainted, and
// Terraform then plans to destroy and recreate it. For a resource carrying
// prevent_destroy (the database, the uploads bucket, the log groups, the
// secrets) that plan is refused, so every later release would stop at that
// point until someone untainted it by hand. Untaint only those; the apply that
// follows brings each one to its configuration in place. Any other tainted
// resource is still replaced.
//
// The protected addresses are read from the configuration itself (every
// `resource` block with `prevent_destroy = true` in infra/*.tf) so the list
// cannot drift.
import { readdirSync, readFileSync } from 'node:fs'
import { type Exec, exec as realExec, isMain } from '../../lib/actions.ts'

/** Addresses (`type.name`) of every resource block holding `prevent_destroy = true`. */
export function protectedAddresses(hclSources: readonly string[]): Set<string> {
	const found = new Set<string>()
	for (const source of hclSources) {
		let address: string | null = null
		for (const line of source.split('\n')) {
			// awk's `$2`/`$3` with the quotes stripped: resource "type" "name" {
			if (line.startsWith('resource "')) {
				const [, type = '', name = ''] = line.split(/\s+/)
				address = `${type.replace(/"/g, '')}.${name.replace(/"/g, '')}`
			}
			if (/prevent_destroy *= *true/.test(line) && address !== null) found.add(address)
		}
	}
	return found
}

/** Addresses of every tainted root-module resource in `terraform show -json` output. */
interface ShownState {
	values?: { root_module?: { resources?: { address: string; tainted?: boolean }[] } }
}

export function taintedAddresses(stateJson: string): string[] {
	const resources = (JSON.parse(stateJson) as ShownState | null)?.values?.root_module?.resources ?? []
	return resources.filter((resource) => resource.tainted === true).map((resource) => resource.address)
}

/** `${address%%[*}`: the address without its first `[index]` suffix. */
export function baseAddress(address: string): string {
	const bracket = address.indexOf('[')
	return bracket === -1 ? address : address.slice(0, bracket)
}

export function main({
	exec = realExec,
	log = console.log,
	readSources = () =>
		readdirSync('infra')
			.filter((name) => name.endsWith('.tf'))
			.sort()
			.map((name) => readFileSync(`infra/${name}`, 'utf8')),
}: { exec?: Exec; log?: (line: string) => void; readSources?: () => string[] } = {}): number {
	const protectedSet = protectedAddresses(readSources())
	const show = exec('terraform', ['-chdir=infra', 'show', '-json'], { check: true })
	for (const address of taintedAddresses(show.stdout)) {
		if (protectedSet.has(baseAddress(address))) {
			log(`Untainting ${address} (prevent_destroy).`)
			exec('terraform', ['-chdir=infra', 'untaint', address], { check: true, inherit: true })
		}
	}
	return 0
}

if (isMain(import.meta.url)) process.exit(main())
