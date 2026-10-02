#!/usr/bin/env node
// Fails with a clear, named error when a deploy-time secret or variable is
// missing, instead of failing deep inside an AWS CLI call with an opaque one.
// The body of .github/actions/require-config (issue #36).
//
// Inputs, through the environment: REQUIRED_SECRETS and REQUIRED_VARIABLES are
// newline-separated NAME=value pairs the caller interpolated from its secrets
// and vars contexts; FAILURE_CONTEXT is the sentence appended to each error.
// Only the NAME is ever printed.
//
// Every missing value is reported before the script exits, not just the first.
import { isMain } from '../lib/actions.mjs'

/** The problems in one newline-separated block of NAME=value lines. */
export function checkBlock(kind, block, context) {
	const messages = []
	for (const line of block.split('\n')) {
		if (line.replace(/\s/g, '') === '') continue
		const equals = line.indexOf('=')
		// A line with no '=' is a malformed call, not a missing value.
		if (equals === -1) {
			messages.push(`::error::require-config: malformed ${kind} entry '${line}' (expected NAME=value).`)
			continue
		}
		const name = line.slice(0, equals)
		const value = line.slice(equals + 1)
		if (value === '') messages.push(`::error::Missing ${kind} '${name}'. ${context}`)
	}
	return messages
}

export function main({ env = process.env, log = console.log } = {}) {
	const context = env.FAILURE_CONTEXT ?? ''
	const messages = [
		...checkBlock('secret', env.REQUIRED_SECRETS ?? '', context),
		...checkBlock('variable', env.REQUIRED_VARIABLES ?? '', context),
	]
	for (const message of messages) log(message)

	if (messages.length > 0) {
		log('')
		log('The AWS environment has not been created yet. It is built by')
		log('#32 (bootstrap) and wired up by #30 (live deployment).')
		return 1
	}
	log('All required configuration is present.')
	return 0
}

if (isMain(import.meta.url)) process.exit(main())
