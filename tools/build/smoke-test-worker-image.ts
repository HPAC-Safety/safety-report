#!/usr/bin/env node
// Builds the Worker image and proves ffmpeg runs inside it (ci.yml `build`).
//
// The Worker image is the one deployed (ADR-0118), built by the same script
// dev-up.sh and deploy-worker.yml use. What is proven here is that ffmpeg and
// ffprobe are on the image's PATH, where FfmpegVideoRemuxer looks, and that
// the Worker runs unprivileged. The remux itself is proven by
// FfmpegVideoRemuxerTests against the runner's Ubuntu ffmpeg, the same archive
// the image installs from.
//
//   node tools/build/smoke-test-worker-image.ts <image-tag>
import { annotation, exec as realExec, isMain, type Exec } from '../lib/actions.ts'

const BUILD_SCRIPT = 'tools/build/build-worker-image.sh'

/** The `docker run` probes, in the order they run; each must exit 0. */
export function probes(image: string): string[][] {
	const commands: [string, ...string[]][] = [
		['ffmpeg', '-hide_banner', '-version'],
		['ffprobe', '-hide_banner', '-version'],
		['dotnet', '--list-runtimes'],
	]
	return commands.map(([entrypoint, ...args]) => ['run', '--rm', '--entrypoint', entrypoint, image, ...args])
}

export interface MainOptions {
	argv?: readonly string[]
	exec?: Exec
	log?: (line: string) => void
}

export function main({ argv = process.argv.slice(2), exec = realExec, log = console.log }: MainOptions = {}): number {
	const image = argv.at(0)
	if (image === undefined || image === '') {
		log(annotation('error', 'An image tag is required.'))
		return 2
	}
	exec(BUILD_SCRIPT, [image], { inherit: true, check: true })
	for (const args of probes(image)) exec('docker', args, { inherit: true, check: true })
	const user = exec('docker', ['run', '--rm', '--entrypoint', 'id', image, '-u'])
	if (user.stdout === '0') {
		log(annotation('error', 'The Worker image runs as root.'))
		return 1
	}
	return 0
}

if (isMain(import.meta.url)) process.exit(main())
