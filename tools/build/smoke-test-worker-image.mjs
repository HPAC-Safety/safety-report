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
//   node tools/build/smoke-test-worker-image.mjs <image-tag>
import { annotation, exec as realExec, isMain } from '../lib/actions.mjs'

const BUILD_SCRIPT = 'tools/build/build-worker-image.sh'

/** The `docker run` probes, in the order they run; each must exit 0. */
export function probes(image) {
	return [
		['ffmpeg', '-hide_banner', '-version'],
		['ffprobe', '-hide_banner', '-version'],
		['dotnet', '--list-runtimes'],
	].map(([entrypoint, ...args]) => ['run', '--rm', '--entrypoint', entrypoint, image, ...args])
}

export function main({ argv = process.argv.slice(2), exec = realExec, log = console.log } = {}) {
	const image = argv[0]
	if (!image) {
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
