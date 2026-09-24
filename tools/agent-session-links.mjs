#!/usr/bin/env node
// Refuses text that carries a coding agent's session link (ADR-0107).
//
// The repository is public, a pull-request body becomes the squash commit
// message, and a session link opens a transcript that can carry what the
// public history must not. Agents add these links by default, so the rule runs
// where the text is written: the commit-msg hook for a commit message, and the
// "Linked issue" workflow for a pull-request body.
//
//   node tools/agent-session-links.mjs <file>   checks a file (a commit message)
//   PR_BODY=… node tools/agent-session-links.mjs checks the environment variable
//
// The exit code is the contract.
import { readFileSync } from 'node:fs'

// One entry per agent whose session links are refused. Supporting another agent
// is one more entry here, with the URL shape its sessions actually use.
export const SESSION_LINKS = [
	{ agent: 'Claude', pattern: /https?:\/\/(?:www\.)?claude\.ai\/code\/session_[A-Za-z0-9_-]+/gi },
]

/** Every session link in the text, with the agent it belongs to. */
export function findSessionLinks(text, links = SESSION_LINKS) {
	return links.flatMap(({ agent, pattern }) => [...text.matchAll(pattern)].map((match) => ({ agent, link: match[0] })))
}

/** Reports every session link found the way the command line does. Returns the exit code. */
export function main(text, source) {
	const found = findSessionLinks(text)
	if (found.length === 0) return 0

	for (const { agent, link } of found) console.error(`::error::${source} carries a ${agent} session link: ${link}`)
	console.error('')
	console.error('Remove it. This repository is public, and a session link must never')
	console.error('reach its history (ADR-0107).')
	return 1
}

const runAsCommand = String(process.argv[1]).endsWith('agent-session-links.mjs')
if (runAsCommand) {
	const file = process.argv[2]
	process.exit(file ? main(readFileSync(file, 'utf8'), 'The commit message') : main(process.env.PR_BODY ?? '', 'The pull-request body'))
}
