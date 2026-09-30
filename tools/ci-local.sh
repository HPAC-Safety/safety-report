#!/usr/bin/env sh
#
# ci-local.sh — run this pull request's GitHub checks locally, under act, before
# the pull request is opened (ADR-0145).
#
#     tools/ci-local.sh --body <pr-body.md> [--job <id>]... [--verbose]
#
#   --body <file>      the draft pull request body; linked-issue,
#                      no-session-link, screenshots, and feature-coverage judge
#                      it as CI will
#   --job <id>         run only this job (repeatable); one of the allowed jobs
#   --verbose          stream act's full output instead of one line per job
#
# Without --job it runs, stopping at the first failure:
#
#   linked-issue.yml      linked-issue, no-session-link, screenshots
#   feature-coverage.yml  feature-coverage
#   terraform.yml         infra only
#   ci.yml                every job, coverage included
#
# The cheap body checks go first so a missing `Closes #N` fails in seconds, not
# after the .NET suite. It never runs traceability.yml, i18n-translate.yml,
# terraform plan or apply, deploy-*, or terraform-relock: those write to the
# repository or to AWS, and nothing here can name them. What two of them would
# commit onto the branch is stood in for: the clone gets the regenerated
# traceability matrix, and the i18n job accepts French still pending as a `#`
# stub under act (see "the bots' two commits" below).
#
# Coverage parity: the coverage job runs as in CI. It gates against the same
# baseline artifact, from the newest push run on main that ran coverage (found
# by tools/find-coverage-baseline.mjs, #589), that CI does, and measures this
# branch on Ubuntu 24.04 with the SDK global.json names. A run whose coverage
# merged a different number of per-project reports than there are test
# projects fails, because its verdict would not be CI's.
#
# No token reaches act. The repository is public, so every job runs
# anonymously; act's GITHUB_TOKEN is set explicitly empty, because act fills a
# missing one from `gh auth token`. The one thing an anonymous caller cannot do
# is download an artifact (401 even on a public repository), and the coverage
# ratchet needs one: the baseline coverage-report. So this script
# downloads it on the host, with your `gh` login, before act starts, into
# .ci-local/baseline/ in the clone act copies; the coverage job reads it from
# there and prints the run it came from. Without a gh login, a run that
# includes coverage stops before act starts: a floor-only pass would not be
# CI's verdict. The synthetic event names head repository `local/act`, so
# every write path guarded by "same repository" (the coverage comment) takes
# its fork branch.
#
# Exit codes: 0 every job passed; 1 a job failed; 2 a precondition or setup
# step failed (usage, gh login, act version, dirty tree, branch behind
# origin/main, Docker down, clone, baseline, image).
#
# There is no lock: runs from different worktrees proceed in parallel, even at
# the same time. Each run gets its own throwaway clone, its own free e2e port
# (tests/e2e/playwright.config.ts picks one when E2E_PORT is unset), and its
# own act job container names (see "unique job names" below), so one run
# can't wait behind, or clobber, another. The dev stack (dev-up.sh, dev
# compose) is unaffected: it keeps its fixed ports, on purpose, so the app is
# always at the same address in development.
#
# Full logs land in artifacts/ci-local/<workflow>[-<job>].log (gitignored).

set -eu

say()  { printf '%s\n' "$*"; }
warn() { printf 'ci-local: warning: %s\n' "$*" >&2; }
die()  { printf 'ci-local: %s\n' "$*" >&2; exit 2; }

ROOT=$(git rev-parse --show-toplevel) || die "not inside a git checkout"
cd "$ROOT" || die "cannot enter $ROOT"

usage() {
	sed -n '3,61p' "$0" | sed 's/^#\{0,1\} \{0,1\}//'
	exit "${1:-0}"
}

# ------------------------------------------------------------------ options --

BODY=''
JOBS=''
VERBOSE=0
while [ $# -gt 0 ]; do
	case "$1" in
		--body) [ $# -ge 2 ] || die "--body needs a file"; BODY=$2; shift 2 ;;
		--job) [ $# -ge 2 ] || die "--job needs a job id"; JOBS="$JOBS $2"; shift 2 ;;
		--verbose) VERBOSE=1; shift ;;
		-h|--help) usage 0 ;;
		*) printf 'ci-local: unknown option: %s\n' "$1" >&2; usage 2 >&2 ;;
	esac
done
[ -n "$BODY" ] || die "--body <pr-body.md> is required: the body checks judge it"
[ -f "$BODY" ] || die "no such body file: $BODY"
BODY=$(CDPATH='' cd -- "$(dirname -- "$BODY")" && pwd)/$(basename -- "$BODY") \
	|| die "cannot resolve $BODY"

# The allow list. A job not named here cannot be run by this script.
workflow_of() {
	case "$1" in
		changes|build|test|cucumber|docs|coverage|web|e2e|agent-config|i18n) echo ci.yml ;;
		linked-issue|no-session-link|screenshots) echo linked-issue.yml ;;
		feature-coverage) echo feature-coverage.yml ;;
		infra) echo terraform.yml ;;
		*) return 1 ;;
	esac
}
for job in $JOBS; do
	workflow_of "$job" >/dev/null || die "not an allowed job: $job"
done

# ------------------------------------------------------------- the gh login --
#
# Checked before anything slow. Only the coverage job needs it, for main's
# baseline artifact, which GitHub serves to no anonymous caller. The login
# stays on the host: it never enters act, a container, or an action.

NEED_BASELINE=0
case " ${JOBS:- coverage} " in *' coverage '*) NEED_BASELINE=1 ;; esac
if [ "$NEED_BASELINE" -eq 1 ]; then
	command -v gh >/dev/null 2>&1 \
		|| die "the coverage ratchet needs main's baseline artifact, which only an authenticated download can fetch; install gh (https://cli.github.com), then run gh auth login"
	gh auth status >/dev/null 2>&1 \
		|| die "the coverage ratchet needs main's baseline artifact, which only an authenticated download can fetch; run gh auth login"
fi

# ------------------------------------------------------------ preconditions --

command -v act >/dev/null 2>&1 || die "act is not installed; see .act-version and README.md"
WANT_ACT=$(tr -d '[:space:]' < .act-version) || die "cannot read .act-version"
HAVE_ACT=$(act --version | awk '{print $NF}') || die "act --version failed"
[ "$HAVE_ACT" = "$WANT_ACT" ] || die "act $HAVE_ACT is installed; .act-version pins $WANT_ACT"
docker info >/dev/null 2>&1 || die "Docker is not running"
command -v node >/dev/null 2>&1 || die "node is required to write the event"

# act also merges a user-level actrc into every run. The secret, variable, and
# env files are forced to /dev/null on the command line below; anything else
# in one (a -s, a --bind) still applies, so say so.
for rc in "$HOME/.actrc" "${XDG_CONFIG_HOME:-$HOME/.config}/act/actrc" \
	"$HOME/Library/Application Support/act/actrc"; do
	[ ! -s "$rc" ] || warn "$rc exists and act merges it into this run; check it holds no secret or --bind"
done

# Only committed work is measured: act runs a clone of HEAD. Untracked files
# (a draft body, say) are fine; a modified tracked file is not.
if [ -n "$(git status --porcelain --untracked-files=no)" ]; then
	die "the tree has uncommitted changes; commit them first — CI sees only commits"
fi
git fetch -q origin main || die "git fetch origin main failed"
git merge-base --is-ancestor origin/main HEAD \
	|| die "HEAD does not contain origin/main; rebase onto it first"

# ---------------------------------------------------------- the teardown --
#
# "try: do work; finally: tear down even if failed." Every test container this
# run creates is deleted when it ends, volumes included, whether it passed,
# failed, died on a precondition, or was interrupted. The finally is the EXIT
# trap, registered here, before anything can create a container. INT and TERM
# route to it (exit 130 / 143), so every way out of the script runs it once.
#
# It is best-effort per item: no step can abort it or change the run's exit
# status (the shell keeps the status `exit` was given). It only ever matches
# THIS run's tag, `ci-local-$RUN_TAG`, which act puts in every job container
# and volume name because the workflow name carries it (see "unique job
# names"): never another parallel run's, never the dev stack's containers
# (<worktree>-api-1, safety-report-*), which carry no such tag.
# Testcontainers' own containers (the Postgres and S3 under `test`) carry no
# tag: Ryuk removes them, with their volumes, when the test process ends.
#
# act-toolcache, act's tool cache shared across runs, is the one deliberate
# exception: it is a cache, keyed by tool and version, that later runs reuse.
#
# A signal reaches this shell at once only while it waits (the `wait` in
# run_act); the run's act is signalled here too, so it stops first.
#
# kill -9 cannot run a trap. So the next run starts with sweep_dead(): anything
# tagged for a pid that is no longer alive is removed before its own work.

WORK=''
RUN_TAG=''
WORKFLOWS=''

# remove_tagged <tag>: containers, then volumes, whose names carry ci-local-<tag>.
remove_tagged() {
	for c in $(docker ps -aq --filter "name=ci-local-$1-" 2>/dev/null); do
		docker rm -fv "$c" >/dev/null 2>&1 || true
	done
	for v in $(docker volume ls -q --filter "name=ci-local-$1-" 2>/dev/null); do
		docker volume rm -f "$v" >/dev/null 2>&1 || true
	done
}

cleanup() {
	set +e
	trap '' INT TERM
	if [ -n "$WORKFLOWS" ]; then
		pkill -TERM -f "act pull_request -W $WORKFLOWS" 2>/dev/null
		sleep 1
	fi
	[ -z "$RUN_TAG" ] || remove_tagged "$RUN_TAG"
	[ -z "$WORK" ] || rm -rf "$WORK"
	return 0
}
trap cleanup EXIT
trap 'exit 130' INT
trap 'exit 143' TERM

# sweep_dead: remove what a run killed with SIGKILL left. Tags look like
# ci-local-<pid>-<suffix>; a live pid's tag is never touched.
sweep_dead() {
	{
		docker ps -a --format '{{.Names}}' 2>/dev/null
		docker volume ls -q 2>/dev/null
	} | sed -nE 's/^act-.*ci-local-([0-9]+-[A-Za-z0-9]+)-.*$/\1/p' | sort -u | while read -r tag; do
		kill -0 "${tag%%-*}" 2>/dev/null || remove_tagged "$tag"
	done
}
sweep_dead

# ------------------------------------------------------------ the checkout --
#
# act copies the directory it runs in into each job container. A worktree's
# .git is a file naming a gitdir outside it, so git fails in the container. So
# act runs in a clone made from a bundle of HEAD alone: no other branch's refs
# come along (CI's checkout has none either), and its origin refs are set to
# what CI compares: main at the fetched origin/main, the branch at HEAD.

HEAD_SHA=$(git rev-parse HEAD) || die "cannot read HEAD"
BASE_SHA=$(git rev-parse origin/main) || die "cannot read origin/main"
BRANCH=$(git symbolic-ref --quiet --short HEAD || echo ci-local)
ORIGIN_URL=$(git remote get-url origin) || die "no origin remote"

WORK=$(mktemp -d "${TMPDIR:-/tmp}/hpac-ci-local.XXXXXX") || die "mktemp failed"
RUN_TAG="$$-${WORK##*.}"
git bundle create -q "$WORK/head.bundle" HEAD || die "git bundle failed"
git clone -q --no-tags "$WORK/head.bundle" "$WORK/repo" || die "git clone failed"
git -C "$WORK/repo" checkout -q -B "$BRANCH" "$HEAD_SHA" || die "git checkout failed"
git -C "$WORK/repo" remote set-url origin "$ORIGIN_URL" || die "git remote set-url failed"
git -C "$WORK/repo" update-ref "refs/remotes/origin/main" "$BASE_SHA" || die "git update-ref failed"
git -C "$WORK/repo" update-ref "refs/remotes/origin/$BRANCH" "$HEAD_SHA" || die "git update-ref failed"
rm -f "$WORK/head.bundle"
REPOSITORY=$(printf '%s' "$ORIGIN_URL" | sed -E 's#^.*github\.com[:/]##; s#\.git$##')

# ------------------------------------------------------- unique job names --
#
# act derives a job's container name deterministically from the workflow's
# top-level `name:` and the job's own name (act 0.2.89,
# pkg/runner/run_context.go: jobContainerName -> createContainerName hashes
# "<workflow name>/<job name>"; verified by reading that source, since nothing
# in act --help offers a container-name flag). Two concurrent runs of the same
# job therefore hash to the same container name and either collide or remove
# each other's container mid-run.
#
# So each run reads its workflows from retagged copies written OUTSIDE the
# clone, under $WORK/workflows/, never inside $WORK/repo — act's `-W` accepts
# a file anywhere; the directory it is invoked from (the clone) is a separate
# concept and still supplies the checkout it copies into each job container
# (verified: `act -l -W <external file>` reports the retagged name, and a full
# run against an external `-W` succeeds with the tag in its container name).
# The clone's git history is never touched. That matters because
# linked-issue.yml, feature-coverage.yml, and ci.yml's `changes` job all diff
# with `...` (merge-base) rather than a plain two-dot diff: an earlier version
# of this fix committed the retag onto the clone's HEAD, which made
# merge-base(origin/main, HEAD) still the untagged upstream commit, so every
# one of those `...` diffs saw all four workflow files as changed on every
# run, regardless of what the pull request actually touched. Writing the
# retagged copies elsewhere and leaving BASE_SHA and HEAD_SHA exactly as they
# were removes the problem instead of working around it: nothing in the
# clone's history differs from the real branch, so every diff any job or
# filter computes is the one CI would compute.
WORKFLOWS="$WORK/workflows"
mkdir -p "$WORKFLOWS" || die "cannot create $WORKFLOWS"
for wf in linked-issue feature-coverage terraform ci; do
	src="$WORK/repo/.github/workflows/$wf.yml"
	[ -f "$src" ] || continue
	head -n 1 "$src" | grep -q '^name: ' || die "$src: expected its first line to be the workflow name"
	sed -E "1s/^name: (.*)\$/name: \\1 (ci-local $RUN_TAG)/" "$src" > "$WORKFLOWS/$wf.yml" \
		|| die "cannot write $WORKFLOWS/$wf.yml"
done

# -------------------------------------------------- the bots' two commits --
#
# On a same-repository pull request, two bots commit onto the branch before
# CI's verdict settles: traceability.yml the regenerated matrix, and
# i18n-translate.yml the French for new or reworded English. Neither runs here,
# because both push. Without them a branch that changes a scenario fails
# `docs`, and one that adds an English key fails `i18n`, although CI passes.
#
# The matrix is plain generated data, so the clone gets the commit
# traceability.yml would push: `node tools/traceability.mjs`, committed on top
# when it changed anything, with HEAD and origin/<branch> moved to it. Like
# traceability.yml, it skips a branch that changes the generator itself, whose
# author regenerates by hand; the docs job then judges the committed matrix,
# as in CI. A generator that fails leaves the clone alone: the docs job runs it
# again and reports why.
#
# The French needs a translation provider, which nothing local may call.
# ci.yml's i18n job passes --allow-pending-translation under act instead,
# the pre-commit hook's branch rule. Neither changes what runs on GitHub.

if git -C "$WORK/repo" diff --quiet "$BASE_SHA" HEAD -- tools/traceability.mjs; then
	if (cd "$WORK/repo" && node tools/traceability.mjs >/dev/null 2>&1); then
		if ! git -C "$WORK/repo" diff --quiet -- docs/traceability.md; then
			git -C "$WORK/repo" -c user.name=ci-local -c user.email=ci-local@localhost \
				commit -q --no-verify -m "Regenerate the traceability matrix, as traceability.yml would" \
				-- docs/traceability.md || die "could not commit the regenerated matrix in the clone"
			HEAD_SHA=$(git -C "$WORK/repo" rev-parse HEAD) || die "cannot read the clone's HEAD"
			git -C "$WORK/repo" update-ref "refs/remotes/origin/$BRANCH" "$HEAD_SHA" \
				|| die "git update-ref failed"
			say "Traceability matrix: regenerated and committed in the clone, as traceability.yml would on GitHub."
		fi
	else
		git -C "$WORK/repo" checkout -q -- docs/traceability.md 2>/dev/null || true
		warn "node tools/traceability.mjs failed in the clone; the docs job will say why"
	fi
else
	say "Traceability matrix: not regenerated, because this branch changes tools/traceability.mjs (traceability.yml skips it too)."
fi

# ------------------------------------------------------------ the baseline --
#
# The run CI's "Fetch the main baseline" step picks, found the same way -
# tools/find-coverage-baseline.mjs, so the two can never pick a different run
# (ADR-0147, ADR-0165). main's newest green push run does not always carry the
# artifact: the coverage job is skipped on a docs-, infra-, or workflow-only
# change, so the script walks back to the newest run that actually ran
# coverage (#589). .ci-local/ is gitignored, so the jobs' own `git status`
# checks never see it, and act is told to copy ignored paths (a fresh clone
# holds nothing else that is ignored). Only Cobertura.xml is kept: the
# artifact's markdown and HTML would otherwise sit in the tree the docs checks
# read. With no baseline found at all, CI runs the floor alone, and so does
# this.

if [ "$NEED_BASELINE" -eq 1 ]; then
	BASELINE="$WORK/repo/.ci-local/baseline"
	mkdir -p "$BASELINE" || die "cannot create $BASELINE"
	RUN_ID=$(node tools/find-coverage-baseline.mjs --repo "$REPOSITORY") \
		|| die "could not find main's coverage baseline with gh; check gh auth status"
	if [ "$RUN_ID" = "none" ]; then
		say "Coverage baseline: none, no push run on main carries a non-expired coverage-report artifact; the floor alone applies, as in CI."
		echo "No push run on main carries a non-expired coverage-report artifact. The ratchet will not run; the floor still applies." > "$BASELINE/notice"
	elif gh run download "$RUN_ID" --repo "$REPOSITORY" --name coverage-report \
		--dir "$WORK/baseline-download" >/dev/null 2>&1 \
		&& [ -f "$WORK/baseline-download/Cobertura.xml" ]; then
		mv "$WORK/baseline-download/Cobertura.xml" "$BASELINE/Cobertura.xml" \
			|| die "cannot move the baseline into the clone"
		echo "$RUN_ID" > "$BASELINE/run-id"
		say "Coverage baseline: main run $RUN_ID, the run CI's ratchet uses."
	else
		die "could not download coverage-report from main run $RUN_ID, though it was reported to hold one; check gh auth status and retry"
	fi
fi

# -------------------------------------------------------------- the event --

LOGIN=$(gh api user --jq .login 2>/dev/null || echo local)
EVENT="$WORK/event.json"
BODY="$BODY" LOGIN="$LOGIN" REPOSITORY="$REPOSITORY" BRANCH="$BRANCH" \
HEAD_SHA="$HEAD_SHA" BASE_SHA="$BASE_SHA" node -e '
const fs = require("node:fs")
const e = process.env
const repo = { full_name: e.REPOSITORY, name: e.REPOSITORY.split("/")[1], owner: { login: e.REPOSITORY.split("/")[0] } }
fs.writeFileSync(process.argv[1], JSON.stringify({
	action: "synchronize",
	number: 0,
	repository: { ...repo, default_branch: "main" },
	pull_request: {
		number: 0,
		body: fs.readFileSync(e.BODY, "utf8"),
		user: { login: e.LOGIN },
		base: { ref: "main", sha: e.BASE_SHA, repo },
		// Not this repository, so every same-repository write path skips.
		head: { ref: e.BRANCH, sha: e.HEAD_SHA, repo: { full_name: "local/act", name: "act", owner: { login: "local" } } },
	},
}))' "$EVENT" || die "could not write the event"

# --------------------------------------------------------------- the image --
#
# Tagged with the committed Dockerfile's hash, so a changed Dockerfile builds a
# new image instead of reusing a stale one. `:local` follows it, for .actrc and
# a hand-run act.

DOCKERFILE_HASH=$(git -C "$WORK/repo" rev-parse --short=12 HEAD:tools/act/Dockerfile) \
	|| die "tools/act/Dockerfile is not committed"
IMAGE="hpac-safety-act:$DOCKERFILE_HASH"
if ! docker image inspect "$IMAGE" >/dev/null 2>&1; then
	say "Building $IMAGE from tools/act/Dockerfile..."
	docker build -q -t "$IMAGE" "$WORK/repo/tools/act" >/dev/null \
		|| die "could not build the runner image from tools/act/Dockerfile"
fi
docker tag "$IMAGE" hpac-safety-act:local || die "docker tag failed"

# ------------------------------------------------------------------ the run --
#
# act puts each job on the Docker VM's host network. Under Docker Desktop a
# published port reaches that network a moment after the container starts, so
# Testcontainers' first connection to Ryuk or Postgres on the gateway is
# refused. host.docker.internal answers at once. Ryuk stays on (lesson 0008).
# A native Linux engine needs none of this.

EXTRA=''
if [ "$(docker info --format '{{.OperatingSystem}}' 2>/dev/null)" = 'Docker Desktop' ]; then
	EXTRA='--env TESTCONTAINERS_HOST_OVERRIDE=host.docker.internal'
	say "Docker Desktop: Testcontainers reaches containers through host.docker.internal"
fi

LOGS="$ROOT/artifacts/ci-local"
{ rm -rf "$LOGS" && mkdir -p "$LOGS"; } || die "cannot create $LOGS"
STATUS="$WORK/status"

# run_act <workflow> [<job>]
run_act() {
	log="$LOGS/${1%.yml}${2:+-$2}.log"
	say "▶ $1${2:+ -j $2}"
	rm -f "$STATUS"
	# Run in the background and wait: a signal to this shell then interrupts
	# `wait` at once and reaches the EXIT trap, instead of queueing behind act.
	{
	(
		cd "$WORK/repo" || exit 1
		# EXTRA is empty or one flag and its value, so it must split. -P and the
		# /dev/null files here override .actrc and any user-level actrc, since
		# act reads those first. The new action cache extracts each action from
		# a bare clone per job; the default one re-checks-out a shared working
		# tree, and parallel jobs using one action (setup-node) raced on it:
		# "Cannot find module .../setup-node@v7/dist/cache-save/index.js".
		# GITHUB_TOKEN is given explicitly empty: act fills a missing one from
		# `gh auth token`, and nothing in act may hold a token. The variables
		# are unset too, so nothing act starts can read one from the environment.
		# --use-gitignore=false copies the gitignored .ci-local/baseline/.
		unset GITHUB_TOKEN GH_TOKEN GH_ENTERPRISE_TOKEN GITHUB_ENTERPRISE_TOKEN
		# --rm removes the job's containers and volumes after a failure too; act
		# already removes them after a success (never pass --reuse).
		# -W reads the retagged copy under $WORKFLOWS (see "unique job names"
		# above), not the clone's own .github/workflows/$1: the invocation
		# directory ($WORK/repo, via cd above) still supplies the checkout act
		# copies into the job container, so nothing else about what runs changes.
		# shellcheck disable=SC2086
		if act pull_request -W "$WORKFLOWS/$1" ${2:+-j "$2"} \
			-P "ubuntu-latest=$IMAGE" --use-new-action-cache --use-gitignore=false --rm \
			--secret-file /dev/null --var-file /dev/null --env-file /dev/null \
			-s GITHUB_TOKEN= -e "$EVENT" $EXTRA; then
			echo 0 > "$STATUS"
		else
			echo 1 > "$STATUS"
		fi
	) 2>&1 | if [ "$VERBOSE" -eq 1 ]; then tee "$log"; else
		tee "$log" | grep -a --line-buffered -E '🏁|❌' || true
	fi
	} &
	wait "$!" || true
	# Anything but a written 0 is a failure, including no status at all.
	if [ "$(cat "$STATUS" 2>/dev/null)" != 0 ]; then
		say "✗ $1${2:+ -j $2} failed; full log: $log"
		if grep -aqi 'rate limit' "$log"; then
			say "  A GitHub call inside act hit the anonymous rate limit, 60 an hour per IP address."
			say "  Wait for it to reset (curl -s https://api.github.com/rate_limit shows when), then rerun."
			say "  ci-local never gives act a token instead."
		fi
		return 1
	fi
}

# The coverage job prints its gate output, per-assembly summary, Cobertura
# totals, and per-project reports between markers, because act keeps no job
# summary and no artifact (see ci.yml's "Coverage for tools/ci-local.sh"
# step). A distinct-report count that is not the test-project count means a
# suite's coverage was lost, and the verdict is not CI's.
check_coverage() {
	grep -aq 'ci-local:coverage:begin' "$LOGS"/ci*.log 2>/dev/null || return 0
	if ! grep -aq 'ci-local:github-token=empty' "$LOGS"/ci*.log; then
		say "✗ the coverage job did not report an empty github.token; nothing in act may hold a token"
		return 1
	fi
	say ""
	cat "$LOGS"/ci*.log | sed -n '/ci-local:coverage:begin/,/ci-local:coverage:end/p' \
		| sed 's/^\[[^]]*\] *| \{0,1\}//' \
		| grep -av 'ci-local:'
	reports=$(cat "$LOGS"/ci*.log | sed -n 's/.*ci-local:reports= *\([0-9][0-9]*\).*/\1/p' | tail -n 1)
	projects=$(find "$WORK/repo/tests" -name '*.Tests.csproj' | wc -l | tr -d ' ')
	if [ "$reports" != "$projects" ]; then
		say "✗ coverage kept ${reports:-no} distinct per-project reports for $projects test projects; one was lost, so the local verdict is not CI's"
		return 1
	fi
}

FAILED=0
if [ -n "$JOBS" ]; then
	for job in $JOBS; do
		run_act "$(workflow_of "$job")" "$job" || { FAILED=1; break; }
	done
else
	run_act linked-issue.yml && run_act feature-coverage.yml && run_act terraform.yml infra \
		&& run_act ci.yml || FAILED=1
fi
check_coverage || FAILED=1

if [ "$FAILED" -ne 0 ]; then
	say "Local CI failed. GitHub stays the authority; this run only mirrors it."
	exit 1
fi
say "Local CI passed. Required checks on the pull request must still go green."
