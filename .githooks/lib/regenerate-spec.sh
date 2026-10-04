# shellcheck shell=sh
#
# Sourced by post-merge and post-rewrite, never run as a hook: the shim runs
# only .githooks/<hook name>. Each hook keeps its own skip-on-main and
# rewrite-type logic, then calls these from the repository root.

# The specification goes into the local graphify graph, when there is one
# (ADR-0193). graphify-out/ is untracked, so this changes nothing git sees and
# runs on main too, where a pull is the moment new claims arrive.
merge_spec_graph() {
	command -v node >/dev/null 2>&1 && node tools/spec/graph-fragment.ts >/dev/null 2>&1
	return 0
}

# The claims, the matrix, and the specification index are generated from
# .spec/ and the step definitions (ADR-0084, ADR-0183, ADR-0193). Regenerate
# them, merge the specification into the graph, and stage whichever changed.
# $1 is the calling hook's name, for its message.
regenerate_spec() {
	command -v node >/dev/null 2>&1 || return 0

	node tools/spec/generate-traceability.ts --no-fail >/dev/null 2>&1 || return 0
	node tools/spec/generate-spec-index.ts >/dev/null 2>&1 || return 0
	merge_spec_graph

	for generated in .spec/claims.json .spec/traceability.md .spec/README.md; do
		if ! git diff --quiet -- "$generated"; then
			git add -- "$generated"
			echo "$1: $generated regenerated and staged — include it in your next commit."
		fi
	done
}
