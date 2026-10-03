#!/usr/bin/env node
// Smoke tests `/api/health` on the deployed site (deploy-environment.yml).
//
// site_urls is keyed by hostname (staging has exactly one - the CloudFront
// default address; production has both hpac.ca/acvl.ca hostnames) so any one
// entry's `public` URL reaches the same distribution and the same /api/health
// - the smoke test only needs one to answer.
//
// CloudFront's edge caches take a few seconds to pick up a fresh distribution
// config, so a short retry loop rather than one shot: ten tries, six seconds
// apart.
//
// Environment: PUBLIC_URL.
import { type Env, type Exec, exec as realExec, isMain } from '../../lib/actions.ts'

export const ATTEMPTS = 10
export const WAIT_SECONDS = 6

/** The health URL for a site URL: trailing slashes dropped, then `/api/health`. */
export function healthUrl(publicUrl: string): string {
	return `${publicUrl.replace(/\/*$/, '')}/api/health`
}

const realSleep = (seconds: number): void => {
	Atomics.wait(new Int32Array(new SharedArrayBuffer(4)), 0, 0, seconds * 1000)
}

export function main({
	env = process.env,
	exec = realExec,
	log = console.log,
	sleep = realSleep,
}: { env?: Env; exec?: Exec; log?: (line: string) => void; sleep?: (seconds: number) => void } = {}): number {
	const url = healthUrl(env.PUBLIC_URL ?? '')
	for (let attempt = 0; attempt < ATTEMPTS; attempt++) {
		const response = exec('curl', ['-fsS', '-o', '/dev/null', '-w', '%{http_code}', url])
		if (response.status === 0 && response.stdout === '200') {
			log(`Healthy: ${url}`)
			return 0
		}
		sleep(WAIT_SECONDS)
	}
	log(`::error::/api/health did not answer 200 at ${url}.`)
	return 1
}

if (isMain(import.meta.url)) process.exit(main())
