#!/usr/bin/env sh
#
# Workflow steps: a workflow step runs one command; its logic lives in a
# tested script under tools/ (ADR-0189). Wakes on any staged file under
# .github/workflows/ or .github/actions/. Backstop: CI's "docs" job. Skipped,
# without failing, when node is not on PATH.

set -u

printf '%s\n' "$STAGED" | grep -qE '^\.github/(workflows|actions)/' || exit 0
command -v node >/dev/null 2>&1 || exit 0

node tools/github/check-workflow-steps.ts
