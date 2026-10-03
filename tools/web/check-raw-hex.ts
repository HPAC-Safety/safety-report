#!/usr/bin/env node
// No raw hex colours in markup (ci.yml `web`, ADR-0024).
//
// Raw hex in markup defeats the token system: a literal colour cannot be
// re-themed, so it is the one thing that stays light when everything around it
// goes dark. Hex values belong in src/web/src/index.css, which is why only
// source markup/components are scanned here, not that file itself.
//
// CSS hex colours come in four valid lengths - #rgb, #rgba, #rrggbb,
// #rrggbbaa - and Tailwind arbitrary values (bg-[#f22312ff]) use the 4- and
// 8-digit alpha forms routinely. The alternation is ordered longest-first so
// grep reports the whole literal rather than a 6-digit prefix of an 8-digit
// one; POSIX ERE alternation is leftmost-longest at each position regardless
// of order, but the order keeps the intent readable.
//
// Known false positive, left as-is rather than fixed: a hex-shaped anchor
// fragment - href="#bad", href="#dead" - matches the 3/4-digit branch.
// Distinguishing "#bad" the fragment from "#bad" the colour needs the
// surrounding attribute context (href=, id=, a CSS declaration, a Tailwind
// bracket), which turns a one-line content lint into a small parser for one
// rare collision - a fragment id that also happens to be a valid hex string.
// Cheaper to rename the anchor than to carry that complexity in a grep gate.
import { annotation, exec as realExec, isMain, type Exec } from '../lib/actions.ts'

export const PATTERN = '#[0-9a-fA-F]{8}\\b|#[0-9a-fA-F]{6}\\b|#[0-9a-fA-F]{4}\\b|#[0-9a-fA-F]{3}\\b'
export const PATHS = ['src/web/src', 'src/web/index.html', 'src/web/theme-preview.html']
export const INCLUDES = ['*.tsx', '*.ts', '*.html']

/** The `file:line:text` matches, or '' for none. `grep` exits 1 for none; any failure counts as none. */
export function findHits(paths: readonly string[] = PATHS, exec: Exec = realExec): string {
	return exec('grep', ['-rInE', PATTERN, ...paths, ...INCLUDES.map((glob) => `--include=${glob}`)]).stdout
}

export interface MainOptions {
	argv?: readonly string[]
	exec?: Exec
	log?: (message: string) => void
}

export function main({ argv = process.argv.slice(2), exec = realExec, log = console.log }: MainOptions = {}): number {
	const hits = findHits(argv.length > 0 ? argv : PATHS, exec)
	if (hits === '') return 0
	log(annotation('error', 'Raw hex values belong in src/web/src/index.css. Use the @theme tokens in markup:'))
	log(hits)
	return 1
}

if (isMain(import.meta.url)) process.exit(main())
