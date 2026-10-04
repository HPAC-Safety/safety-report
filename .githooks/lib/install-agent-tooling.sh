# shellcheck shell=sh
#
# Sourced by post-merge, post-rewrite, and init-dev.sh, never run as a hook: the
# shim runs only .githooks/<hook name>. Call from the repository root.
#
# .claude/agents and .claude/skills belong to skillfile (docs/agent-workflow.md)
# and are gitignored, but `skillfile install` only adds and overwrites: an agent
# or skill the Skillfile no longer declares stays installed, and Claude Code
# keeps loading it. So after a change to what is declared, install, then prune
# whatever `skillfile list` does not name. Personal skills go in ~/.claude, not
# here. Nothing outside .claude/agents and .claude/skills is touched.
#
# A dev-machine convenience, not a CI gate (same framing as the other hooks).
# Silent when skillfile is not on PATH.

# True when the commits just merged or rewritten touched what skillfile reads.
# No ORIG_HEAD to compare with: assume they did. quotePath is off so a
# non-ASCII path is not wrapped in quotes, which would hide it from the match.
agent_tooling_changed() {
	_changed=$(git -c core.quotePath=false diff --name-only ORIG_HEAD HEAD 2>/dev/null) || return 0
	printf '%s\n' "$_changed" | grep -Eq '^(Skillfile$|Skillfile\.lock$|agents/|skills/)'
}

# True when the Skillfile declares an entry of kind $1 (agent or skill) that is
# a directory: one that can deploy several names, so the listing is not the full
# set of names to keep. An entry is a directory when its location is not a .md
# file and its last segment is not its own name. The listing does not say how
# many names an entry deploys, so this is a heuristic: a multi-skill directory
# entry given an explicit name equal to its last path segment is not detected.
# Today's Skillfile has no directory entry.
_has_directory_entry() {
	skillfile list --json 2>/dev/null | awk -v kind="$1" '
		/"name":/ { name = $2; gsub(/[",]/, "", name) }
		/"entity_type":/ { type = $2; gsub(/[",]/, "", type) }
		/"location":/ {
			loc = $2; gsub(/[",]/, "", loc)
			sub(/^.*:/, "", loc)
			n = split(loc, parts, "/")
			if (type == kind && loc !~ /\.md$/ && parts[n] != name) found = 1
		}
		END { exit found ? 0 : 1 }'
}

# Delete each installed entry under $1 whose name is not one of the lines in $3.
# $2 is "agent" (regular .md files only) or "skill" (directories only); anything
# else there is not skillfile's and is left alone. A symlinked $1 is never
# followed.
_prune_installed() {
	_dir=$1
	_kind=$2
	_declared=$3
	{ [ -d "$_dir" ] && [ ! -L "$_dir" ]; } || return 0
	for _path in "$_dir"/*; do
		[ -e "$_path" ] || [ -L "$_path" ] || continue
		_base=$(basename "$_path")
		if [ "$_kind" = agent ]; then
			{ [ -f "$_path" ] && [ ! -L "$_path" ]; } || continue
			case $_base in *.md) ;; *) continue ;; esac
			_name=${_base%.md}
		else
			{ [ -d "$_path" ] && [ ! -L "$_path" ]; } || continue
			_name=$_base
		fi
		if ! printf '%s\n' "$_declared" | grep -Fxq -- "$_name"; then
			rm -rf -- "$_path"
			echo "agent tooling: removed retired $_path"
		fi
	done
}

# Prune one kind ("agent" or "skill") under $2, unless the listing cannot be
# trusted: it failed, is empty, or the Skillfile has a directory entry.
_prune_kind() {
	_k=$1
	_listed=$(skillfile list --names-only "--${_k}s" 2>/dev/null) || return 0
	[ -n "$_listed" ] || return 0
	if _has_directory_entry "$_k"; then
		echo "agent tooling: not pruning ${_k}s: the Skillfile has a directory entry, which can deploy names it does not list."
		return 0
	fi
	_prune_installed "$2" "$_k" "$_listed"
}

# Where the stamp, the background log, and the background lock live: under
# .skillfile/cache/, which .gitignore already ignores.
AGENT_TOOLING_STATE_DIR=.skillfile/cache/agent-tooling

# A checksum over the sorted list and contents of everything skillfile installs
# from: Skillfile, Skillfile.lock, and every file under agents/ and skills/. The
# working tree counts, so an uncommitted edit changes it.
_content_fingerprint() {
	{
		for _f in Skillfile Skillfile.lock; do
			[ -f "$_f" ] && printf '%s\n' "$_f"
		done
		find agents skills -type f 2>/dev/null
	} | LC_ALL=C sort | while IFS= read -r _f; do
		printf '%s\n' "$_f"
		cat -- "$_f"
	done | cksum
}

# Record what the last successful install was made from.
_write_stamp() {
	mkdir -p "$AGENT_TOOLING_STATE_DIR" 2>/dev/null || return 0
	_content_fingerprint >"$AGENT_TOOLING_STATE_DIR/stamp" 2>/dev/null
	return 0
}

# The names installed under $1 for kind $2, one per line: the regular .md files
# of an agent (minus .md), or the directories of a skill. A symlinked entry
# counts only when its name is one of the lines in $3: the prune leaves symlinks
# alone, so a declared name behind one is installed, and an undeclared one is
# not skillfile's to count.
_installed_names() {
	for _p in "$1"/*; do
		[ -e "$_p" ] || continue
		_b=$(basename "$_p")
		if [ "$2" = agent ]; then
			case $_b in *.md) _n=${_b%.md} ;; *) continue ;; esac
			[ -f "$_p" ] || continue
		else
			[ -d "$_p" ] || continue
			_n=$_b
		fi
		if [ -L "$_p" ]; then
			printf '%s\n' "$3" | grep -Fxq -- "$_n" || continue
		fi
		printf '%s\n' "$_n"
	done
	return 0
}

# True when what is installed matches what is declared: for both kinds the
# installed names equal the declared names (so a missing and an extra entry both
# count as out of sync), and the content fingerprint equals the stamp the last
# install wrote (so an edited agent or skill, or a Skillfile.lock bump, counts;
# a missing stamp is out of sync). A kind whose names cannot be compared (the
# listing failed or is empty, the Skillfile has a directory entry, or its
# directory is a symlink) is skipped, the same safety rules as the prune.
agent_tooling_in_sync() {
	command -v skillfile >/dev/null 2>&1 || return 0
	for _sk in "agent .claude/agents" "skill .claude/skills"; do
		_kind=${_sk%% *}
		_where=${_sk#* }
		_listed=$(skillfile list --names-only "--${_kind}s" 2>/dev/null) || continue
		[ -n "$_listed" ] || continue
		_has_directory_entry "$_kind" && continue
		[ -L "$_where" ] && continue
		_have=$(_installed_names "$_where" "$_kind" "$_listed" | sort)
		_want=$(printf '%s\n' "$_listed" | sort)
		[ "$_have" = "$_want" ] || return 1
	done
	[ "$(cat "$AGENT_TOOLING_STATE_DIR/stamp" 2>/dev/null)" = "$(_content_fingerprint)" ]
}

# True on main, or on a detached HEAD at origin/main (a worktree on the tip).
_on_main() {
	[ "$(git rev-parse --abbrev-ref HEAD 2>/dev/null)" = "main" ] && return 0
	_head=$(git rev-parse HEAD 2>/dev/null) || return 1
	[ "$_head" = "$(git rev-parse origin/main 2>/dev/null)" ]
}

# Install, then prune. Returns 0 when skillfile is absent (silently) or the
# install worked, 1 when `skillfile install` failed. $1 is the calling hook's
# name, for its messages. A hook calls it behind `agent_tooling_changed`;
# init-dev.sh calls it always.
install_agent_tooling() {
	command -v skillfile >/dev/null 2>&1 || return 0

	# A lock already changed before the install is the contributor's own work:
	# it is neither restored nor announced.
	_lock_was_clean=0
	git diff --quiet -- Skillfile.lock 2>/dev/null && _lock_was_clean=1

	skillfile install >/dev/null 2>&1 || return 1

	_prune_kind agent .claude/agents
	_prune_kind skill .claude/skills

	# `skillfile install` can rewrite the tracked Skillfile.lock. A hook never
	# leaves a change on main (#802, CONV-005), so restore it there.
	if [ "$_lock_was_clean" = 1 ] && ! git diff --quiet -- Skillfile.lock 2>/dev/null; then
		if _on_main; then
			git checkout -- Skillfile.lock
		else
			echo "${1:-install-agent-tooling}: Skillfile.lock changed by skillfile install — include it in your next commit."
		fi
	fi

	# Written last, after the lock is settled, so the stamp matches what is on disk.
	_write_stamp
	return 0
}
