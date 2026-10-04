#!/usr/bin/env sh
#
# C# formatting: `dotnet format --include <staged files>` on every staged .cs
# or .csproj file, auto-fixing and re-staging just those files, so formatting
# drift never reaches a commit without touching files outside the diff.
# Backstop: CI's "build" job, which re-verifies the whole solution with
# --verify-no-changes.
#
# It runs even when an earlier check fails: a single script's `set -e` once let
# a locale failure skip it (#207), and with no way to clear a pending
# translation locally, `--no-verify` used to skip it too (ADR-0057).

set -u

STAGED_CS=$(printf '%s\n' "$STAGED" | grep -E '\.(cs|csproj)$') || exit 0

if ! command -v dotnet >/dev/null 2>&1; then
	echo "pre-commit: dotnet is not on PATH, cannot run dotnet format — see README.md" >&2
	exit 1
fi

# --include scopes formatting to the staged files, not the whole solution, so
# a commit touching two files never rewrites (and re-stages) unrelated ones
# elsewhere in the tree that also happen to be out of format.
# shellcheck disable=SC2086 # word-split on purpose: one path per --include arg
dotnet format HpacSafety.slnx --include $STAGED_CS || exit 1

echo "$STAGED_CS" | while IFS= read -r f; do
	[ -f "$f" ] && git add -- "$f"
done

exit 0
