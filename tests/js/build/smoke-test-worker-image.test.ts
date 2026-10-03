import { describe, it } from 'node:test'
import assert from 'node:assert/strict'

import type { Exec } from '../../../tools/lib/actions.ts'
import { main, probes } from '../../../tools/build/smoke-test-worker-image.ts'

const recorder = (idStdout = '1000') => {
	const calls: string[][] = []
	const exec: Exec = (command, args = []) => {
		calls.push([command, ...args])
		return { status: 0, stdout: args.includes('id') ? idStdout : '', stderr: '' }
	}
	return { calls, exec }
}

describe('smoke-test-worker-image', () => {
	it('builds the image, then probes ffmpeg, ffprobe, and the runtimes, then checks the user', () => {
		const { calls, exec } = recorder()
		const logs: string[] = []
		assert.equal(main({ argv: ['img:ci'], exec, log: (m) => logs.push(m) }), 0)
		assert.deepEqual(calls[0], ['tools/build/build-worker-image.sh', 'img:ci'])
		assert.deepEqual(calls.slice(1, 4), probes('img:ci').map((args) => ['docker', ...args]))
		assert.deepEqual(calls[4], ['docker', 'run', '--rm', '--entrypoint', 'id', 'img:ci', '-u'])
		assert.deepEqual(logs, [])
	})

	it('fails with the annotation when the image runs as root', () => {
		const { exec } = recorder('0')
		const logs: string[] = []
		assert.equal(main({ argv: ['img:ci'], exec, log: (m) => logs.push(m) }), 1)
		assert.deepEqual(logs, ['::error::The Worker image runs as root.'])
	})

	it('needs an image tag', () => {
		const { calls, exec } = recorder()
		assert.equal(main({ argv: [], exec, log: () => undefined }), 2)
		assert.deepEqual(calls, [])
	})

	it('lets a failing probe throw, as set -e would stop the step', () => {
		const exec: Exec = () => {
			throw new Error('docker exited 1')
		}
		assert.throws(() => main({ argv: ['img'], exec, log: () => undefined }), /docker exited 1/)
	})
})
