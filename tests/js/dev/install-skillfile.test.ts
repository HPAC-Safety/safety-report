import { describe, it } from 'node:test'
import assert from 'node:assert/strict'
import { mkdtempSync, readFileSync, rmSync, statSync, writeFileSync } from 'node:fs'
import { tmpdir } from 'node:os'
import { join } from 'node:path'
import { spawnSync } from 'node:child_process'
import { fileURLToPath } from 'node:url'

import type { Exec } from '../../../tools/lib/actions.ts'
import { main } from '../../../tools/dev/install-skillfile.ts'

describe('install-skillfile', () => {
	it('downloads the pinned release into RUNNER_TEMP, makes it executable, and puts it on the path', () => {
		const dir = mkdtempSync(join(tmpdir(), 'install-skillfile-'))
		const pathFile = join(dir, 'GITHUB_PATH')
		const calls: string[][] = []
		const exec: Exec = (command, args = []) => {
			calls.push([command, ...args])
			writeFileSync(join(dir, 'skillfile'), '#!/bin/sh')
			return { status: 0, stdout: '', stderr: '' }
		}
		assert.equal(main({ env: { RUNNER_TEMP: dir, GITHUB_PATH: pathFile }, exec }), 0)
		assert.deepEqual(calls, [['gh', 'release', 'download', 'v1.9.1', '--repo', 'eljulians/skillfile', '--pattern', 'skillfile-x86_64-linux', '--output', `${dir}/skillfile`]])
		assert.ok(statSync(join(dir, 'skillfile')).mode & 0o100)
		assert.equal(readFileSync(pathFile, 'utf8'), `${dir}\n`)
		rmSync(dir, { recursive: true })
	})

	it('stops before touching the path when the download fails', () => {
		const exec: Exec = () => {
			throw new Error('gh exited 1')
		}
		assert.throws(() => main({ env: { RUNNER_TEMP: '/t', GITHUB_PATH: '/p' }, exec, appendPath: () => assert.fail('appended') }), /gh exited 1/)
	})

	it('needs RUNNER_TEMP', () => {
		assert.throws(() => main({ env: {}, exec: () => assert.fail('ran') }), /RUNNER_TEMP is not set/)
	})
})

describe('install-skillfile as a command', () => {
	it('fails without RUNNER_TEMP', () => {
		const script = fileURLToPath(new URL('../../../tools/dev/install-skillfile.ts', import.meta.url))
		const result = spawnSync(process.execPath, [script], { encoding: 'utf8', env: { PATH: process.env.PATH } })
		assert.notEqual(result.status, 0)
		assert.match(result.stderr, /RUNNER_TEMP is not set/)
	})
})
