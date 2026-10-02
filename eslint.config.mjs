// ESLint for every TypeScript and JavaScript file in the repository (ADR-0188).
//
// One flat config at the root, run as `npm run lint` from the root (dependencies
// are in the root package.json, so the web app's and the browser suite's
// package files stay about their own runtime and tests). The set is the
// recommended presets, every rule an error:
//
//   everywhere  @eslint/js recommended, typescript-eslint recommended (not
//               the type-checked presets: those are a separate, later step);
//   src/web     also the Rules of Hooks and exhaustive-deps, and jsx-a11y
//               recommended. React rules apply to the web app only.
//
// A rule that cannot hold for a reason is turned off on the line, with the
// reason after `--`, never for a directory.
import js from '@eslint/js'
import jsxA11y from 'eslint-plugin-jsx-a11y'
import reactHooks from 'eslint-plugin-react-hooks'
import globals from 'globals'
import tseslint from 'typescript-eslint'

/** A preset's warnings raised to errors; its rules switched off stay off. */
function asErrors(rules) {
	const severity = (setting) => (Array.isArray(setting) ? setting[0] : setting)
	const raised = (setting) => (Array.isArray(setting) ? ['error', ...setting.slice(1)] : 'error')
	return Object.fromEntries(
		Object.entries(rules).map(([name, setting]) => [name, ['off', 0].includes(severity(setting)) ? setting : raised(setting)]),
	)
}

const a11y = jsxA11y.flatConfigs.recommended

export default tseslint.config(
	{
		ignores: ['**/node_modules/**', '**/dist/**', '**/coverage/**', 'artifacts/**', '.claude/**', '.skillfile/**', 'graphify-out/**', 'tests/e2e/.features-gen/**', 'tests/e2e/test-results/**', 'tests/e2e/playwright-report/**'],
	},
	js.configs.recommended,
	...tseslint.configs.recommended,
	{
		linterOptions: { reportUnusedDisableDirectives: 'error' },
		rules: {
			// A name that starts with an underscore is unused on purpose: a step
			// definition's positional capture, the rest of an object destructured to
			// drop one key.
			'@typescript-eslint/no-unused-vars': [
				'error',
				{ argsIgnorePattern: '^_', varsIgnorePattern: '^_', caughtErrorsIgnorePattern: '^_', destructuredArrayIgnorePattern: '^_', ignoreRestSiblings: true },
			],
		},
	},
	{
		files: ['tools/**/*.mjs', 'tests/js/**/*.mjs', 'eslint.config.mjs', 'src/web/vite.config.ts'],
		languageOptions: { globals: globals.node },
	},
	{
		// Playwright steps run in Node and hand callbacks to the page, which read the browser's globals.
		files: ['tests/e2e/**/*.ts'],
		languageOptions: { globals: { ...globals.node, ...globals.browser } },
	},
	{
		files: ['src/web/src/**/*.{ts,tsx}'],
		...a11y,
		languageOptions: { ...a11y.languageOptions, globals: { ...globals.browser } },
		plugins: { ...a11y.plugins, 'react-hooks': reactHooks },
		rules: {
			...asErrors(a11y.rules),
			'react-hooks/rules-of-hooks': 'error',
			'react-hooks/exhaustive-deps': 'error',
		},
	},
)
