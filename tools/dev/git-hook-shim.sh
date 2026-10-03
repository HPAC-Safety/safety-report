#!/usr/bin/env sh
#
# The git hook ./init-dev.sh installs under every hook name (pre-commit,
# commit-msg, post-merge, post-rewrite). It holds no logic: it runs the
# repository's tracked copy, .githooks/<this hook's name>, from the worktree git
# is working in. So moving a tool, or changing a hook, can never leave an
# installed hook stale, and a worktree runs its own branch's hook, not the
# main checkout's. Edit .githooks/<name>, never the installed file.
#
# A worktree whose branch has no .githooks/<name> (an old one) runs nothing: a
# missing hook must never block a commit.

set -u

name=$(basename "$0")
root=$(git rev-parse --show-toplevel 2>/dev/null) || exit 0
hook="$root/.githooks/$name"

[ -f "$hook" ] || exit 0

# exec keeps the hook's exit status and its stdin (post-rewrite reads the
# rewritten commits from it). `sh` runs it whether or not the tracked file is
# executable.
exec sh "$hook" "$@"
