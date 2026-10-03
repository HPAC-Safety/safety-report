#!/usr/bin/env node
// Nothing in the built bundle reaches Google for a font (ci.yml `web`).
//
// The acceptance criterion from #11, checked rather than trusted. The
// @font-face URLs in src/web/src/index.css are relative paths into
// src/web/assets/fonts, so a Google Fonts hostname in src/web/dist means a
// stylesheet or a dependency pulled one in.
//
//   node tools/web/check-no-third-party-fonts.ts [dist-dir]
import { annotation, exec as realExec, isMain, type Exec } from '../lib/actions.ts'

export const HOSTS = ['fonts.googleapis.com', 'fonts.gstatic.com']

/** The files under `dist` that name a Google Fonts host. `grep` exits 1 for none; any failure counts as none. */
export function findHits(dist: string, exec: Exec = realExec): string {
	return exec('grep', ['-rIl', ...HOSTS.flatMap((host) => ['-e', host]), dist]).stdout
}

export interface MainOptions {
	argv?: readonly string[]
	exec?: Exec
	log?: (message: string) => void
}

export function main({ argv = process.argv.slice(2), exec = realExec, log = console.log }: MainOptions = {}): number {
	const hits = findHits(argv[0] ?? 'src/web/dist', exec)
	if (hits === '') return 0
	log(annotation('error', 'Fonts must be self-hosted from src/web/assets/fonts. Found a Google Fonts reference in:'))
	log(hits)
	return 1
}

if (isMain(import.meta.url)) process.exit(main())
