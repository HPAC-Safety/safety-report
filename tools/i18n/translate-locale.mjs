#!/usr/bin/env node
// Temporary (#800): main's i18n-translate.yml runs on pull_request_target from
// the base branch and still calls this .mjs path against the pull request that
// converted tools/ to TypeScript (#798). Delete once that pull request has
// merged and main's workflow calls tools/i18n/translate-locale.ts.
//
// It re-runs the TypeScript script with the same arguments, environment and
// standard streams, and exits with its status. Importing would not run it: the
// script's command guard matches only its own path.
import { spawnSync } from 'node:child_process'
import { fileURLToPath } from 'node:url'

const script = fileURLToPath(new URL('./translate-locale.ts', import.meta.url))
const run = spawnSync(process.execPath, [script, ...process.argv.slice(2)], { stdio: 'inherit' })
process.exit(run.status ?? 1)
