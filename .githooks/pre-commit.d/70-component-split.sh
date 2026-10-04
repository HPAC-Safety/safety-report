#!/usr/bin/env sh
#
# Component split: every *.view.tsx is markup only, and no shipped file imports
# test code (ADR-0188). Wakes on any staged file under src/web/src/, or the
# guard itself. tools/web/check-component-split.ts reads the working tree, as
# its backstop, CI's "web" job, does.

set -u

printf '%s\n' "$STAGED" | grep -qE '^(src/web/src/|tools/web/check-component-split\.ts$)' || exit 0

if ! command -v node >/dev/null 2>&1; then
	echo "pre-commit: node is not on PATH, cannot check the component split — see README.md" >&2
	exit 1
fi

node tools/web/check-component-split.ts
