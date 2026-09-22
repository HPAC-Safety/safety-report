#!/usr/bin/env bash
# Label the running Claude Code session with the issue and pull request it owns,
# so a person running several agents can find the right terminal tab.
#
#   tools/session-label.sh "#330 · PR #331 session-label"
#   tools/session-label.sh --clear
#
# It sets the tab title by writing an OSC 0 sequence to the terminal of the
# `claude` process ($CLAUDE_PID) — the Bash tool itself has no controlling
# terminal, so /dev/tty is not available. It also records the label at
# ${CLAUDE_CONFIG_DIR:-~/.claude}/task-labels/$CLAUDE_CODE_SESSION_ID, where a
# user status line may read it. Outside a Claude Code session it does nothing.
set -eu

session="${CLAUDE_CODE_SESSION_ID:-}"
[ -n "$session" ] || exit 0
labels="${CLAUDE_CONFIG_DIR:-$HOME/.claude}/task-labels"
file="$labels/$session"

case "${1:-}" in
  --clear) rm -f "$file"; exit 0 ;;
  "") echo "usage: tools/session-label.sh \"<label>\" | --clear" >&2; exit 2 ;;
esac

# One printable line, bounded, because it ends up inside a terminal escape.
label=$(printf '%s' "$*" | tr -d '\000-\037\177' | cut -c1-80)
mkdir -p "$labels"
printf '%s\n' "$label" > "$file"

tty=$(ps -o tty= -p "${CLAUDE_PID:-$PPID}" 2>/dev/null | tr -d ' ')
case "$tty" in
  ttys*|pts/*) printf '\033]0;%s\007' "$label" > "/dev/$tty" 2>/dev/null || true ;;
esac
echo "session label: $label"
