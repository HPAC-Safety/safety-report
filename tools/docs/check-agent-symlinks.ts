#!/usr/bin/env node
// The agent instruction files are symlinks to AGENTS.md (ci.yml `agent-config`).
//
// CLAUDE.md, .github/copilot-instructions.md, and .cursor/rules/agents.mdc must
// each be a symlink that resolves to AGENTS.md. Symlinks only survive checkout
// when core.symlinks is on, which it is by default on Linux runners; if that
// ever changes, this is what notices.
//
//   node tools/docs/check-agent-symlinks.ts
import { lstatSync, realpathSync, type Stats } from 'node:fs'
import path from 'node:path'
import { annotation, isMain } from '../lib/actions.ts'

export const CANONICAL = 'AGENTS.md'
export const LINKS = ['CLAUDE.md', '.github/copilot-instructions.md', '.cursor/rules/agents.mdc']

/** The annotations for every link that is not a symlink to AGENTS.md, in order. */
export interface SymlinkChecks {
	links?: readonly string[]
	lstat?: (file: string) => Pick<Stats, 'isSymbolicLink'>
	realpath?: (file: string) => string
}

export function problems(root: string, { links = LINKS, lstat = lstatSync, realpath = realpathSync }: SymlinkChecks = {}): string[] {
	const resolve = (file: string): string | null => {
		try {
			return realpath(path.join(root, file))
		} catch {
			return null
		}
	}
	const canonical = resolve(CANONICAL) ?? path.join(root, CANONICAL)
	const found: string[] = []
	for (const link of links) {
		let isLink = false
		try {
			isLink = lstat(path.join(root, link)).isSymbolicLink()
		} catch {
			// A missing file is not a symlink either.
		}
		if (!isLink) {
			found.push(annotation('error', `Not a symlink. It must point at ${CANONICAL}.`, { file: link }))
			continue
		}
		const target = resolve(link)
		if (target !== canonical) {
			found.push(annotation('error', `Resolves to '${target ?? '<broken>'}', expected '${canonical}'.`, { file: link }))
		}
	}
	return found
}

export function main({ root = process.cwd(), log = console.log }: { root?: string; log?: (line: string) => void } = {}): number {
	const found = problems(root)
	for (const line of found) log(line)
	return found.length === 0 ? 0 : 1
}

if (isMain(import.meta.url)) process.exit(main())
