#!/usr/bin/env sh
#
# The Claude Code SessionStart hook (.claude/settings.json). It catches what the
# git hooks cannot see — a fast-forward rebase, `git worktree add`,
# `merge --squash`, a hand-resolved merge, which fire only post-checkout or
# post-commit, owned by graphify (CONV-005) — and an edit to an agent or skill,
# which changes no name.
#
# In sync means two things: the installed names equal what `skillfile list`
# declares, and a content fingerprint of Skillfile, Skillfile.lock, agents/, and
# skills/ equals the stamp the last install wrote. When either differs it starts
# the shared install and prune (lib/install-agent-tooling.sh) detached, because
# `skillfile install` may fetch from the network and a session-start hook has a
# timeout. The job's log and a mkdir lock, stale after 10 minutes, live in
# .skillfile/cache/agent-tooling/ (gitignored).
#
# Deterministic, no model. Silent when skillfile is absent, nothing differs, or a
# sync is already running. Every path exits 0, so it never blocks a session.
# ./init-dev.sh is the manual fallback.

SELF_DIR=$(cd "$(dirname "$0")" 2>/dev/null && pwd) || exit 0
REPO_ROOT=$(cd "$SELF_DIR/../.." 2>/dev/null && pwd) || exit 0
LIB="$REPO_ROOT/.githooks/lib/install-agent-tooling.sh"

# A checkout mixing versions may have the script without the lib, or an older lib.
[ -f "$LIB" ] || exit 0
cd "$REPO_ROOT" || exit 0

# shellcheck source=.githooks/lib/install-agent-tooling.sh
. "$LIB"

command -v agent_tooling_in_sync >/dev/null 2>&1 || exit 0
command -v skillfile >/dev/null 2>&1 || exit 0
agent_tooling_in_sync && exit 0

STATE="$AGENT_TOOLING_STATE_DIR"
LOCK="$STATE/sync.lock"
LOG="$STATE/sync.log"

mkdir -p "$STATE" 2>/dev/null || exit 0
if ! mkdir "$LOCK" 2>/dev/null; then
	# Held: a sync is running, unless the holder died more than 10 minutes ago.
	[ -n "$(find "$LOCK" -maxdepth 0 -mmin +10 2>/dev/null)" ] || exit 0
	rm -rf "$LOCK"
	mkdir "$LOCK" 2>/dev/null || exit 0
fi

# The job removes the lock when it finishes, whether or not the install worked.
# shellcheck disable=SC2016 # $1 and $2 are the job's own arguments, not expanded here
nohup sh -c '. "$1"; install_agent_tooling session-start || echo "session-start: skillfile install failed — run it directly to see why"; rmdir "$2"' sh "$LIB" "$LOCK" >"$LOG" 2>&1 &

echo "session-start: agent tooling out of step with the Skillfile — syncing in the background (log: $LOG)"
exit 0
