// ESLint for every TypeScript and JavaScript file in the repository (ADR-0188).
//
// One flat config at the root, run as `npm run lint` from the root. Its own
// dependencies are in the root package.json, but the type-aware rules read the
// web app's and the browser suite's packages (React's and Playwright's types),
// so CI and a local run install `src/web` and `tests/e2e` too. Every rule is
// an error:
//
//   TypeScript  typescript-eslint strict-type-checked, with type information
//               from `parserOptions.projectService`: src/web/tsconfig.json for
//               src/web, tests/e2e/tsconfig.json for the browser suite, the
//               root tsconfig.json for tools and tests/js, and
//               src/web/tsconfig.node.json for vite.config.ts, a Node script
//               in none of the others;
//   this file   typescript-eslint strict (no types). It stays .mjs because
//               ESLint 9 loads a TypeScript config only through jiti or an
//               unstable Node flag, and a config is the one file that cannot
//               lint itself with a tool it has not loaded yet;
//   src/web     also the Rules of Hooks and exhaustive-deps, and jsx-a11y
//               recommended. React rules apply to the web app only;
//   every file  a disable comment states its reason after `--`.
//
// A rule that cannot hold for a reason is turned off on the line, with the
// reason after `--`. The few turned off or narrowed for a whole kind of file
// are named, with their reasons, in ADR-0188's strict-linting amendment.
import js from '@eslint/js'
import eslintComments from '@eslint-community/eslint-plugin-eslint-comments'
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
		ignores: ['**/node_modules/**', '**/dist/**', 'coverage/**', 'src/web/coverage/**', 'artifacts/**', '.claude/**', '.skillfile/**', 'graphify-out/**', 'tests/e2e/.features-gen/**', 'tests/e2e/test-results/**', 'tests/e2e/playwright-report/**'],
	},
	js.configs.recommended,
	{
		// strict-type-checked for TypeScript: it has real types. Each package's
		// tsconfig.json (src/web, tests/e2e) is the project its files belong to.
		files: ['**/*.{ts,tsx}'],
		extends: [tseslint.configs.strictTypeChecked],
		languageOptions: { parserOptions: { projectService: true, tsconfigRootDir: import.meta.dirname } },
		rules: {
			// `onClick={() => setOpen(true)}` is the idiom of every handler here; the
			// rule still reports a void call used as a value anywhere else.
			'@typescript-eslint/no-confusing-void-expression': ['error', { ignoreArrowShorthand: true }],
			// A number in a template is predictable (`Step ${index + 1}`); the rule still
			// refuses an object, array, null, undefined, boolean, `any` and `unknown`.
			'@typescript-eslint/restrict-template-expressions': ['error', { allowNumber: true }],
		},
	},
	{
		// vite.config.ts is a Node build script outside src/web/tsconfig.json (its
		// `include` is `src`, and Node's globals do not belong in the app), so it
		// names the project that does hold it. src/web/package.json carries
		// @types/node, so this resolves in every job that installs src/web.
		files: ['src/web/vite.config.ts'],
		languageOptions: { parserOptions: { projectService: false, project: ['src/web/tsconfig.node.json'], tsconfigRootDir: import.meta.dirname } },
	},
	{
		// This file is the only JavaScript left. The type-aware preset has nothing to
		// read from it (no annotations, outside every tsconfig `include` that checks
		// JavaScript), so it gets the strict (syntactic) preset.
		files: ['eslint.config.mjs'],
		extends: [tseslint.configs.strict],
	},
	{
		// node:test registers a test with a call whose promise the runner awaits.
		files: ['tests/js/**/*.ts'],
		rules: { '@typescript-eslint/no-floating-promises': 'off' },
	},
	{
		linterOptions: { reportUnusedDisableDirectives: 'error' },
		plugins: { '@eslint-community/eslint-comments': eslintComments },
		rules: {
			// A disable states why, after `--`.
			'@eslint-community/eslint-comments/require-description': ['error', { ignore: ['eslint-enable'] }],
			'@eslint-community/eslint-comments/no-unlimited-disable': 'error',
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
		files: ['tools/**/*.ts', 'tests/js/**/*.ts', 'eslint.config.mjs', 'src/web/vite.config.ts'],
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
