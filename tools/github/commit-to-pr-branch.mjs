#!/usr/bin/env node
// Commits a bot's generated files and pushes them onto a same-repo pull
// request's own branch (ADR-0021, ADR-0057, ADR-0101, ADR-0113).
//
// Shared by traceability.yml and i18n-translate.yml, which both commit onto the
// branch they were fired for. It sets the bot identity, puts the token on the
// remote URL, stages the files, commits, and pushes through
// tools/github/push-to-pr-branch.mjs, which decides what a rejected push means.
//
// A push made with the built-in GITHUB_TOKEN does not trigger workflows
// (GitHub's loop protection), so ci.yml would never re-run on the commit.
// TRANSLATION_PR_TOKEN, a fine-grained PAT scoped to this repository, is what
// makes the required checks re-run; without it the commit still lands, it just
// needs a manual nudge (an empty commit, or closing and reopening the PR). The
// run warns when it is pushing with the built-in token.
//
// Run it in the checkout that holds the generated files, from the trusted copy
// of the tools. The token is never printed: a failed git command reports its
// own stderr, not its arguments.
//
//   node tools/github/commit-to-pr-branch.mjs --adr <ADR-NNNN> --add <file,...>
//     [--add-if-exists <file,...>] --subject <message> [--body-env <NAME>]
//     --paths <pattern,...>
//
// --body-env names a variable whose value, plus a full stop, is the second -m.
// --paths is the workflow's `pull_request_target.paths`, verbatim;
// tests/js/github/push-to-pr-branch.test.mjs fails if they disagree.
//
// Environment: GH_TOKEN, USING_DEFAULT_TOKEN, HEAD_REF, HEAD_SHA,
// GITHUB_REPOSITORY.
import { existsSync } from 'node:fs'
import path from 'node:path'

import { exec, isMain, required } from '../lib/actions.mjs'
import { main as push } from './push-to-pr-branch.mjs'

export const BOT_NAME = 'github-actions[bot]'
export const BOT_EMAIL = '41898282+github-actions[bot]@users.noreply.github.com'

/** Parses `--name value` pairs. */
export function parseArgs(argv) {
	const options = {}
	for (let i = 0; i < argv.length; i += 2) options[argv[i].replace(/^--/, '')] = argv[i + 1]
	return options
}

const list = (value) => (value ?? '').split(',').map((item) => item.trim()).filter(Boolean)

export function warning(adr) {
	return `::warning::No TRANSLATION_PR_TOKEN is set, so this commit is pushed with the built-in GITHUB_TOKEN and CI will not re-run on it. Push an empty commit, or close and reopen the PR, before merging. See ${adr}.`
}

export function main({ argv = process.argv.slice(2), env = process.env, exec: run = exec, log = console.log, push: pushBranch = push, cwd = process.cwd(), exists = existsSync } = {}) {
	const options = parseArgs(argv)
	if (!options.adr || !options.add || !options.subject || !options.paths) {
		log('::error::Usage: node tools/github/commit-to-pr-branch.mjs --adr <ADR> --add <file,...> --subject <message> --paths <pattern,...>')
		return 1
	}
	const token = required(env, 'GH_TOKEN')
	const repository = required(env, 'GITHUB_REPOSITORY')

	const git = (...args) => {
		const result = run('git', args, { cwd })
		if (result.status !== 0) throw new Error(`git ${args[0]} failed${result.stderr ? `: ${result.stderr}` : ''}`)
	}

	try {
		if (env.USING_DEFAULT_TOKEN === 'true') log(warning(options.adr))

		git('config', 'user.name', BOT_NAME)
		git('config', 'user.email', BOT_EMAIL)
		git('remote', 'set-url', 'origin', `https://x-access-token:${token}@github.com/${repository}.git`)

		git('add', ...list(options.add))
		for (const file of list(options['add-if-exists'])) {
			if (exists(path.join(cwd, file))) git('add', file)
		}
		const body = options['body-env'] ? ['-m', `${env[options['body-env']] ?? ''}.`] : []
		git('commit', '-m', options.subject, ...body)
	} catch (error) {
		log(`::error::${error.message}`)
		return 1
	}

	return pushBranch(['--branch', required(env, 'HEAD_REF'), '--event-sha', required(env, 'HEAD_SHA'), '--paths', options.paths], cwd)
}

if (isMain(import.meta.url)) process.exit(main())
