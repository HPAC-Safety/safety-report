import { describe, it } from 'node:test'
import assert from 'node:assert/strict'
import { execFileSync } from 'node:child_process'
import { mkdirSync, mkdtempSync, rmSync, writeFileSync } from 'node:fs'
import { tmpdir } from 'node:os'
import { join } from 'node:path'

import { HELD_PACKAGES, checkMajors, declaredMajors, main } from '../../tools/dotnet-major.mjs'

const globalJson = (version) => JSON.stringify({ sdk: { version, rollForward: 'latestFeature' } })
const buildProps = (framework) => `<Project><PropertyGroup><TargetFramework>${framework}</TargetFramework></PropertyGroup></Project>`
const dockerfile = (tag) => `# The Worker image.\nFROM mcr.microsoft.com/dotnet/runtime:${tag}@sha256:abc\nRUN true\n`
const renovateJson = (allowedVersions, matchPackageNames = HELD_PACKAGES) =>
	JSON.stringify({ packageRules: [{ groupName: 'Other', matchPackageNames: ['vite'] }, { matchPackageNames, allowedVersions }] })

/** A repository whose four declarations agree on .NET 10, with any of them overridden. */
const repository = (overrides = {}) => ({
	globalJson: globalJson('10.0.401'),
	buildProps: buildProps('net10.0'),
	dockerfiles: { 'src/HpacSafety.Worker/Dockerfile': dockerfile('10.0') },
	renovateJson: renovateJson('/^10\\./'),
	...overrides,
})

/** Runs `main` with console output captured, restoring it afterwards even on failure. */
function runMain(root) {
	const output = { log: [], error: [] }
	const original = { log: console.log, error: console.error }
	console.log = (...args) => output.log.push(args.join(' '))
	console.error = (...args) => output.error.push(args.join(' '))
	try {
		return { code: main(root), output }
	} finally {
		console.log = original.log
		console.error = original.error
	}
}

describe('declaredMajors', () => {
	it('reads the major from every place it is declared', () => {
		const { found, problems } = declaredMajors(repository())

		assert.deepEqual(problems, [])
		assert.deepEqual(
			found.map((entry) => entry.source),
			['global.json sdk.version', 'Directory.Build.props TargetFramework', 'src/HpacSafety.Worker/Dockerfile FROM', 'renovate.json allowedVersions'],
		)
		assert.ok(found.every((entry) => entry.major === '10'))
	})

	it('ignores a base image that is not a .NET image', () => {
		const { found } = declaredMajors(repository({ dockerfiles: { 'Dockerfile': 'FROM ubuntu:24.04\n' } }))

		assert.ok(!found.some((entry) => entry.source.includes('Dockerfile')))
	})
})

describe('checkMajors', () => {
	it('passes when every declaration agrees', () => {
		assert.deepEqual(checkMajors(repository()), [])
	})

	it('fails a runtime image a major ahead of the target framework', () => {
		const problems = checkMajors(repository({ dockerfiles: { 'src/HpacSafety.Worker/Dockerfile': dockerfile('11.0') } }))

		assert.equal(problems.length, 1)
		assert.match(problems[0], /Dockerfile FROM = 11/)
		assert.match(problems[0], /TargetFramework = 10/)
	})

	it('fails a target framework moved without the SDK', () => {
		assert.match(checkMajors(repository({ buildProps: buildProps('net11.0') }))[0], /global.json sdk.version = 10/)
	})

	it('fails a Renovate allowance left behind by an upgrade', () => {
		const upgraded = repository({
			globalJson: globalJson('11.0.100'),
			buildProps: buildProps('net11.0'),
			dockerfiles: { 'src/HpacSafety.Worker/Dockerfile': dockerfile('11.0') },
		})

		assert.match(checkMajors(upgraded)[0], /renovate.json allowedVersions = 10/)
	})

	it('fails when Renovate no longer holds the major', () => {
		const problems = checkMajors(repository({ renovateJson: renovateJson(undefined, ['vite']) }))

		assert.match(problems[0], /no packageRule matching dotnet-sdk and mcr.microsoft.com\/dotnet/)
	})

	it('fails an allowance that is not the one anchored major shape', () => {
		assert.match(checkMajors(repository({ renovateJson: renovateJson('<11') }))[0], /allowedVersions "<11"/)
	})

	it('fails a global.json with no SDK version', () => {
		assert.match(checkMajors(repository({ globalJson: '{}' }))[0], /no sdk.version/)
	})

	it('fails a build props file with no target framework', () => {
		assert.match(checkMajors(repository({ buildProps: '<Project />' }))[0], /declares no <TargetFramework>/)
	})
})

describe('main', () => {
	it('passes on this repository', () => {
		const { code, output } = runMain(process.cwd())

		assert.equal(code, 0, output.error.join('\n'))
		assert.match(output.log[0], /agrees/)
	})

	it('fails a checkout whose Worker image is a major ahead', () => {
		const root = mkdtempSync(join(tmpdir(), 'dotnet-major-'))
		try {
			const files = repository()
			writeFileSync(join(root, 'global.json'), files.globalJson)
			writeFileSync(join(root, 'Directory.Build.props'), files.buildProps)
			writeFileSync(join(root, 'renovate.json'), files.renovateJson)
			mkdirSync(join(root, 'src/HpacSafety.Worker'), { recursive: true })
			writeFileSync(join(root, 'src/HpacSafety.Worker/Dockerfile'), dockerfile('11.0'))
			execFileSync('git', ['-C', root, 'init', '--quiet'])
			execFileSync('git', ['-C', root, 'add', '-A'])

			const { code, output } = runMain(root)

			assert.equal(code, 1)
			assert.match(output.error[0], /^::error::The .NET major is not the same everywhere/)
		} finally {
			rmSync(root, { recursive: true, force: true })
		}
	})
})
