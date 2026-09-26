#!/usr/bin/env sh
#
# ci-local.sh — run this pull request's GitHub checks locally, under act, before
# the pull request is opened (ADR-0145).
#
#     tools/ci-local.sh --body <pr-body.md> [--job <id>]... [--verbose]
#                       [--allow-gh-token]
#
#   --body <file>      the draft pull request body; linked-issue,
#                      no-session-link, screenshots, and feature-coverage judge
#                      it as CI will
#   --job <id>         run only this job (repeatable); one of the allowed jobs
#   --verbose          stream act's full output instead of one line per job
#   --allow-gh-token   use `gh auth token` when HPAC_ACT_TOKEN is unset (also
#                      HPAC_ACT_ALLOW_GH_TOKEN=1); it can write, so it is opt-in
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
# repository or to AWS, and nothing here can name them.
#
# Coverage parity: the coverage job runs unchanged. It downloads the same
# baseline artifact from main's last green CI run that CI does, and measures
# this branch on Ubuntu 24.04 with the SDK global.json names. A run whose
# coverage merged a different number of per-project reports than there are
# test projects fails, because its verdict would not be CI's.
#
# Secrets: only GITHUB_TOKEN reaches act, through the environment, never argv.
# It is HPAC_ACT_TOKEN: a fine-grained, read-only token for this repository
# (Actions: read, Contents: read, Metadata: read). act does not enforce a
# workflow's `permissions:`, so every job and action gets this token as is. The
# synthetic event names head repository `local/act`, so every write path
# guarded by "same repository" (the coverage comment) takes its fork branch.
#
# Exit codes: 0 every job passed; 1 a job failed; 2 a precondition or setup
# step failed (usage, token, act version, dirty tree, branch behind
# origin/main, Docker down, clone, image); 3 another run held the lock past
# HPAC_CI_LOCAL_WAIT seconds (default 3600).
#
# Full logs land in artifacts/ci-local/<workflow>[-<job>].log (gitignored).

set -eu

say()  { printf '%s\n' "$*"; }
warn() { printf 'ci-local: warning: %s\n' "$*" >&2; }
die()  { printf 'ci-local: %s\n' "$*" >&2; exit 2; }

ROOT=$(git rev-parse --show-toplevel) || die "not inside a git checkout"
cd "$ROOT" || die "cannot enter $ROOT"

usage() {
	sed -n '3,47p' "$0" | sed 's/^#\{0,1\} \{0,1\}//'
	exit "${1:-0}"
}

# ------------------------------------------------------------------ options --

BODY=''
JOBS=''
VERBOSE=0
ALLOW_GH_TOKEN=${HPAC_ACT_ALLOW_GH_TOKEN:-0}
while [ $# -gt 0 ]; do
	case "$1" in
		--body) [ $# -ge 2 ] || die "--body needs a file"; BODY=$2; shift 2 ;;
		--job) [ $# -ge 2 ] || die "--job needs a job id"; JOBS="$JOBS $2"; shift 2 ;;
		--verbose) VERBOSE=1; shift ;;
		--allow-gh-token) ALLOW_GH_TOKEN=1; shift ;;
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

# ---------------------------------------------------------------- the token --
#
# Checked before anything slow. The gh login is a full-scope token, and act
# hands GITHUB_TOKEN to every job and third-party action regardless of the
# workflow's `permissions:`, so using it is an explicit choice.

if [ -n "${HPAC_ACT_TOKEN-}" ]; then
	GITHUB_TOKEN=$HPAC_ACT_TOKEN
elif [ "$ALLOW_GH_TOKEN" = 1 ]; then
	warn "using your gh login (--allow-gh-token), which can write; act passes it to every job and action. A read-only HPAC_ACT_TOKEN is recommended (README.md)."
	GITHUB_TOKEN=$(gh auth token) || die "gh auth token failed; run gh auth login"
else
	die "set HPAC_ACT_TOKEN to a fine-grained read-only token for this repository (README.md), or pass --allow-gh-token to use your gh login, which can write"
fi
[ -n "$GITHUB_TOKEN" ] || die "the token is empty"
export GITHUB_TOKEN

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
for rc in "$HOME/.actrc" "${XDG_CONFIG_HOME:-$HOME/.config}/act/actrc"; do
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

# ---------------------------------------------------------------- the lock --
#
# Port 4173 and the Docker VM are shared by every checkout on this machine, so
# one run at a time. mkdir is atomic. The holder writes its pid and start time
# inside. A run releases only a lock it acquired and never clears another's: a
# lock left by a killed run is removed by hand.

LOCK=${HPAC_CI_LOCAL_LOCK:-${TMPDIR:-/tmp}/hpac-ci-local.lock}
WAIT=${HPAC_CI_LOCAL_WAIT:-3600}
WORK=''
LOCKED=0

cleanup() {
	[ -z "$WORK" ] || rm -rf "$WORK"
	if [ "$LOCKED" -eq 1 ]; then
		rm -f "$LOCK/holder"
		rmdir "$LOCK" 2>/dev/null || true
	fi
}
trap cleanup EXIT
trap 'exit 130' INT
trap 'exit 143' TERM

holder() {
	h=$(cat "$LOCK/holder" 2>/dev/null || true)
	pid=${h%% *}
	if [ -z "$h" ]; then
		echo "an unknown holder"
	elif kill -0 "$pid" 2>/dev/null; then
		echo "pid $pid (alive), started ${h#* }"
	else
		echo "pid $pid (not running — remove $LOCK by hand if no run is left), started ${h#* }"
	fi
}

waited=0
until mkdir "$LOCK" 2>/dev/null; do
	if [ "$waited" -ge "$WAIT" ]; then
		printf 'ci-local: %s was held for %ss by %s\n' "$LOCK" "$WAIT" "$(holder)" >&2
		exit 3
	fi
	[ "$waited" -gt 0 ] || say "Waiting for ${LOCK}, held by $(holder)."
	sleep 10
	waited=$((waited + 10))
done
LOCKED=1
printf '%s %s\n' "$$" "$(date -u +%Y-%m-%dT%H:%M:%SZ)" > "$LOCK/holder" \
	|| die "cannot write $LOCK/holder"

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
git bundle create -q "$WORK/head.bundle" HEAD || die "git bundle failed"
git clone -q --no-tags "$WORK/head.bundle" "$WORK/repo" || die "git clone failed"
git -C "$WORK/repo" checkout -q -B "$BRANCH" "$HEAD_SHA" || die "git checkout failed"
git -C "$WORK/repo" remote set-url origin "$ORIGIN_URL" || die "git remote set-url failed"
git -C "$WORK/repo" update-ref "refs/remotes/origin/main" "$BASE_SHA" || die "git update-ref failed"
git -C "$WORK/repo" update-ref "refs/remotes/origin/$BRANCH" "$HEAD_SHA" || die "git update-ref failed"
rm -f "$WORK/head.bundle"

# -------------------------------------------------------------- the event --

LOGIN=$(GH_TOKEN=$GITHUB_TOKEN gh api user --jq .login 2>/dev/null || echo local)
REPOSITORY=$(printf '%s' "$ORIGIN_URL" | sed -E 's#^.*github\.com[:/]##; s#\.git$##')
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
	(
		cd "$WORK/repo" || exit 1
		# EXTRA is empty or one flag and its value, so it must split. -P and the
		# /dev/null files here override .actrc and any user-level actrc, since
		# act reads those first.
		# shellcheck disable=SC2086
		if act pull_request -W ".github/workflows/$1" ${2:+-j "$2"} \
			-P "ubuntu-latest=$IMAGE" \
			--secret-file /dev/null --var-file /dev/null --env-file /dev/null \
			-e "$EVENT" -s GITHUB_TOKEN $EXTRA; then
			echo 0 > "$STATUS"
		else
			echo 1 > "$STATUS"
		fi
	) 2>&1 | if [ "$VERBOSE" -eq 1 ]; then tee "$log"; else
		tee "$log" | grep -a --line-buffered -E '🏁|❌' || true
	fi
	# Anything but a written 0 is a failure, including no status at all.
	if [ "$(cat "$STATUS" 2>/dev/null)" != 0 ]; then
		say "✗ $1${2:+ -j $2} failed; full log: $log"
		return 1
	fi
}

# The coverage job prints its gate output, per-assembly summary, Cobertura
# totals, and per-project reports between markers, because act keeps no job
# summary and no artifact (see ci.yml's "Coverage for tools/ci-local.sh"
# step). A report count that is not the test-project count means a suite's
# coverage was lost, and the verdict is not CI's.
check_coverage() {
	grep -aq 'ci-local:coverage:begin' "$LOGS"/ci*.log 2>/dev/null || return 0
	say ""
	cat "$LOGS"/ci*.log | sed -n '/ci-local:coverage:begin/,/ci-local:coverage:end/p' \
		| sed 's/^\[[^]]*\] *| \{0,1\}//' \
		| grep -av 'ci-local:'
	reports=$(cat "$LOGS"/ci*.log | sed -n 's/.*ci-local:reports= *\([0-9][0-9]*\).*/\1/p' | tail -n 1)
	projects=$(find "$WORK/repo/tests" -name '*.Tests.csproj' | wc -l | tr -d ' ')
	if [ "$reports" != "$projects" ]; then
		say "✗ coverage merged ${reports:-no} per-project reports for $projects test projects; the local verdict is not CI's"
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
