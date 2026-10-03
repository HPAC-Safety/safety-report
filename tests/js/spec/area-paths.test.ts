import { describe, it } from 'node:test'
import assert from 'node:assert/strict'
import { mkdirSync, mkdtempSync, writeFileSync } from 'node:fs'
import { tmpdir } from 'node:os'
import { join } from 'node:path'

import { type AreaClaim, type AreaPaths, areaOfFeature, areasOf, parseAreaPaths, validate } from '../../../tools/spec/area-paths.ts'
import { featureAreas, main } from '../../../tools/spec/check-area-paths.ts'
import type { Exec } from '../../../tools/lib/actions.ts'

const MAP: AreaPaths = { every: ['src/Program.cs'], areas: { media: ['src/Media/**', 'src/web/src/components/Attachment*'], comments: ['src/Comments/**'] } }

const CLAIMS: AreaClaim[] = [
	{ id: 'REQ-MED-001', area: 'media', engine: 'Reqnroll', stepFiles: ['tests/HpacSafety.Acceptance.Tests/MediaSteps.cs'] },
	{ id: 'REQ-COM-001', area: 'comments', engine: 'playwright-bdd', stepFiles: ['tests/e2e/steps/comments.steps.ts'] },
]

const sorted = (areas: Set<string>): string[] => [...areas].sort()

describe('parseAreaPaths', () => {
	it('reads a map', () => {
		assert.deepEqual(parseAreaPaths(JSON.stringify(MAP)), MAP)
	})

	it('says why a value is not a map', () => {
		assert.match(parseAreaPaths('{') as string, /is not JSON/)
		assert.equal(parseAreaPaths('[]'), 'is not an object')
		assert.equal(parseAreaPaths('{"every":[],"areas":{},"x":1}'), 'has keys it does not define: x')
		assert.equal(parseAreaPaths('{"every":[""],"areas":{}}'), '"every" is not a list of globs')
		assert.equal(parseAreaPaths('{"every":[],"areas":[]}'), '"areas" is not an object')
		assert.equal(parseAreaPaths('{"every":[],"areas":{"media":"src/**"}}'), '"areas.media" is not a list of globs')
	})
})

describe('areasOf', () => {
	it('maps a behavior file by its globs, a * staying inside one directory', () => {
		assert.deepEqual(sorted(areasOf('src/Media/Sniffer/Chain.cs', MAP, CLAIMS)), ['media'])
		assert.deepEqual(sorted(areasOf('src/web/src/components/AttachmentStrip.tsx', MAP, CLAIMS)), ['media'])
		assert.deepEqual(sorted(areasOf('src/web/src/components/Attachment/Strip.tsx', MAP, CLAIMS)), [])
	})

	it('maps a file under every to every area', () => {
		assert.deepEqual(sorted(areasOf('src/Program.cs', MAP, CLAIMS)), ['comments', 'media'])
	})

	it('maps a step definition by the claims it binds', () => {
		assert.deepEqual(sorted(areasOf('tests/e2e/steps/comments.steps.ts', MAP, CLAIMS)), ['comments'])
		assert.deepEqual(sorted(areasOf('tests/HpacSafety.Acceptance.Tests/MediaSteps.cs', MAP, CLAIMS)), ['media'])
	})

	it('maps a step-directory helper that binds nothing to every area its engine serves', () => {
		assert.deepEqual(sorted(areasOf('tests/e2e/steps/auth.ts', MAP, CLAIMS)), ['comments'])
		assert.deepEqual(sorted(areasOf('tests/HpacSafety.Acceptance.Tests/FixedClock.cs', MAP, CLAIMS)), ['media'])
	})
})

describe('validate', () => {
	const tree = { files: ['src/Program.cs', 'src/Media/A.cs', 'src/web/src/components/AttachmentStrip.tsx', 'src/Comments/B.cs'], areas: ['comments', 'media'] }

	it('accepts a map that covers the tree with live globs', () => {
		assert.deepEqual(validate(MAP, tree), [])
	})

	it('fails an unmapped file, a dead glob, and an area with no feature file', () => {
		const map: AreaPaths = { every: ['src/Program.cs'], areas: { media: ['src/Media/**', 'src/Gone/**'], typo: ['src/Comments/**'] } }
		assert.deepEqual(validate(map, { files: [...tree.files, 'src/New.cs'], areas: tree.areas }), [
			'"typo" is not a feature area; the areas are comments, media',
			'media: "src/Gone/**" matches no behavior-bearing file',
			'src/web/src/components/AttachmentStrip.tsx belongs to no area; add it to the area whose scenarios describe it, or to "every"',
			'src/New.cs belongs to no area; add it to the area whose scenarios describe it, or to "every"',
		])
	})
})

describe('areaOfFeature', () => {
	it('reads the area directory', () => {
		assert.equal(areaOfFeature('.spec/features', '.spec/features/media/media.feature'), 'media')
	})
})

describe('check-area-paths', () => {
	const tree = (map: unknown): string => {
		const root = mkdtempSync(join(tmpdir(), 'area-paths-'))
		mkdirSync(join(root, '.spec/features/media'), { recursive: true })
		mkdirSync(join(root, '.spec/features/notes'), { recursive: true })
		writeFileSync(join(root, '.spec/features/media/media.feature'), 'Feature: Media\n')
		writeFileSync(join(root, '.spec/area-paths.json'), JSON.stringify(map))
		return root
	}
	const git: Exec = () => ({ status: 0, stdout: 'src/Media/A.cs\nsrc/README.md\ntests/e2e/steps/a.ts\n', stderr: '' })

	it('lists only directories that hold their own feature file as areas', () => {
		assert.deepEqual(featureAreas(tree({})), ['media'])
	})

	it('passes a map that covers every behavior-bearing file', () => {
		const output: string[] = []
		assert.equal(main({ root: tree({ every: [], areas: { media: ['src/Media/**'] } }), exec: git, log: (line) => output.push(line) }), 0)
		assert.match(output.join('\n'), /1 behavior-bearing files, each in at least one of 1 areas/)
	})

	it('fails with every problem as an annotation on the map', () => {
		const output: string[] = []
		assert.equal(main({ root: tree({ every: [], areas: { notes: ['src/Notes/**'] } }), exec: git, log: (line) => output.push(line) }), 1)
		assert.match(output.join('\n'), /::error file=\.spec\/area-paths\.json::"notes" is not a feature area/)
		assert.match(output.join('\n'), /src\/Media\/A\.cs belongs to no area/)
	})

	it('fails a map that is not one', () => {
		const output: string[] = []
		assert.equal(main({ root: tree([]), exec: git, log: (line) => output.push(line) }), 1)
		assert.match(output.join('\n'), /is not an object/)
	})
})

describe('the real map', () => {
	it('covers every behavior-bearing file in the tree', () => {
		const output: string[] = []
		assert.equal(main({ log: (line) => output.push(line) }), 0, output.join('\n'))
	})
})
