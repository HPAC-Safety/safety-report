#!/usr/bin/env bash
# Run CI's coverage gate locally, against origin/main measured on this machine,
# before a pull request is opened (lesson 0010).
#
#   tools/coverage-check.sh
#
# CI compares this branch with the coverage of main's last green run. Measured
# on a different machine, the absolute numbers differ (ffmpeg, timing); the
# difference between the two sides is what the ratchet judges. So this measures
# both sides here, with the exact commands the CI coverage job uses, and runs
# tools/coverage-gate.mjs on the pair. Exit code is the gate's.
#
# The baseline is a throwaway detached worktree of origin/main under
# .claude/worktrees/coverage-baseline, removed on exit.
set -euo pipefail

root=$(git rev-parse --show-toplevel)
baseline="$root/.claude/worktrees/coverage-baseline"

measure() {
  (
    cd "$1"
    dotnet tool restore >/dev/null
    rm -rf artifacts
    dotnet test HpacSafety.slnx --configuration Release --filter "Category!=ui" \
      --collect:"XPlat Code Coverage" --settings coverlet.runsettings \
      --results-directory ./artifacts/coverage >/dev/null
    suite=$(find tests/js -name '*.test.mjs' -o -name '*.test.js' 2>/dev/null || true)
    if [ -n "$suite" ]; then
      mkdir -p ./artifacts/coverage/js
      # shellcheck disable=SC2086
      node --test --experimental-test-coverage --test-reporter=lcov \
        --test-reporter-destination=./artifacts/coverage/js/lcov.info $suite >/dev/null
    fi
    dotnet tool run reportgenerator \
      "-reports:./artifacts/coverage/**/coverage.cobertura.xml;./artifacts/coverage/**/lcov.info" \
      "-targetdir:./artifacts/report" "-reporttypes:Cobertura" >/dev/null
  )
}

cleanup() { git -C "$root" worktree remove --force "$baseline" >/dev/null 2>&1 || true; }
trap cleanup EXIT

git -C "$root" fetch -q origin main
cleanup
git -C "$root" worktree add -q --detach "$baseline" origin/main

echo "Measuring origin/main…" >&2
measure "$baseline"
echo "Measuring this branch…" >&2
measure "$root"

node "$root/tools/coverage-gate.mjs" \
  --report "$root/artifacts/report/Cobertura.xml" \
  --baseline "$baseline/artifacts/report/Cobertura.xml" \
  --min-line 80 --min-branch 70
