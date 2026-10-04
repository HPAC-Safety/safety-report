#!/usr/bin/env sh
#
# The Claude Code SessionStart hook (.claude/settings.json). It catches what the
# git hooks cannot see — a fast-forward rebase, `git worktree add`,
# `merge --squash`, a hand-resolved merge, which fire only post-checkout or
# post-commit, owned by graphify (CONV-005) — by comparing the installed
# .claude/agents and .claude/skills with what `skillfile list` declares, and
# running the shared install and prune only when they differ.
#
# Deterministic, no model. Silent when skillfile is absent or nothing differs.
# Always exits 0, so it never blocks a session. ./init-dev.sh is the manual
# fallback.

REPO_ROOT=$(git rev-parse --show-toplevel 2>/dev/null) || exit 0
cd "$REPO_ROOT" || exit 0

# shellcheck source=.githooks/lib/install-agent-tooling.sh
. "$REPO_ROOT/.githooks/lib/install-agent-tooling.sh"

command -v skillfile >/dev/null 2>&1 || exit 0
agent_tooling_in_sync && exit 0

if install_agent_tooling session-start; then
	echo "session-start: .claude/agents and .claude/skills were out of step with the Skillfile; ran skillfile install and pruned retired ones."
else
	echo "session-start: skillfile install failed — run it directly to see why"
fi

exit 0
