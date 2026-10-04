#!/usr/bin/env sh
#
# Source inventory: every directory under src/ holding a tracked file has a row
# in docs/source-inventory.md, and every row names one (ADR-0143). Wakes when a
# file under src/ is added or deleted, or docs/source-inventory.md is staged.
# tools/docs/check-inventories.ts reads the index, so it judges exactly what
# this commit holds. Backstop: CI's "docs" job.

set -u

if ! git diff --cached --name-only --diff-filter=AD | grep -q '^src/' \
	&& ! printf '%s\n' "$STAGED" | grep -q '^docs/source-inventory\.md$'; then
	exit 0
fi

if ! command -v node >/dev/null 2>&1; then
	echo "pre-commit: node is not on PATH, cannot verify the source inventory — see README.md" >&2
	exit 1
fi

node tools/docs/check-inventories.ts
