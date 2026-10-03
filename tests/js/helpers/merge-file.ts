import { mkdtempSync, writeFileSync } from 'node:fs'
import { spawnSync } from 'node:child_process'
import { tmpdir } from 'node:os'
import { join } from 'node:path'

/** What `git merge-file -p` reports: the merged text and its exit status (the conflict count, `null` when killed). */
export interface MergeResult {
	conflicts: number | null
	merged: string
}

/**
 * Three-way merges `ours` and `theirs` against `base` the way git merges a
 * file, so a generated file can be shown to merge cleanly (ADR-0106).
 */
export function mergeFile(base: string, ours: string, theirs: string): MergeResult {
	const dir = mkdtempSync(join(tmpdir(), 'merge-file-'))
	const files = Object.entries({ base, ours, theirs }).map(([name, content]) => {
		writeFileSync(join(dir, name), content)
		return join(dir, name)
	})
	const result = spawnSync('git', ['merge-file', '-p', files[1], files[0], files[2]], { encoding: 'utf8' })
	return { conflicts: result.status, merged: result.stdout }
}
