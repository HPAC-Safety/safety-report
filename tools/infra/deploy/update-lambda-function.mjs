#!/usr/bin/env node
// Points a Lambda function at an image digest and waits for the update to
// finish (deploy-environment.yml; the API and the Worker each run it once).
//
// Environment: FUNCTION_NAME, IMAGE_URI (`repo@sha256:...`).
import { exec as realExec, isMain, required } from '../../lib/actions.mjs'

export function main({ env = process.env, exec = realExec } = {}) {
	const name = required(env, 'FUNCTION_NAME')
	const imageUri = required(env, 'IMAGE_URI')
	exec('aws', ['lambda', 'update-function-code', '--function-name', name, '--image-uri', imageUri], { check: true })
	exec('aws', ['lambda', 'wait', 'function-updated', '--function-name', name], { check: true, inherit: true })
	return 0
}

if (isMain(import.meta.url)) process.exit(main())
