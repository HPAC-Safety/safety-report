import { describe, it } from 'node:test'
import assert from 'node:assert/strict'
import { mkdirSync, mkdtempSync, rmSync, writeFileSync } from 'node:fs'
import { tmpdir } from 'node:os'
import { join } from 'node:path'

import { coverageTags, findFiles, packageNames, render } from '../../../tools/coverage/print-ci-local-coverage.ts'

const report = (packages: string[]): string => `<coverage line-rate="1">\n<packages>${packages.map((p) => `<package name="${p}">`).join('')}</packages></coverage>`

describe('print-ci-local-coverage', () => {
	it('reads the coverage tags and the sorted package names from a report', () => {
		const xml = report(['B', 'A'])
		assert.deepEqual(coverageTags(xml), ['<coverage line-rate="1">'])
		assert.equal(packageNames(xml), 'A B ')
		assert.equal(packageNames(''), '')
	})

	it('finds the named file at any depth', () => {
		const dir = mkdtempSync(join(tmpdir(), 'ci-local-coverage-'))
		mkdirSync(join(dir, 'a/b'), { recursive: true })
		writeFileSync(join(dir, 'a/b/x.xml'), '')
		writeFileSync(join(dir, 'x.xml'), '')
		assert.deepEqual(findFiles(dir, 'x.xml').sort(), [`${dir}/a/b/x.xml`, `${dir}/x.xml`])
		assert.deepEqual(findFiles('/no/such', 'x.xml'), [])
		rmSync(dir, { recursive: true })
	})

	it('prints the sections between the markers and counts distinct attachment reports', () => {
		const dir = mkdtempSync(join(tmpdir(), 'ci-local-coverage-'))
		// Two attachment copies with the same content, one different, and a per-run copy a level deeper.
		for (const [path, xml] of [['g1', report(['A'])], ['g2', report(['A'])], ['g3', report(['B'])], ['run/In', report(['A'])]]) {
			mkdirSync(join(dir, path), { recursive: true })
			writeFileSync(join(dir, path, 'coverage.cobertura.xml'), xml)
		}
		const text = render({ comment: 'gate\n', summary: 'detail\n', cobertura: report(['A']), coverageDir: dir })
		rmSync(dir, { recursive: true })
		const body = text.replaceAll(dir, 'D')
		assert.equal(
			body,
			[
				'ci-local:coverage:begin',
				'gate',
				'',
				'detail',
				'',
				'Cobertura totals:',
				'<coverage line-rate="1">',
				'',
				'Per-project reports (packages each one carries):',
				'D/g1/coverage.cobertura.xml: A ',
				'D/g2/coverage.cobertura.xml: A ',
				'D/g3/coverage.cobertura.xml: B ',
				'D/run/In/coverage.cobertura.xml: A ',
				'ci-local:reports=2',
				'ci-local:coverage:end',
				'',
			].join('\n'),
		)
	})

	it('prints just the markers and headings when nothing was produced', () => {
		const text = render({ comment: '', summary: '', cobertura: '', coverageDir: '/no/such' })
		assert.equal(text, 'ci-local:coverage:begin\n\n\nCobertura totals:\n\nPer-project reports (packages each one carries):\nci-local:reports=0\nci-local:coverage:end\n')
	})
})
