import { describe, it } from 'node:test'
import assert from 'node:assert/strict'
import { mkdtempSync, mkdirSync, writeFileSync } from 'node:fs'
import { tmpdir } from 'node:os'
import { join } from 'node:path'

import { collectSourceFiles, findViolations, main, scanRepository } from '../../tools/check-hardcoded-strings.mjs'

/** Runs `main` with console output captured, restoring it afterwards even on failure. */
function runMain(root) {
	const output = { log: [], error: [] }
	const original = { log: console.log, error: console.error }
	console.log = (...args) => output.log.push(args.join(' '))
	console.error = (...args) => output.error.push(args.join(' '))
	try {
		return { code: main(root), output }
	} finally {
		console.log = original.log
		console.error = original.error
	}
}

/** A throwaway source tree, one file per entry (nested paths create subdirectories). */
function sourceTree(files) {
	const dir = mkdtempSync(join(tmpdir(), 'hardcoded-strings-'))
	for (const [relativePath, content] of Object.entries(files)) {
		const path = join(dir, relativePath)
		mkdirSync(join(path, '..'), { recursive: true })
		writeFileSync(path, content)
	}
	return dir
}

describe('the hardcoded-string scanner', () => {
	describe('given JSX text content', () => {
		it('when it is a plain string then it is flagged', () => {
			// Given
			const source = 'function App() {\n\treturn <h1>Hello World</h1>\n}\n'

			// When
			const violations = findViolations(source, 'src/App.tsx')

			// Then
			assert.equal(violations.length, 1)
			assert.equal(violations[0].text, 'Hello World')
			assert.equal(violations[0].line, 2)
		})

		it('when it is sourced from t(...) then it is not flagged', () => {
			// Given
			const source = 'function App() {\n\treturn <h1>{t("home.heroTitle")}</h1>\n}\n'

			// When
			const violations = findViolations(source, 'src/App.tsx')

			// Then
			assert.deepEqual(violations, [])
		})
	})

	describe('given a copy-bearing prop', () => {
		it('when it is a plain string literal then it is flagged', () => {
			// Given
			const source = '<img alt="HPAC" src={logo} />'

			// When
			const violations = findViolations(source, 'src/components/Header.tsx')

			// Then
			assert.equal(violations.length, 1)
			assert.equal(violations[0].text, 'HPAC')
			assert.equal(violations[0].reason, 'alt attribute')
		})

		it('when it is sourced from t(...) then it is not flagged', () => {
			// Given
			const source = '<img alt={t("header.logoAlt")} src={logo} />'

			// When
			const violations = findViolations(source, 'src/components/Header.tsx')

			// Then
			assert.deepEqual(violations, [])
		})
	})

	describe('given a comment line that looks like markup', () => {
		it('when it is a // comment then it is not flagged', () => {
			// Given
			const source = '// <h1>Hello World</h1>'

			// When
			const violations = findViolations(source, 'src/App.tsx')

			// Then
			assert.deepEqual(violations, [])
		})

		it('when it is a block-comment line then it is not flagged', () => {
			// Given
			const source = '/**\n * <h1>Hello World</h1>\n */\nfunction App() {}'

			// When
			const violations = findViolations(source, 'src/App.tsx')

			// Then
			assert.deepEqual(violations, [])
		})
	})

	describe('given a non-copy attribute', () => {
		it('when it is a plain string literal then it is not flagged', () => {
			// Given
			const source = '<NavLink to="/reports" className="text-ink">{t("nav.viewReports")}</NavLink>'

			// When
			const violations = findViolations(source, 'src/components/Nav.tsx')

			// Then
			assert.deepEqual(violations, [])
		})
	})
})

describe('collectSourceFiles', () => {
	describe('given a source tree with nested directories', () => {
		it('when it walks the tree then it finds every .ts/.tsx file and skips other extensions', () => {
			// Given
			const dir = sourceTree({
				'App.tsx': '',
				'index.css': '',
				'i18n/useLocale.ts': '',
				'components/Header.tsx': '',
			})

			// When
			const files = collectSourceFiles(dir)

			// Then
			assert.equal(files.length, 3)
			assert.ok(files.every((file) => file.endsWith('.ts') || file.endsWith('.tsx')))
		})
	})
})

describe('scanRepository', () => {
	describe('given a source tree with one hardcoded string', () => {
		it('when it scans then it reports exactly that violation', () => {
			// Given
			const dir = sourceTree({
				'App.tsx': 'function App() {\n\treturn <h1>Hello World</h1>\n}\n',
				'components/Header.tsx': 'export const Header = () => <header>{t("header.logoAlt")}</header>\n',
			})

			// When
			const { files, violations } = scanRepository(dir)

			// Then
			assert.equal(files.length, 2)
			assert.equal(violations.length, 1)
			assert.equal(violations[0].text, 'Hello World')
		})
	})

	describe('given a source tree with no hardcoded strings', () => {
		it('when it scans then it reports no violations', () => {
			// Given
			const dir = sourceTree({ 'App.tsx': 'function App() {\n\treturn <h1>{t("home.heroTitle")}</h1>\n}\n' })

			// When
			const { violations } = scanRepository(dir)

			// Then
			assert.deepEqual(violations, [])
		})
	})
})

describe('the command', () => {
	describe('given a source tree with a hardcoded string', () => {
		it('when it runs then it exits non-zero and annotates the violation', () => {
			// Given
			const dir = sourceTree({ 'App.tsx': 'function App() {\n\treturn <h1>Hello World</h1>\n}\n' })

			// When
			const { code, output } = runMain(dir)

			// Then
			assert.equal(code, 1)
			assert.equal(output.error.length, 1)
			assert.match(output.error[0], /::error file=.*Hello World/)
		})
	})

	describe('given a source tree with no hardcoded strings', () => {
		it('when it runs then it exits zero and reports the scanned count', () => {
			// Given
			const dir = sourceTree({ 'App.tsx': 'function App() {\n\treturn <h1>{t("home.heroTitle")}</h1>\n}\n' })

			// When
			const { code, output } = runMain(dir)

			// Then
			assert.equal(code, 0)
			assert.deepEqual(output.error, [])
			assert.match(output.log[0], /1 file\(s\) scanned/)
		})
	})
})
