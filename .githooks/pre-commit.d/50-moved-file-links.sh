#!/usr/bin/env sh
#
# Links to a moved file: a deleted or renamed file breaks every link to it, in
# files this commit never touched, so the whole tree's links are checked
# (ADR-0183). Wakes on any staged deletion or rename. Backstop: CI's "docs"
# job. Skipped, without failing, when node is not on PATH.

set -u

git diff --cached --name-only --diff-filter=DR | grep -q . || exit 0
command -v node >/dev/null 2>&1 || exit 0

node tools/docs/check-links.ts
