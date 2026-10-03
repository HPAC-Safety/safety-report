#!/usr/bin/env node
// Reads the pinned Node major out of a workflow file and outputs it as `major`.
//
// The Node major is pinned in ci.yml and nowhere else (ADR-0015, and
// skills/deliver-hpac-change/SKILL.md). Reading it rather than writing 24 again
// keeps traceability.yml and i18n-translate.yml from silently disagreeing with
// CI. They run it from the trusted checkout (the base branch in
// traceability.yml), before actions/setup-node.
//
//   node tools/github/read-node-major.ts <workflow-file>
//
// The first `node-version: <digits>` in the file wins. Fails, naming the file,
// when there is none.
import { readFileSync } from 'node:fs'

import { isMain, setOutput, type Env } from '../lib/actions.ts'

/** The major in the first `node-version: N` line of the text, or null. */
export function readNodeMajor(text: string): string | null {
	return text.match(/node-version: ([0-9]+)/)?.[1] ?? null
}

export function main({ argv = process.argv.slice(2), env = process.env, log = console.log }: { argv?: readonly string[]; env?: Env; log?: (line: string) => void } = {}): number {
	const file = argv.at(0)
	if (!file) {
		log('::error::Usage: node tools/github/read-node-major.ts <workflow-file>')
		return 1
	}
	const major = readNodeMajor(readFileSync(file, 'utf8'))
	if (!major) {
		log(`::error::Could not read the pinned Node major out of ${file}.`)
		return 1
	}
	setOutput('major', major, env)
	return 0
}

if (isMain(import.meta.url)) process.exit(main())
