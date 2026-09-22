#!/usr/bin/env sh
#
# check-coverage.sh — run the same coverage gate CI runs, locally, before you
# push. Mirrors .github/workflows/ci.yml's coverage job: collect coverage with
# coverlet, merge with ReportGenerator, fetch main's last successful run as
# the ratchet baseline, then run tools/coverage-gate.mjs against it.
#
#     ./check-coverage.sh              full run against main's baseline
#     ./check-coverage.sh --no-baseline   skip the ratchet, only enforce the floor
#
# Requires Docker running (the Api/Infrastructure suites use Testcontainers),
# and the GitHub CLI authenticated (`gh auth status`) to fetch the baseline —
# omit that requirement with --no-baseline.

set -eu

REPO_ROOT=$(CDPATH='' cd -- "$(dirname -- "$0")" && pwd)
cd "$REPO_ROOT"

BASELINE_ARG="none"

if [ "${1-}" != "--no-baseline" ]; then
	if ! command -v gh >/dev/null 2>&1; then
		echo "note: gh CLI not found — skipping the main-branch ratchet, only the floor applies." >&2
	else
		RUN_ID=$(gh run list --workflow CI --branch main --status success \
			--limit 1 --json databaseId --jq '.[0].databaseId' 2>/dev/null || true)

		if [ -n "${RUN_ID:-}" ]; then
			rm -rf ./artifacts/baseline
			if gh run download "$RUN_ID" --name coverage-report --dir ./artifacts/baseline >/dev/null 2>&1 \
				&& [ -f ./artifacts/baseline/Cobertura.xml ]; then
				echo "Baseline from main run $RUN_ID."
				BASELINE_ARG="./artifacts/baseline/Cobertura.xml"
			else
				echo "note: main run $RUN_ID has no coverage-report artifact yet — only the floor applies." >&2
			fi
		else
			echo "note: no successful CI run found on main — only the floor applies." >&2
		fi
	fi
fi

echo "Restoring dotnet tools..."
dotnet tool restore

echo "Running the full suite with coverage (this runs the same Testcontainers suites CI does)..."
rm -rf ./artifacts/coverage ./artifacts/report
dotnet test HpacSafety.slnx \
	--configuration Release \
	--filter "Category!=ui" \
	--collect:"XPlat Code Coverage" \
	--settings coverlet.runsettings \
	--results-directory ./artifacts/coverage

echo "Merging coverage into one report..."
dotnet tool run reportgenerator \
	"-reports:./artifacts/coverage/**/coverage.cobertura.xml" \
	"-targetdir:./artifacts/report" \
	"-reporttypes:Cobertura;MarkdownSummaryGithub;HtmlInline"

echo "Gate:"
node tools/coverage-gate.mjs \
	--report ./artifacts/report/Cobertura.xml \
	--baseline "$BASELINE_ARG" \
	--min-line 80 --min-branch 70

echo
echo "Per-file detail: ./artifacts/report/index.html"
