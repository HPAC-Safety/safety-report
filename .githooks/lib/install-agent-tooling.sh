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
# No ORIG_HEAD to compare with: assume they did.
agent_tooling_changed() {
	_changed=$(git diff --name-only ORIG_HEAD HEAD 2>/dev/null) || return 0
	printf '%s\n' "$_changed" | grep -Eq '^(Skillfile$|Skillfile\.lock$|agents/|skills/)'
}

# Delete each installed file or directory under $1 whose name is not one of the
# lines in $3. $2 is the extension a name carries on disk ("" for a skill).
_prune_installed() {
	_dir=$1
	_ext=$2
	_declared=$3
	[ -d "$_dir" ] || return 0
	for _path in "$_dir"/*; do
		[ -e "$_path" ] || continue
		_base=$(basename "$_path")
		_name=${_base%"$_ext"}
		if ! printf '%s\n' "$_declared" | grep -Fxq -- "$_name"; then
			rm -rf -- "$_path"
			echo "agent tooling: removed retired $_path"
		fi
	done
}

# Install, then prune. Returns 0 when skillfile is absent (silently) or the
# install worked, 1 when `skillfile install` failed. $1 is the calling hook's
# name, for its messages. A hook calls it behind `agent_tooling_changed`;
# init-dev.sh calls it always.
install_agent_tooling() {
	command -v skillfile >/dev/null 2>&1 || return 0

	skillfile install >/dev/null 2>&1 || return 1

	# An empty or failed listing prunes nothing: it would otherwise read as
	# "declare nothing" and delete every copy.
	if _agents=$(skillfile list --names-only --agents 2>/dev/null) && [ -n "$_agents" ]; then
		_prune_installed .claude/agents .md "$_agents"
	fi
	if _skills=$(skillfile list --names-only --skills 2>/dev/null) && [ -n "$_skills" ]; then
		_prune_installed .claude/skills "" "$_skills"
	fi

	# `skillfile install` can rewrite the tracked Skillfile.lock. A hook never
	# leaves a change on main (#802, CONV-005), so restore it there.
	if ! git diff --quiet -- Skillfile.lock 2>/dev/null; then
		if [ "$(git rev-parse --abbrev-ref HEAD 2>/dev/null)" = "main" ]; then
			git checkout -- Skillfile.lock
		else
			echo "${1:-install-agent-tooling}: Skillfile.lock changed by skillfile install — include it in your next commit."
		fi
	fi
	return 0
}
