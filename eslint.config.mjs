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
//               src/web, tests/e2e/tsconfig.json for the browser suite
//               (vite.config.ts, a Node script in neither, gets strict without
//               types);
//   JavaScript  typescript-eslint strict (no types: plain .mjs carries none),
//               plus no-floating-promises, no-misused-promises,
//               await-thenable and require-await, which need only inference,
//               against the root tsconfig.json (allowJs, not checkJs);
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
		ignores: ['**/node_modules/**', '**/dist/**', '**/coverage/**', 'artifacts/**', '.claude/**', '.skillfile/**', 'graphify-out/**', 'tests/e2e/.features-gen/**', 'tests/e2e/test-results/**', 'tests/e2e/playwright-report/**'],
	},
	js.configs.recommended,
	{
		// strict-type-checked for TypeScript: it has real types. Each package's
		// tsconfig.json (src/web, tests/e2e) is the project its files belong to.
		files: ['**/*.{ts,tsx}'],
		ignores: ['src/web/vite.config.ts'],
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
		// `include` is `src`, and the web job installs no @types/node), so it has no
		// project to take types from: the strict preset without them.
		files: ['src/web/vite.config.ts'],
		extends: [tseslint.configs.strict],
	},
	{
		// Plain JavaScript (tools, tests/js, this file) carries no annotations, so
		// the type-aware presets would report `any` everywhere. It gets the strict
		// (syntactic) preset, plus the type-aware rules that find real bugs without
		// annotations, against the root tsconfig.json (allowJs, not checkJs).
		files: ['**/*.{mjs,js,cjs}'],
		extends: [tseslint.configs.strict],
		languageOptions: { parserOptions: { projectService: true, tsconfigRootDir: import.meta.dirname } },
		rules: {
			'@typescript-eslint/no-floating-promises': 'error',
			'@typescript-eslint/no-misused-promises': 'error',
			'@typescript-eslint/await-thenable': 'error',
			'@typescript-eslint/require-await': 'error',
		},
	},
	{
		// Test code. Three rules are off here, for the reasons in ADR-0188's
		// 2026-10-02 strict-linting amendment: a fixture is indexed after the test built
		// it, so a `!` that fails is a failing test; `expect(mock.method)` reads
		// a method to assert on it, which `unbound-method` reads as a bug; and an
		// `async` callback with no `await` is the shape `act` and Playwright want.
		files: ['src/web/src/**/*.test.{ts,tsx}', 'tests/e2e/**/*.ts', 'tests/js/**/*.mjs'],
		rules: {
			'@typescript-eslint/no-non-null-assertion': 'off',
			'@typescript-eslint/unbound-method': 'off',
			'@typescript-eslint/require-await': 'off',
		},
	},
	{
		// node:test registers a test with a call whose promise the runner awaits.
		files: ['tests/js/**/*.mjs'],
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
