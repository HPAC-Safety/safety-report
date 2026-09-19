import { describe, it } from 'node:test'
import assert from 'node:assert/strict'

import { findViolations } from '../../tools/check-hardcoded-strings.mjs'

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
