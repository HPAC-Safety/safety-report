#!/usr/bin/env sh
#
# ESLint: the same rules as CI's "lint" job, its backstop (ADR-0188). Wakes on
# a staged TypeScript or JavaScript file, and lints just those; when the
# config, a tsconfig, or the root package.json or its lock is staged, lints
# everything. It needs the root dependencies (`npm ci` at the repository root)
# and, for type-aware rules, the web app's, the browser suite's, and the
# Gherkin check's (`npm --prefix src/web ci`, `npm --prefix tests/e2e ci`,
# `npm --prefix tools/gherkin ci`).
#
# When anything under tests/e2e is staged, the browser suite is also
# type-checked (`npm run typecheck:e2e`), as the "lint" job does, because
# Playwright transpiles without checking.

set -u

CONFIG='^(eslint\.config\.mjs|tsconfig\.json|package(-lock)?\.json|src/web/tsconfig\.json|src/web/tsconfig\.node\.json|tests/e2e/tsconfig\.json)$'

STAGED_SCRIPTS=$(printf '%s\n' "$STAGED" | grep -E '\.(ts|tsx|mjs|js|cjs)$' || true)

if [ -z "$STAGED_SCRIPTS" ] && ! printf '%s\n' "$STAGED" | grep -qE "$CONFIG"; then
	exit 0
fi

status=0

if ! command -v node >/dev/null 2>&1; then
	echo "pre-commit: node is not on PATH, cannot lint — see README.md" >&2
	status=1
elif [ ! -x node_modules/.bin/eslint ]; then
	echo "pre-commit: ESLint is not installed; run npm ci at the repository root" >&2
	status=1
elif [ ! -d src/web/node_modules ] || [ ! -d tests/e2e/node_modules ] || [ ! -d tools/gherkin/node_modules ]; then
	echo "pre-commit: ESLint's type-aware rules need the web app's and the browser suite's packages; run npm --prefix src/web ci, npm --prefix tests/e2e ci and npm --prefix tools/gherkin ci" >&2
	status=1
elif printf '%s\n' "$STAGED" | grep -qE "$CONFIG"; then
	node_modules/.bin/eslint . || status=1
else
	# shellcheck disable=SC2086 # word-split on purpose: one path per argument
	node_modules/.bin/eslint $STAGED_SCRIPTS || status=1
fi

# Playwright transpiles the browser suite without checking it, so a type error
# in a step is found here and in CI's "lint" job, nowhere else. The packages it
# needs are the ones the lint above already insisted on.
if command -v node >/dev/null 2>&1 && [ -x node_modules/.bin/eslint ] && [ -d tests/e2e/node_modules ] \
	&& printf '%s\n' "$STAGED" | grep -qE '^(tests/e2e/|tsconfig\.json$|package(-lock)?\.json$)'; then
	npm run --silent typecheck:e2e || status=1
fi

exit "$status"
