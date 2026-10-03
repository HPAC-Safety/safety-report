#!/usr/bin/env node
// Reads the main baseline tools/dev/ci-local.sh fetched, under act (ci.yml
// `coverage`, ADR-0145).
//
// Under act the job has no token, and GitHub answers an anonymous artifact
// download with 401 even on a public repository. So the wrapper downloads the
// same run's coverage-report on the host, with the developer's gh login, into
// the gitignored .ci-local/baseline/, and this reads it. Permanent: act never
// receives a token, and the wrapper fails a run whose job reports holding one,
// which is what the `ci-local:github-token=` line is for.
//
// Outputs `path` and `run-id`, both `none` when the wrapper fetched nothing.
// Reads JOB_TOKEN, the job's github.token, only to say whether it is empty.
import { existsSync, readFileSync } from 'node:fs'
import path from 'node:path'
import { type Env, annotation, isMain, setOutput } from '../lib/actions.ts'

export const DIR = '.ci-local/baseline'
const DEFAULT_NOTICE = 'No baseline from tools/dev/ci-local.sh. Ratchet skipped; the floor still applies.'

/** `$(cat file)`: the file's text without trailing newlines, or null when it cannot be read. */
function readText(file: string): string | null {
	try {
		return readFileSync(file, 'utf8').replace(/\n+$/, '')
	} catch {
		return null
	}
}

export interface MainOptions {
	env?: Env
	log?: (line: string) => void
	dir?: string
}

export function main({ env = process.env, log = console.log, dir = DIR }: MainOptions = {}): number {
	log(`ci-local:github-token=${env.JOB_TOKEN ? 'present' : 'empty'}`)
	if (existsSync(path.join(dir, 'Cobertura.xml'))) {
		const runId = readText(path.join(dir, 'run-id')) ?? ''
		log(`Baseline from main run ${runId}.`)
		setOutput('path', './.ci-local/baseline/Cobertura.xml', env)
		setOutput('run-id', runId, env)
		return 0
	}
	log(annotation('notice', readText(path.join(dir, 'notice')) ?? DEFAULT_NOTICE))
	setOutput('path', 'none', env)
	setOutput('run-id', 'none', env)
	return 0
}

if (isMain(import.meta.url)) process.exit(main())
