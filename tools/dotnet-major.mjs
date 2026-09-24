#!/usr/bin/env node
// The .NET major is one decision written in four files, and they move together
// (ADR-0120):
//
//   global.json                  the SDK that builds
//   Directory.Build.props        the target framework every project compiles to
//   **/Dockerfile                the runtime image the Worker runs on
//   renovate.json                the major Renovate is allowed to offer
//
// Renovate sees each file on its own. Left alone, it offered the runtime image's
// next major while every project still targeted this one. A framework-dependent
// publish does not roll forward across a major, so the Worker would not start
// (#429). This check fails when the four disagree, so a major moves in one pull
// request that changes all of them.
//
// The exit code is the contract.
import { execFileSync } from 'node:child_process'
import { readFileSync } from 'node:fs'
import { join } from 'node:path'

// Renovate's rule is found by what it matches, not by its description, so
// rewording the description cannot hide it from this check.
export const HELD_PACKAGES = ['dotnet-sdk', 'mcr.microsoft.com/dotnet/**']

const TARGET_FRAMEWORK = /<TargetFramework>net(\d+)\.\d+<\/TargetFramework>/g
const DOTNET_IMAGE = /^\s*FROM\s+mcr\.microsoft\.com\/dotnet\/[^:\s]+:(\d+)\./gim
// The one shape the rule's allowedVersions takes: a regex anchored on the major.
const ALLOWED_MAJOR = /^\/\^(\d+)\\\.\/$/

const majorOf = (version) => version.match(/^(\d+)\./)?.[1] ?? null

/** Every place a .NET major is declared, as `{ source, major }`, plus any problem reading one. */
export function declaredMajors({ globalJson, buildProps, dockerfiles, renovateJson }) {
	const found = []
	const problems = []

	const sdk = JSON.parse(globalJson).sdk?.version ?? ''
	const sdkMajor = majorOf(sdk)
	if (sdkMajor) found.push({ source: 'global.json sdk.version', major: sdkMajor })
	else problems.push(`global.json has no sdk.version to read a major from (found "${sdk}").`)

	const frameworks = [...buildProps.matchAll(TARGET_FRAMEWORK)].map((match) => match[1])
	if (frameworks.length === 0) problems.push('Directory.Build.props declares no <TargetFramework>netN.0</TargetFramework>.')
	for (const major of frameworks) found.push({ source: 'Directory.Build.props TargetFramework', major })

	for (const [path, contents] of Object.entries(dockerfiles)) {
		for (const match of contents.matchAll(DOTNET_IMAGE)) found.push({ source: `${path} FROM`, major: match[1] })
	}

	const rule = (JSON.parse(renovateJson).packageRules ?? []).find(
		(candidate) => HELD_PACKAGES.every((name) => candidate.matchPackageNames?.includes(name)),
	)
	const allowed = rule?.allowedVersions?.match(ALLOWED_MAJOR)?.[1]
	if (!rule) problems.push(`renovate.json has no packageRule matching ${HELD_PACKAGES.join(' and ')} to hold the .NET major.`)
	else if (!allowed) problems.push(`renovate.json's .NET rule has allowedVersions "${rule.allowedVersions ?? ''}"; it must be "/^N\\\\./" for the current major N.`)
	else found.push({ source: 'renovate.json allowedVersions', major: allowed })

	return { found, problems }
}

/** Why the declared majors disagree, or an empty list. */
export function checkMajors(input) {
	const { found, problems } = declaredMajors(input)
	const majors = new Set(found.map((entry) => entry.major))
	if (majors.size > 1) {
		problems.push(
			`The .NET major is not the same everywhere: ${found.map((entry) => `${entry.source} = ${entry.major}`).join('; ')}. Move all of them in one pull request (ADR-0120).`,
		)
	}
	return problems
}

/** The four inputs, read from a checkout. */
export function readRepository(root) {
	const read = (path) => readFileSync(join(root, path), 'utf8')
	const dockerfiles = Object.fromEntries(
		execFileSync('git', ['-C', root, 'ls-files', '--', ':(glob)**/Dockerfile'], { encoding: 'utf8' })
			.split('\n')
			.filter(Boolean)
			.map((path) => [path, read(path)]),
	)
	return {
		globalJson: read('global.json'),
		buildProps: read('Directory.Build.props'),
		dockerfiles,
		renovateJson: read('renovate.json'),
	}
}

/** Reports the way the command line does, without exiting. Returns the exit code. */
export function main(root) {
	const problems = checkMajors(readRepository(root))
	if (problems.length === 0) {
		console.log('The .NET major agrees across global.json, the target framework, the Worker image, and renovate.json.')
		return 0
	}
	for (const problem of problems) console.error(`::error::${problem}`)
	return 1
}

const runAsCommand = String(process.argv[1]).endsWith('dotnet-major.mjs')
if (runAsCommand) process.exit(main(process.cwd()))
