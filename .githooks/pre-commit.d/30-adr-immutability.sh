#!/usr/bin/env sh
#
# ADR immutability: an accepted ADR changes only its status and is never
# deleted (ADR-0192). Wakes on a staged modification, deletion, or rename of a
# decision record. The index is judged against the merge base with
# origin/main, as CI judges the pull request against its base. Backstop: CI's
# "docs" job. Skipped, without failing, when node is not on PATH.

set -u

git diff --cached --name-only --diff-filter=MDR | grep -q '^\.spec/decisions/ADR-' || exit 0
command -v node >/dev/null 2>&1 || exit 0

node tools/spec/check-adr-immutability.ts --staged
